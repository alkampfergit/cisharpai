using System.Collections.Concurrent;
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Core;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi.Models;
using Cisharpai.Azure.Common;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Azure.AzureOpenAi;

/// <summary>
/// Azure OpenAI chat completion client using HttpClient.
/// Supports legacy models (GPT-4), reasoning models (o1/o3/o4), and GPT-5 via the Responses API.
/// </summary>
public sealed class AzureOpenAiChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly ConcurrentDictionary<AzureOpenAiRoutingCacheKey, AzureOpenAiRoutingCacheEntry> RoutingCache = new();

    private readonly LlmHttpClient _client;
    private readonly AzureOpenAiClientOptions _options;
    private readonly AzureOpenAiRoutingCacheKey _routingCacheKey;
    private AzureOpenAiRoutingMode _routingMode = AzureOpenAiRoutingMode.Auto;
    private AzureOpenAiModelType? _chatCompletionsModelOverride;

    internal enum AzureOpenAiModelType { Legacy, Reasoning, Gpt5 }

    private enum AzureOpenAiRoutingMode { Auto, ChatCompletions, ResponsesApi }

    private readonly record struct AzureOpenAiRoutingCacheKey(string Endpoint, string DeploymentName, string ApiVersion);

    private readonly record struct AzureOpenAiRoutingCacheEntry(
        AzureOpenAiRoutingMode RoutingMode,
        AzureOpenAiModelType? ChatCompletionsModelOverride);

    public IFeatureCollection Features { get; }

    public AzureOpenAiChatCompletionClient(
        HttpClient httpClient,
        AzureOpenAiClientOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, logger: loggerFactory?.CreateLogger<LlmHttpClient>());
        _options = options;
        _routingCacheKey = CreateRoutingCacheKey(options);

        if (!HasExplicitModelName())
            RestoreSharedRoutingState();

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
        features.Set<IStreamingChatFeature>(this);
        Features = features;
    }

    public static AzureOpenAiChatCompletionClient Create(
        IHttpMessageHandlerFactory handlerFactory,
        AzureOpenAiClientOptions options,
        TokenCredential? credential = null,
        string handlerName = "cisharpai",
        ILoggerFactory? loggerFactory = null)
    {
        var http = new HttpClient(new AzureAuthenticationHandler(options, credential) { InnerHandler = handlerFactory.CreateHandler(handlerName) })
        {
            BaseAddress = new Uri(options.Endpoint),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return new AzureOpenAiChatCompletionClient(http, options, loggerFactory);
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var modelType = DetectModelTypeForRequest(request);

            if (modelType == AzureOpenAiModelType.Gpt5)
            {
                return await ExecuteWithRouteFallbackAsync(
                    () => SendResponsesApiAsync(request, cancellationToken),
                    AzureOpenAiRoutingMode.ResponsesApi,
                    () => SendChatCompletionsAsync(
                        request,
                        DetectChatCompletionsModelTypeForRequest(request),
                        cancellationToken),
                    AzureOpenAiRoutingMode.ChatCompletions);
            }

            return await ExecuteWithRouteFallbackAsync(
                () => SendChatCompletionsAsync(request, modelType, cancellationToken),
                AzureOpenAiRoutingMode.ChatCompletions,
                () => SendResponsesApiAsync(request, cancellationToken),
                AzureOpenAiRoutingMode.ResponsesApi);
        }
        catch (LlmHttpRequestException ex)
        {
            return ChatCompletionResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return ChatCompletionResponse.Error(ex.Message);
        }
    }

    public async Task<ChatCompletionResponse> GetChatCompletionWithJsonOutputAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            jsonOutputOptions.Validate();

            var modelType = DetectModelTypeForRequest(request);

            if (modelType == AzureOpenAiModelType.Gpt5)
            {
                return await ExecuteWithRouteFallbackAsync(
                    () => SendResponsesApiWithJsonAsync(request, jsonOutputOptions, cancellationToken),
                    AzureOpenAiRoutingMode.ResponsesApi,
                    () => SendChatCompletionsWithJsonAsync(
                        request,
                        jsonOutputOptions,
                        DetectChatCompletionsModelTypeForRequest(request),
                        cancellationToken),
                    AzureOpenAiRoutingMode.ChatCompletions);
            }

            return await ExecuteWithRouteFallbackAsync(
                () => SendChatCompletionsWithJsonAsync(request, jsonOutputOptions, modelType, cancellationToken),
                AzureOpenAiRoutingMode.ChatCompletions,
                () => SendResponsesApiWithJsonAsync(request, jsonOutputOptions, cancellationToken),
                AzureOpenAiRoutingMode.ResponsesApi);
        }
        catch (LlmHttpRequestException ex)
        {
            return ChatCompletionResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return ChatCompletionResponse.Error(ex.Message);
        }
    }

    public async Task<ToolCallingResponse> GetChatCompletionWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        try
        {
            toolOptions.Validate();

            var modelType = DetectChatCompletionsModelTypeForRequest(request);
            return await SendChatCompletionsWithToolsAsync(request, toolOptions, modelType, cancellationToken);
        }
        catch (LlmHttpRequestException ex)
        {
            return ToolCallingResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return ToolCallingResponse.Error(ex.Message);
        }
    }

    public async IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modelType = DetectModelTypeForRequest(request);

        if (modelType == AzureOpenAiModelType.Gpt5)
        {
            await foreach (var chunk in StreamResponsesApiAsync(request, cancellationToken))
                yield return chunk;
            yield break;
        }

        var messages = await MapMessagesAsync(request.Messages, cancellationToken);
        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        object providerRequest = modelType == AzureOpenAiModelType.Reasoning
            ? new AzureOpenAiReasoningChatRequest
            {
                Messages = messages,
                MaxCompletionTokens = request.MaxTokens,
                ReasoningEffort = request.ReasoningEffort ?? _options.ReasoningEffort,
                Stream = true,
                StreamOptions = new AzureOpenAiStreamOptions { IncludeUsage = true }
            }
            : new AzureOpenAiChatRequest
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Messages = messages,
                Stream = true,
                StreamOptions = new AzureOpenAiStreamOptions { IncludeUsage = true }
            };

        await foreach (var json in _client.PostStreamAsync<object>(uri, providerRequest, request.ExtraParameters, cancellationToken))
        {
            AzureOpenAiStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<AzureOpenAiStreamChunk>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (chunk is null) continue;

            if (chunk.Choices is { Count: > 0 })
            {
                var choice = chunk.Choices[0];
                var toolDelta = ToolCallingHelper.MapStreamToolCallDelta(
                    choice.Delta?.ToolCalls,
                    tc => (tc.Index, tc.Id, tc.Function?.Name, tc.Function?.Arguments));
                yield return new ChatCompletionChunk(
                    Content: choice.Delta?.Content ?? string.Empty,
                    FinishReason: choice.FinishReason,
                    Model: chunk.Model,
                    PromptTokens: chunk.Usage?.PromptTokens,
                    CompletionTokens: chunk.Usage?.CompletionTokens,
                    ToolCallDelta: toolDelta);
            }
            else if (chunk.Usage is not null)
            {
                yield return new ChatCompletionChunk(
                    Content: string.Empty,
                    PromptTokens: chunk.Usage.PromptTokens,
                    CompletionTokens: chunk.Usage.CompletionTokens);
            }
        }
    }

    private async Task<ChatCompletionResponse> SendChatCompletionsAsync(
        ChatCompletionRequest request,
        AzureOpenAiModelType modelType,
        CancellationToken cancellationToken)
    {
        var providerRequest = await CreateChatCompletionsRequestAsync(request, modelType, cancellationToken);

        try
        {
            var response = await ExecuteRequestAsync(providerRequest, request, cancellationToken);
            SetChatCompletionsModelOverride(modelType);
            return response;
        }
        catch (LlmHttpRequestException ex) when (CanRetryWithAlternateChatCompletionsModel(ex, modelType))
        {
            var fallbackModelType = GetAlternateChatCompletionsModelType(modelType);
            var fallbackRequest = await CreateChatCompletionsRequestAsync(
                request,
                fallbackModelType,
                cancellationToken);
            var response = await ExecuteRequestAsync(fallbackRequest, request, cancellationToken);
            SetChatCompletionsModelOverride(fallbackModelType, persistToSharedCache: true);
            return response;
        }
    }

    private async Task<ChatCompletionResponse> SendChatCompletionsWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        AzureOpenAiModelType modelType,
        CancellationToken cancellationToken)
    {
        var providerRequest = await CreateChatCompletionsJsonRequestAsync(
            request,
            jsonOutputOptions,
            modelType,
            cancellationToken);

        try
        {
            var response = await ExecuteRequestAsync(providerRequest, request, cancellationToken);
            SetChatCompletionsModelOverride(modelType);
            return response;
        }
        catch (LlmHttpRequestException ex) when (CanRetryWithAlternateChatCompletionsModel(ex, modelType))
        {
            var fallbackModelType = GetAlternateChatCompletionsModelType(modelType);
            var fallbackRequest = await CreateChatCompletionsJsonRequestAsync(
                request,
                jsonOutputOptions,
                fallbackModelType,
                cancellationToken);
            var response = await ExecuteRequestAsync(fallbackRequest, request, cancellationToken);
            SetChatCompletionsModelOverride(fallbackModelType, persistToSharedCache: true);
            return response;
        }
    }

    private async Task<ToolCallingResponse> SendChatCompletionsWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        AzureOpenAiModelType modelType,
        CancellationToken cancellationToken)
    {
        var providerRequest = await CreateChatCompletionsToolRequestAsync(request, toolOptions, modelType, cancellationToken);

        try
        {
            var response = await ExecuteToolCallingRequestAsync(providerRequest, request, cancellationToken);
            SetChatCompletionsModelOverride(modelType);
            return response;
        }
        catch (LlmHttpRequestException ex) when (CanRetryWithAlternateChatCompletionsModel(ex, modelType))
        {
            var fallbackModelType = GetAlternateChatCompletionsModelType(modelType);
            var fallbackRequest = await CreateChatCompletionsToolRequestAsync(
                request,
                toolOptions,
                fallbackModelType,
                cancellationToken);
            var response = await ExecuteToolCallingRequestAsync(fallbackRequest, request, cancellationToken);
            SetChatCompletionsModelOverride(fallbackModelType, persistToSharedCache: true);
            return response;
        }
    }

    private async Task<object> CreateChatCompletionsRequestAsync(
        ChatCompletionRequest request,
        AzureOpenAiModelType modelType,
        CancellationToken cancellationToken)
    {
        var messages = await MapMessagesAsync(request.Messages, cancellationToken);
        return modelType == AzureOpenAiModelType.Reasoning
            ? new AzureOpenAiReasoningChatRequest
            {
                Messages = messages,
                MaxCompletionTokens = request.MaxTokens,
                ReasoningEffort = request.ReasoningEffort ?? _options.ReasoningEffort
            }
            : new AzureOpenAiChatRequest
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Messages = messages
            };
    }

    private async Task<object> CreateChatCompletionsJsonRequestAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        AzureOpenAiModelType modelType,
        CancellationToken cancellationToken)
    {
        var adjustedMessages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(request.Messages, jsonOutputOptions);
        var messages = await MapMessagesAsync(adjustedMessages, cancellationToken);
        var responseFormat = BuildResponseFormat(jsonOutputOptions);

        return modelType == AzureOpenAiModelType.Reasoning
            ? new AzureOpenAiReasoningChatRequest
            {
                Messages = messages,
                MaxCompletionTokens = request.MaxTokens,
                ReasoningEffort = request.ReasoningEffort ?? _options.ReasoningEffort,
                ResponseFormat = responseFormat
            }
            : new AzureOpenAiChatRequest
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Messages = messages,
                ResponseFormat = responseFormat
            };
    }

    private async Task<object> CreateChatCompletionsToolRequestAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        AzureOpenAiModelType modelType,
        CancellationToken cancellationToken)
    {
        var messages = await MapMessagesAsync(request.Messages, cancellationToken);
        var tools = MapToolDefinitions(toolOptions.Tools);
        var toolChoice = MapToolChoiceValue(toolOptions.ToolChoice);

        return modelType == AzureOpenAiModelType.Reasoning
            ? new AzureOpenAiReasoningChatRequest
            {
                Messages = messages,
                MaxCompletionTokens = request.MaxTokens,
                ReasoningEffort = request.ReasoningEffort ?? _options.ReasoningEffort,
                Tools = tools,
                ToolChoice = toolChoice
            }
            : new AzureOpenAiChatRequest
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Messages = messages,
                Tools = tools,
                ToolChoice = toolChoice
            };
    }

    private string ResponsesApiUri =>
        $"openai/deployments/{_options.DeploymentName}/responses?api-version={_options.ApiVersion}";

    private async Task<ChatCompletionResponse> SendResponsesApiAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new AzureOpenAiResponsesApiRequest
        {
            MaxOutputTokens = request.MaxTokens,
            Input = await MapMessagesAsync(request.Messages, cancellationToken),
            Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                ? new AzureOpenAiResponsesReasoningOption { Effort = effort }
                : null,
            Text = _options.TextVerbosity is not null
                ? new AzureOpenAiTextOption { Verbosity = _options.TextVerbosity }
                : null
        };

        return await ExecuteResponsesApiAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> SendResponsesApiWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);
        var textOption = new AzureOpenAiTextOption
        {
            Verbosity = _options.TextVerbosity,
            Format = BuildResponsesApiTextFormat(jsonOptions)
        };

        var providerRequest = new AzureOpenAiResponsesApiRequest
        {
            MaxOutputTokens = request.MaxTokens,
            Input = await MapMessagesAsync(messages, cancellationToken),
            Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                ? new AzureOpenAiResponsesReasoningOption { Effort = effort }
                : null,
            Text = textOption
        };

        return await ExecuteResponsesApiAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> ExecuteResponsesApiAsync(
        AzureOpenAiResponsesApiRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var (raw, rawResponseJson, rawRequestJson) = await PostWithOptionalRawAsync<AzureOpenAiResponsesApiRequest, AzureOpenAiResponsesApiResponse>(
            ResponsesApiUri, providerRequest, request, cancellationToken);

        var content = raw.Output
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "output_text")
            .Select(c => c.Text)
            .FirstOrDefault() ?? string.Empty;

        var refusal = raw.Output
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "refusal")
            .Select(c => c.Refusal)
            .FirstOrDefault();

        var isIncomplete = raw.Status == "incomplete";
        var incompleteReason = raw.IncompleteDetails?.Reason;
        var isError = raw.Status is "incomplete" or "failed";
        var errorMessage = BuildResponsesErrorMessage(isError, raw.Status, incompleteReason);

        return new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: raw.Status,
            IncompleteReason: isIncomplete ? incompleteReason : null,
            IsSuccess: !isError,
            ErrorMessage: errorMessage,
            Refusal: refusal);
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamResponsesApiAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var providerRequest = new AzureOpenAiResponsesApiRequest
        {
            MaxOutputTokens = request.MaxTokens,
            Input = await MapMessagesAsync(request.Messages, cancellationToken),
            Stream = true,
            Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                ? new AzureOpenAiResponsesReasoningOption { Effort = effort }
                : null,
            Text = _options.TextVerbosity is not null
                ? new AzureOpenAiTextOption { Verbosity = _options.TextVerbosity }
                : null
        };

        string? model = null;

        await foreach (var json in _client.PostStreamAsync(ResponsesApiUri, providerRequest, request.ExtraParameters, cancellationToken))
        {
            AzureOpenAiResponsesStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<AzureOpenAiResponsesStreamEvent>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "response.output_text.delta":
                    yield return new ChatCompletionChunk(
                        Content: evt.Delta ?? string.Empty,
                        Model: model);
                    break;

                case "response.completed":
                    if (evt.Response is not null)
                    {
                        model = evt.Response.Model;
                        yield return new ChatCompletionChunk(
                            Content: string.Empty,
                            FinishReason: evt.Response.Status,
                            Model: model,
                            PromptTokens: evt.Response.Usage?.InputTokens,
                            CompletionTokens: evt.Response.Usage?.OutputTokens);
                    }
                    break;
            }
        }
    }

    private static AzureOpenAiTextFormat BuildResponsesApiTextFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
            return new AzureOpenAiTextFormat { Type = "json_object" };

        return new AzureOpenAiTextFormat
        {
            Type = "json_schema",
            Name = options.SchemaName,
            Description = options.SchemaDescription,
            Strict = options.Strict,
            Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
        };
    }

    private async Task<ChatCompletionResponse> ExecuteRequestAsync(
        object providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        var (raw, rawResponseJson, rawRequestJson) = await PostWithOptionalRawAsync<object, AzureOpenAiChatResponse>(
            uri, providerRequest, request, cancellationToken);

        var choice = raw.Choices.FirstOrDefault();
        var finishReason = choice?.FinishReason;
        var isIncomplete = IsIncompleteFinishReason(finishReason);

        return new ChatCompletionResponse(
            Content: ContentPartHelper.ExtractStringContent(choice?.Message.Content),
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: finishReason,
            IncompleteReason: isIncomplete ? finishReason : null,
            IsSuccess: !isIncomplete,
            ErrorMessage: isIncomplete
                ? $"Azure OpenAI response was incomplete because finish_reason was '{finishReason}'."
                : null,
            Refusal: choice?.Message.Refusal);
    }

    private async Task<ToolCallingResponse> ExecuteToolCallingRequestAsync(
        object providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        var (raw, rawResponseJson, rawRequestJson) = await PostWithOptionalRawAsync<object, AzureOpenAiChatResponse>(
            uri, providerRequest, request, cancellationToken);

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private async Task<ChatCompletionResponse> ExecuteWithRouteFallbackAsync(
        Func<Task<ChatCompletionResponse>> primaryAction,
        AzureOpenAiRoutingMode primaryRoute,
        Func<Task<ChatCompletionResponse>> fallbackAction,
        AzureOpenAiRoutingMode fallbackRoute)
    {
        try
        {
            var response = await primaryAction();
            SetRoutingMode(primaryRoute);
            return response;
        }
        catch (LlmHttpRequestException ex) when (CanRetryWithAlternateRoute(ex))
        {
            var response = await fallbackAction();
            SetRoutingMode(fallbackRoute, persistToSharedCache: true);
            return response;
        }
    }

    private bool HasExplicitModelName() =>
        !string.IsNullOrWhiteSpace(_options.ModelName);

    private void RestoreSharedRoutingState()
    {
        if (!RoutingCache.TryGetValue(_routingCacheKey, out var cached))
            return;

        _routingMode = cached.RoutingMode;
        _chatCompletionsModelOverride = cached.ChatCompletionsModelOverride;
    }

    private void SetRoutingMode(AzureOpenAiRoutingMode routingMode, bool persistToSharedCache = false)
    {
        _routingMode = routingMode;

        if (persistToSharedCache)
            PersistSharedRoutingState();
    }

    private void SetChatCompletionsModelOverride(
        AzureOpenAiModelType? modelType,
        bool persistToSharedCache = false)
    {
        _chatCompletionsModelOverride = modelType;

        if (persistToSharedCache)
            PersistSharedRoutingState();
    }

    private void PersistSharedRoutingState()
    {
        RoutingCache[_routingCacheKey] = new AzureOpenAiRoutingCacheEntry(
            _routingMode,
            _chatCompletionsModelOverride);
    }

    private static AzureOpenAiRoutingCacheKey CreateRoutingCacheKey(AzureOpenAiClientOptions options)
    {
        return new AzureOpenAiRoutingCacheKey(
            NormalizeEndpoint(options.Endpoint),
            NormalizeCacheComponent(options.DeploymentName),
            NormalizeCacheComponent(options.ApiVersion));
    }

    private static string NormalizeEndpoint(string? endpoint)
    {
        if (string.IsNullOrWhiteSpace(endpoint))
            return string.Empty;

        if (Uri.TryCreate(endpoint, UriKind.Absolute, out var uri))
            return uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToUpperInvariant();

        return endpoint.Trim().TrimEnd('/').ToUpperInvariant();
    }

    private static string NormalizeCacheComponent(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().ToUpperInvariant();
    }

    private static bool CanRetryWithAlternateRoute(LlmHttpRequestException exception)
    {
        return exception.StatusCode == HttpStatusCode.NotFound &&
               exception.ResponseBody?.Contains("Resource not found", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static bool CanRetryWithAlternateChatCompletionsModel(
        LlmHttpRequestException exception,
        AzureOpenAiModelType modelType)
    {
        if (modelType == AzureOpenAiModelType.Gpt5 ||
            exception.StatusCode != HttpStatusCode.BadRequest ||
            string.IsNullOrWhiteSpace(exception.ResponseBody))
        {
            return false;
        }

        var responseBody = exception.ResponseBody;
        return modelType == AzureOpenAiModelType.Reasoning
            ? responseBody.Contains("max_completion_tokens", StringComparison.OrdinalIgnoreCase) ||
              responseBody.Contains("reasoning_effort", StringComparison.OrdinalIgnoreCase)
            : responseBody.Contains("max_tokens", StringComparison.OrdinalIgnoreCase) &&
              responseBody.Contains("max_completion_tokens", StringComparison.OrdinalIgnoreCase);
    }

    private static AzureOpenAiModelType GetAlternateChatCompletionsModelType(AzureOpenAiModelType modelType)
    {
        return modelType == AzureOpenAiModelType.Reasoning
            ? AzureOpenAiModelType.Legacy
            : AzureOpenAiModelType.Reasoning;
    }

    /// <summary>
    /// Centralizes the IncludeRawResponse-aware POST so the three execute paths
    /// (chat completions, tool calling, responses API) don't repeat the if/else block.
    /// </summary>
    private async Task<(TResponse Raw, string? RawResponseJson, string? RawRequestJson)> PostWithOptionalRawAsync<TRequest, TResponse>(
        string uri,
        TRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
        where TRequest : class
    {
        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<TRequest, TResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
            return (raw, rawResponseJson, rawRequestJson);
        }

        var response = await _client.PostAsync<TRequest, TResponse>(
            uri, providerRequest, request.ExtraParameters, cancellationToken);
        return (response, null, null);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        AzureOpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var finishReason = choice?.FinishReason;
        var isIncomplete = IsIncompleteFinishReason(finishReason);
        var content = ContentPartHelper.ExtractStringContent(choice?.Message.Content);

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: finishReason,
            IncompleteReason: isIncomplete ? finishReason : null,
            IsSuccess: !isIncomplete,
            ErrorMessage: isIncomplete
                ? $"Azure OpenAI response was incomplete because finish_reason was '{finishReason}'."
                : null);

        var toolCalls = ToolCallingHelper.MapResponseToolCalls(
            choice?.Message.ToolCalls,
            tc => (tc.Id, tc.Function.Name, tc.Function.Arguments));

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static bool IsIncompleteFinishReason(string? finishReason) =>
        string.Equals(finishReason, "length", StringComparison.Ordinal);

    private static string? BuildResponsesErrorMessage(bool isError, string? status, string? incompleteReason)
    {
        if (!isError)
        {
            return null;
        }

        if (incompleteReason is not null)
        {
            return $"Azure OpenAI Responses API response was {status}: {incompleteReason}";
        }

        return $"Azure OpenAI Responses API response status: {status}";
    }

    private static List<AzureOpenAiToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new AzureOpenAiToolDefinition
        {
            Type = "function",
            Function = new AzureOpenAiToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
                Strict = t.Strict
            }
        }).ToList();
    }

    private static object? MapToolChoiceValue(ToolChoice? toolChoice) =>
        ToolCallingHelper.MapToolChoice(toolChoice, name => new AzureOpenAiToolChoiceObject
        {
            Type = "function",
            Function = new AzureOpenAiToolChoiceFunction { Name = name }
        });

    private static AzureOpenAiResponseFormat BuildResponseFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new AzureOpenAiResponseFormat { Type = "json_object" };
        }

        return new AzureOpenAiResponseFormat
        {
            Type = "json_schema",
            JsonSchema = new AzureOpenAiJsonSchemaSpec
            {
                Name = options.SchemaName!,
                Description = options.SchemaDescription,
                Strict = options.Strict,
                Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
            }
        };
    }

    private static async Task<List<AzureOpenAiChatMessage>> MapMessagesAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken ct)
    {
        var result = new List<AzureOpenAiChatMessage>();

        foreach (var m in messages)
        {
            var msg = new AzureOpenAiChatMessage { Role = RoleMapper.MapRole(m.Role) };

            if (m.ContentParts is { Count: > 0 })
                msg.Content = await MapContentPartsAsync(m.ContentParts, ct);
            else
                msg.Content = m.Content;

            if (m.Role == LlmRole.Tool && m.ToolCallId is not null)
                msg.ToolCallId = m.ToolCallId;

            if (m.Role == LlmRole.Assistant && m.ToolCalls is { Count: > 0 })
                msg.ToolCalls = MapToolCalls(m.ToolCalls);

            result.Add(msg);
        }

        return result;
    }

    private static Task<List<AzureOpenAiContentPart>> MapContentPartsAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        CancellationToken ct)
    {
        return ContentPartHelper.MapOpenAiStyleContentPartsAsync(
            contentParts,
            text => new AzureOpenAiContentPart { Type = "text", Text = text },
            url => new AzureOpenAiContentPart { Type = "image_url", ImageUrl = new AzureOpenAiImageUrl { Url = url } },
            ct);
    }

    private static List<AzureOpenAiToolCall> MapToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        return toolCalls.Select(tc => new AzureOpenAiToolCall
        {
            Id = tc.Id,
            Type = "function",
            Function = new AzureOpenAiToolCallFunction
            {
                Name = tc.FunctionName,
                Arguments = tc.Arguments.GetRawText()
            }
        }).ToList();
    }

    /// <summary>
    /// Picks the model identifier used for routing decisions.
    /// <see cref="AzureOpenAiClientOptions.ModelName"/> wins when set (covers opaque deployment names);
    /// otherwise falls back to the first recognizable hint from request model,
    /// default model, or deployment name.
    /// </summary>
    private AzureOpenAiModelType DetectModelTypeForRequest(ChatCompletionRequest request)
    {
        return _routingMode switch
        {
            AzureOpenAiRoutingMode.ResponsesApi => AzureOpenAiModelType.Gpt5,
            AzureOpenAiRoutingMode.ChatCompletions => DetectChatCompletionsModelTypeForRequest(request),
            _ => GetRoutingHints(request)
                .Select(TryDetectModelType)
                .FirstOrDefault(modelType => modelType.HasValue)
                ?? AzureOpenAiModelType.Legacy
        };
    }

    private AzureOpenAiModelType DetectChatCompletionsModelTypeForRequest(ChatCompletionRequest request)
    {
        if (_chatCompletionsModelOverride is { } overrideModel)
            return overrideModel;

        return GetRoutingHints(request)
            .Select(TryDetectModelType)
            .FirstOrDefault(modelType => modelType == AzureOpenAiModelType.Reasoning)
            ?? AzureOpenAiModelType.Legacy;
    }

    private IEnumerable<string?> GetRoutingHints(ChatCompletionRequest request)
    {
        yield return _options.ModelName;
        yield return request.Model;
        yield return _options.DefaultModel;
        yield return _options.DeploymentName;
    }

    private static AzureOpenAiModelType? TryDetectModelType(string? model)
    {
        if (string.IsNullOrWhiteSpace(model))
            return null;

        if (model.StartsWith("gpt-", StringComparison.OrdinalIgnoreCase))
            return model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase)
                ? AzureOpenAiModelType.Gpt5
                : AzureOpenAiModelType.Legacy;

        if (model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            model.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            return AzureOpenAiModelType.Reasoning;

        return null;
    }

    internal static AzureOpenAiModelType DetectModelType(string? model)
    {
        return TryDetectModelType(model) ?? AzureOpenAiModelType.Legacy;
    }
}
