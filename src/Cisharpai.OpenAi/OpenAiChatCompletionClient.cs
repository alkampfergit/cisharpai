using System.Runtime.CompilerServices;
using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.OpenAi.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Cisharpai.OpenAi;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature, IGroundedChatFeature
{
    private const string ChatCompletionsEndpoint = "chat/completions";
    private const string ResponsesEndpoint = "responses";

    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LlmHttpClient _client;
    private readonly OpenAiClientOptions _options;

    public IFeatureCollection Features { get; }

    public OpenAiChatCompletionClient(HttpClient httpClient, OpenAiClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, logger: loggerFactory?.CreateLogger<LlmHttpClient>());
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
        features.Set<IStreamingChatFeature>(this);
        features.Set<IGroundedChatFeature>(this);
        Features = features;
    }

    public static OpenAiChatCompletionClient Create(
        IHttpMessageHandlerFactory handlerFactory,
        OpenAiClientOptions options,
        string handlerName = "cisharpai",
        ILoggerFactory? loggerFactory = null)
    {
        var http = new HttpClient(new OpenAiAuthenticationHandler(options) { InnerHandler = handlerFactory.CreateHandler(handlerName) })
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return new OpenAiChatCompletionClient(http, options, loggerFactory);
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            var modelType = DetectModelType(model);

            return modelType switch
            {
                OpenAiModelType.Gpt5 => await SendResponsesApiAsync(request, cancellationToken),
                OpenAiModelType.Reasoning => await SendReasoningChatAsync(request, cancellationToken),
                _ => await SendLegacyChatAsync(request, cancellationToken)
            };
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
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            jsonOutputOptions.Validate();

            var modelType = DetectModelType(model);

            return modelType switch
            {
                OpenAiModelType.Gpt5 => await SendResponsesApiWithJsonAsync(request, jsonOutputOptions, cancellationToken),
                OpenAiModelType.Reasoning => await SendReasoningChatWithJsonAsync(request, jsonOutputOptions, cancellationToken),
                _ => await SendLegacyChatWithJsonAsync(request, jsonOutputOptions, cancellationToken)
            };
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
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            toolOptions.Validate();

            var modelType = DetectModelType(model);

            return modelType switch
            {
                OpenAiModelType.Reasoning => await SendReasoningChatWithToolsAsync(request, toolOptions, cancellationToken),
                _ => await SendLegacyChatWithToolsAsync(request, toolOptions, cancellationToken)
            };
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
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        var modelType = DetectModelType(model);

        if (modelType == OpenAiModelType.Gpt5)
        {
            await foreach (var chunk in StreamResponsesApiAsync(request, cancellationToken))
                yield return chunk;
        }
        else
        {
            await foreach (var chunk in StreamLegacyChatAsync(request, cancellationToken))
                yield return chunk;
        }
    }

    public async Task<GroundedChatCompletionResponse> GetGroundedChatCompletionAsync(
        ChatCompletionRequest request,
        GroundedChatOptions groundedChatOptions,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            groundedChatOptions.Validate();

            var modelType = DetectModelType(model);
            if (modelType != OpenAiModelType.Gpt5)
            {
                return GroundedChatCompletionResponse.Error(
                    $"Grounded chat requires the Responses API (GPT-5 models). Model '{model}' uses the Chat Completions API which does not support native document grounding with citations. Use a GPT-5 model or consider the prompt-injection grounding approach.");
            }

            var messages = new List<object>(await MapMessagesAsync(request.Messages, cancellationToken));
            var inputFiles = MapDocumentChunksToInputFiles(groundedChatOptions.Documents);
            if (!EmbedInputFilesInUserMessage(messages, inputFiles))
            {
                return GroundedChatCompletionResponse.Error(
                    "Grounded chat requires at least one user message to attach documents to.");
            }

            var providerRequest = new OpenAiResponsesApiRequest
            {
                Model = model,
                MaxOutputTokens = request.MaxTokens,
                Input = messages,
                Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                    ? new OpenAiReasoningOption { Effort = effort }
                    : null,
                Text = _options.TextVerbosity is not null
                    ? new OpenAiTextOption { Verbosity = _options.TextVerbosity }
                    : null
            };

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            OpenAiResponsesApiResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                    ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            }
            else
            {
                raw = await _client.PostAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                    ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            }

            return MapGroundedChatResponse(raw, groundedChatOptions.Documents, rawResponseJson, rawRequestJson);
        }
        catch (LlmHttpRequestException ex)
        {
            return GroundedChatCompletionResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return GroundedChatCompletionResponse.Error(ex.Message);
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamLegacyChatAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken),
            Stream = true,
            StreamOptions = new OpenAiStreamOptions { IncludeUsage = true }
        };

        await foreach (var json in _client.PostStreamAsync(ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken))
        {
            OpenAiStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(json, StreamJsonOptions);
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
                var streamCached = chunk.Usage?.PromptTokensDetails?.CachedTokens;
                yield return new ChatCompletionChunk(
                    Content: choice.Delta?.Content ?? string.Empty,
                    FinishReason: choice.FinishReason,
                    Model: chunk.Model,
                    PromptTokens: chunk.Usage?.PromptTokens,
                    CompletionTokens: chunk.Usage?.CompletionTokens,
                    ToolCallDelta: toolDelta,
                    CachedInputTokens: streamCached > 0 ? streamCached : null);
            }
            else if (chunk.Usage is not null)
            {
                var streamCached = chunk.Usage.PromptTokensDetails?.CachedTokens;
                yield return new ChatCompletionChunk(
                    Content: string.Empty,
                    PromptTokens: chunk.Usage.PromptTokens,
                    CompletionTokens: chunk.Usage.CompletionTokens,
                    CachedInputTokens: streamCached > 0 ? streamCached : null);
            }
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamResponsesApiAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model!,
            MaxOutputTokens = request.MaxTokens,
            Input = new List<object>(await MapMessagesAsync(request.Messages, cancellationToken)),
            Stream = true,
            Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                ? new OpenAiReasoningOption { Effort = effort }
                : null,
            Text = _options.TextVerbosity is not null
                ? new OpenAiTextOption { Verbosity = _options.TextVerbosity }
                : null
        };

        string? model = null;

        await foreach (var json in _client.PostStreamAsync(ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken))
        {
            var (mapped, evtModel) = ParseResponsesStreamEvent(json, model);
            if (evtModel is not null) model = evtModel;
            if (mapped is not null)
                yield return mapped;
        }
    }

    private static (ChatCompletionChunk? Chunk, string? Model) ParseResponsesStreamEvent(string json, string? currentModel)
    {
        OpenAiResponsesStreamEvent? evt;
        try
        {
            evt = JsonSerializer.Deserialize<OpenAiResponsesStreamEvent>(json, StreamJsonOptions);
        }
        catch
        {
            return (null, null);
        }

        if (evt is null) return (null, null);

        return evt.Type switch
        {
            "response.output_text.delta" => (
                new ChatCompletionChunk(Content: evt.Delta ?? string.Empty, Model: currentModel),
                null),

            "response.completed" when evt.Response is not null => MapCompletedResponseEvent(evt.Response),

            _ => (null, null)
        };
    }

    private static (ChatCompletionChunk Chunk, string? Model) MapCompletedResponseEvent(OpenAiResponsesApiResponse response)
    {
        var cached = response.Usage?.InputTokensDetails?.CachedTokens;
        return (
            new ChatCompletionChunk(
                Content: string.Empty,
                FinishReason: response.Status,
                Model: response.Model,
                PromptTokens: response.Usage?.InputTokens,
                CompletionTokens: response.Usage?.OutputTokens,
                CachedInputTokens: cached > 0 ? cached : null),
            response.Model);
    }

    private async Task<ChatCompletionResponse> SendLegacyChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ChatCompletionResponse> SendLegacyChatWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(messages, cancellationToken),
            ResponseFormat = BuildChatCompletionsResponseFormat(jsonOptions)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ToolCallingResponse> SendLegacyChatWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken),
            Tools = MapToolDefinitions(toolOptions.Tools),
            ToolChoice = MapToolChoiceValue(toolOptions.ToolChoice)
        };

        return await ExecuteToolCallingAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> SendReasoningChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model!,
            MaxCompletionTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ChatCompletionResponse> SendReasoningChatWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model!,
            MaxCompletionTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(messages, cancellationToken),
            ResponseFormat = BuildChatCompletionsResponseFormat(jsonOptions)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ToolCallingResponse> SendReasoningChatWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model!,
            MaxCompletionTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken),
            Tools = MapToolDefinitions(toolOptions.Tools),
            ToolChoice = MapToolChoiceValue(toolOptions.ToolChoice)
        };

        string? rawResponseJson = null;
        string? rawRequestJson = null;
        OpenAiChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private async Task<ToolCallingResponse> ExecuteToolCallingAsync(
        OpenAiChatRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        string? rawResponseJson = null;
        string? rawRequestJson = null;
        OpenAiChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private async Task<ChatCompletionResponse> SendResponsesApiAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model!,
            MaxOutputTokens = request.MaxTokens,
            Input = new List<object>(await MapMessagesAsync(request.Messages, cancellationToken)),
            Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                ? new OpenAiReasoningOption { Effort = effort }
                : null,
            Text = _options.TextVerbosity is not null
                ? new OpenAiTextOption { Verbosity = _options.TextVerbosity }
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

        var textOption = new OpenAiTextOption
        {
            Verbosity = _options.TextVerbosity,
            Format = BuildResponsesApiTextFormat(jsonOptions)
        };

        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model!,
            MaxOutputTokens = request.MaxTokens,
            Input = new List<object>(await MapMessagesAsync(messages, cancellationToken)),
            Reasoning = (request.ReasoningEffort ?? _options.ReasoningEffort) is { } effort
                ? new OpenAiReasoningOption { Effort = effort }
                : null,
            Text = textOption
        };

        return await ExecuteResponsesApiAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> ExecuteResponsesApiAsync(
        OpenAiResponsesApiRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        string? rawResponseJson = null;
        string? rawRequestJson = null;
        OpenAiResponsesApiResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }

        var parsed = ParseResponsesApiOutput(raw);
        var cachedTokens = raw.Usage.InputTokensDetails?.CachedTokens;

        return new ChatCompletionResponse(
            Content: parsed.Content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: raw.Status,
            IncompleteReason: parsed.IncompleteReason,
            IsSuccess: !parsed.IsError,
            ErrorMessage: parsed.ErrorMessage,
            Refusal: parsed.Refusal,
            CachedInputTokens: cachedTokens > 0 ? cachedTokens : null);
    }

    private static ChatCompletionResponse MapChatResponse(
        OpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var refusal = choice?.Message.Refusal;
        var cachedTokens = raw.Usage.PromptTokensDetails?.CachedTokens;

        return new ChatCompletionResponse(
            Content: ContentPartHelper.ExtractStringContent(choice?.Message.Content),
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Refusal: refusal,
            CachedInputTokens: cachedTokens > 0 ? cachedTokens : null);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        OpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var content = ContentPartHelper.ExtractStringContent(choice?.Message.Content);
        var cachedTokens = raw.Usage.PromptTokensDetails?.CachedTokens;

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            CachedInputTokens: cachedTokens > 0 ? cachedTokens : null);

        var toolCalls = ToolCallingHelper.MapResponseToolCalls(
            choice?.Message.ToolCalls,
            tc => (tc.Id, tc.Function.Name, tc.Function.Arguments));

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static List<OpenAiToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new OpenAiToolDefinition
        {
            Type = "function",
            Function = new OpenAiToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
                Strict = t.Strict
            }
        }).ToList();
    }

    private static object? MapToolChoiceValue(ToolChoice? toolChoice) =>
        ToolCallingHelper.MapToolChoice(toolChoice, name => new OpenAiToolChoiceObject
        {
            Type = "function",
            Function = new OpenAiToolChoiceFunction { Name = name }
        });

    private static OpenAiResponseFormat BuildChatCompletionsResponseFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new OpenAiResponseFormat { Type = "json_object" };
        }

        return new OpenAiResponseFormat
        {
            Type = "json_schema",
            JsonSchema = new OpenAiJsonSchemaSpec
            {
                Name = options.SchemaName!,
                Description = options.SchemaDescription,
                Strict = options.Strict,
                Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
            }
        };
    }

    private static OpenAiTextFormat BuildResponsesApiTextFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new OpenAiTextFormat { Type = "json_object" };
        }

        return new OpenAiTextFormat
        {
            Type = "json_schema",
            Name = options.SchemaName,
            Description = options.SchemaDescription,
            Strict = options.Strict,
            Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
        };
    }

    private static async Task<List<OpenAiChatMessage>> MapMessagesAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken ct)
    {
        var result = new List<OpenAiChatMessage>();

        foreach (var m in messages)
        {
            var msg = new OpenAiChatMessage { Role = RoleMapper.MapRole(m.Role) };

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

    private static Task<List<OpenAiContentPart>> MapContentPartsAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        CancellationToken ct)
    {
        return ContentPartHelper.MapOpenAiStyleContentPartsAsync(
            contentParts,
            text => new OpenAiContentPart { Type = "text", Text = text },
            url => new OpenAiContentPart { Type = "image_url", ImageUrl = new OpenAiImageUrl { Url = url } },
            ct);
    }

    private static List<OpenAiToolCall> MapToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        return toolCalls.Select(tc => new OpenAiToolCall
        {
            Id = tc.Id,
            Type = "function",
            Function = new OpenAiToolCallFunction
            {
                Name = tc.FunctionName,
                Arguments = tc.Arguments.GetRawText()
            }
        }).ToList();
    }

    private string ResolveModel(string? model)
    {
        return model
            ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "Model must be specified either in the request or via DefaultModel in options.");
    }

    internal static OpenAiModelType DetectModelType(string model)
    {
        if (model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
            model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
            model.StartsWith("o4", StringComparison.OrdinalIgnoreCase))
            return OpenAiModelType.Reasoning;

        if (model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase))
            return OpenAiModelType.Gpt5;

        return OpenAiModelType.Legacy;
    }

    private static List<OpenAiInputFile> MapDocumentChunksToInputFiles(IReadOnlyList<DocumentChunk> documents)
    {
        return documents.Select((doc, index) =>
        {
            var (filename, fileData) = GroundedChatHelper.EncodeDocumentChunk(doc, index);
            return new OpenAiInputFile { Filename = filename, FileData = fileData };
        }).ToList();
    }

    private static bool EmbedInputFilesInUserMessage(List<object> messages, List<OpenAiInputFile> inputFiles)
    {
        var lastUserMsg = messages.OfType<OpenAiChatMessage>().LastOrDefault(m => m.Role == "user");
        if (lastUserMsg == null) return false;

        var contentItems = new List<object>();
        contentItems.AddRange(inputFiles);

        switch (lastUserMsg.Content)
        {
            case string text:
                contentItems.Add(new OpenAiContentPart { Type = "input_text", Text = text });
                break;
            case IEnumerable<object> parts:
                foreach (var part in parts)
                {
                    contentItems.Add(part switch
                    {
                        OpenAiContentPart { Type: "text" } textPart =>
                            new OpenAiContentPart { Type = "input_text", Text = textPart.Text },
                        OpenAiContentPart { Type: "image_url", ImageUrl: { } img } =>
                            (object)new Dictionary<string, string> { ["type"] = "input_image", ["image_url"] = img.Url },
                        _ => part
                    });
                }
                break;
        }

        lastUserMsg.Content = contentItems;
        return true;
    }

    private static GroundedChatCompletionResponse MapGroundedChatResponse(
        OpenAiResponsesApiResponse raw,
        IReadOnlyList<DocumentChunk> documents,
        string? rawResponseJson,
        string? rawRequestJson)
    {
        var parsed = ParseResponsesApiOutput(raw);
        var cachedTokens = raw.Usage.InputTokensDetails?.CachedTokens;

        var chatCompletion = new ChatCompletionResponse(
            Content: parsed.Content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: raw.Status,
            IncompleteReason: parsed.IncompleteReason,
            IsSuccess: !parsed.IsError,
            ErrorMessage: parsed.ErrorMessage,
            Refusal: parsed.Refusal,
            CachedInputTokens: cachedTokens > 0 ? cachedTokens : null);

        var annotations = raw.Output
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "output_text" && c.Annotations is not null)
            .SelectMany(c => c.Annotations!)
            .ToList();

        var citations = GroundedChatHelper.MapAnnotationsToCitations(
            annotations, parsed.Content, documents,
            a => (a.Type, a.FileId, a.Filename, a.Index, a.StartIndex, a.EndIndex));

        return new GroundedChatCompletionResponse(chatCompletion, citations);
    }

    private readonly record struct ParsedResponsesApiOutput(
        string Content,
        string? Refusal,
        bool IsError,
        string? IncompleteReason,
        string? ErrorMessage);

    private static ParsedResponsesApiOutput ParseResponsesApiOutput(OpenAiResponsesApiResponse raw)
    {
        var messages = raw.Output.Where(o => o.Type == "message");

        var content = string.Join("", messages
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "output_text")
            .Select(c => c.Text));

        var refusal = messages
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "refusal")
            .Select(c => c.Refusal)
            .FirstOrDefault();

        var isError = raw.Status is "incomplete" or "failed";
        var errorMessage = isError
            ? (raw.IncompleteDetails?.Reason ?? $"Response status: {raw.Status}")
            : null;

        return new ParsedResponsesApiOutput(content, refusal, isError, raw.IncompleteDetails?.Reason, errorMessage);
    }

    internal enum OpenAiModelType
    {
        Legacy,
        Reasoning,
        Gpt5
    }
}
