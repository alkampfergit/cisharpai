using System.Runtime.CompilerServices;
using System.Text.Json;
using Azure.Core;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference.Models;
using Cisharpai.Azure.Common;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Azure.AzureAiInference;

/// <summary>
/// Azure AI Inference chat completion client using HttpClient.
/// Supports Azure AI model-as-a-service offerings including Phi-3, Llama-3, Mistral, and others.
/// </summary>
public sealed class AzureAiInferenceChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LlmHttpClient _client;
    private readonly AzureAiInferenceClientOptions _options;

    public IFeatureCollection Features { get; }

    public AzureAiInferenceChatCompletionClient(
        HttpClient httpClient,
        AzureAiInferenceClientOptions options,
        ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, logger: loggerFactory?.CreateLogger<LlmHttpClient>());
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
        features.Set<IStreamingChatFeature>(this);
        Features = features;
    }

    public static AzureAiInferenceChatCompletionClient Create(
        IHttpMessageHandlerFactory handlerFactory,
        AzureAiInferenceClientOptions options,
        TokenCredential? credential = null,
        string handlerName = "cisharpai",
        ILoggerFactory? loggerFactory = null)
    {
        var http = new HttpClient(new AzureAuthenticationHandler(options, credential) { InnerHandler = handlerFactory.CreateHandler(handlerName) })
        {
            BaseAddress = new Uri(options.Endpoint),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return new AzureAiInferenceChatCompletionClient(http, options, loggerFactory);
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = await MapMessagesAsync(request.Messages, cancellationToken);

            var modelId = !string.IsNullOrWhiteSpace(request.Model)
                ? request.Model
                : _options.ModelId;

            var isReasoning = IsReasoningModel(modelId);

            object providerRequest = isReasoning
                ? new AzureAiInferenceReasoningChatRequest
                {
                    Model = modelId,
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens
                }
                : new AzureAiInferenceChatRequest
                {
                    Model = modelId,
                    Messages = messages,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens
                };

            return await ExecuteRequestAsync(providerRequest, request, cancellationToken);
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

            var adjustedMessages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(request.Messages, jsonOutputOptions);
            var messages = await MapMessagesAsync(adjustedMessages, cancellationToken);

            var modelId = !string.IsNullOrWhiteSpace(request.Model)
                ? request.Model
                : _options.ModelId;

            var isReasoning = IsReasoningModel(modelId);
            var responseFormat = BuildResponseFormat(jsonOutputOptions);

            object providerRequest = isReasoning
                ? new AzureAiInferenceReasoningChatRequest
                {
                    Model = modelId,
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens,
                    ResponseFormat = responseFormat
                }
                : new AzureAiInferenceChatRequest
                {
                    Model = modelId,
                    Messages = messages,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    ResponseFormat = responseFormat
                };

            return await ExecuteRequestAsync(providerRequest, request, cancellationToken);
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

            var messages = await MapMessagesAsync(request.Messages, cancellationToken);

            var modelId = !string.IsNullOrWhiteSpace(request.Model)
                ? request.Model
                : _options.ModelId;

            var isReasoning = IsReasoningModel(modelId);
            var tools = MapToolDefinitions(toolOptions.Tools);
            var toolChoice = MapToolChoiceValue(toolOptions.ToolChoice);

            object providerRequest = isReasoning
                ? new AzureAiInferenceReasoningChatRequest
                {
                    Model = modelId,
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens,
                    Tools = tools,
                    ToolChoice = toolChoice
                }
                : new AzureAiInferenceChatRequest
                {
                    Model = modelId,
                    Messages = messages,
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Tools = tools,
                    ToolChoice = toolChoice
                };

            return await ExecuteToolCallingRequestAsync(providerRequest, request, cancellationToken);
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
        var messages = await MapMessagesAsync(request.Messages, cancellationToken);
        var uri = $"models/chat/completions?api-version={_options.ApiVersion}";

        var modelId = !string.IsNullOrWhiteSpace(request.Model)
            ? request.Model
            : _options.ModelId;

        var isReasoning = IsReasoningModel(modelId);

        object providerRequest = isReasoning
            ? new AzureAiInferenceReasoningChatRequest
            {
                Model = modelId,
                Messages = messages,
                MaxCompletionTokens = request.MaxTokens,
                Stream = true,
                StreamOptions = new AzureAiInferenceStreamOptions { IncludeUsage = true }
            }
            : new AzureAiInferenceChatRequest
            {
                Model = modelId,
                Messages = messages,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Stream = true,
                StreamOptions = new AzureAiInferenceStreamOptions { IncludeUsage = true }
            };

        await foreach (var json in _client.PostStreamAsync<object>(uri, providerRequest, request.ExtraParameters, cancellationToken))
        {
            AzureAiInferenceStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<AzureAiInferenceStreamChunk>(json, StreamJsonOptions);
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

    private async Task<ChatCompletionResponse> ExecuteRequestAsync(
        object providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var uri = $"models/chat/completions?api-version={_options.ApiVersion}";

        string? rawResponseJson = null;
        string? rawRequestJson = null;
        AzureAiInferenceChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<
                object,
                AzureAiInferenceChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<
                object,
                AzureAiInferenceChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }

        var choice = raw.Choices.FirstOrDefault();

        return new ChatCompletionResponse(
            Content: ContentPartHelper.ExtractStringContent(choice?.Message.Content),
            Model: raw.Model,
            PromptTokens: raw.Usage?.PromptTokens ?? 0,
            CompletionTokens: raw.Usage?.CompletionTokens ?? 0,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: choice?.FinishReason,
            IncompleteReason: MapIncompleteReason(choice?.FinishReason),
            Refusal: choice?.Message.Refusal);
    }

    private async Task<ToolCallingResponse> ExecuteToolCallingRequestAsync(
        object providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var uri = $"models/chat/completions?api-version={_options.ApiVersion}";

        string? rawResponseJson = null;
        string? rawRequestJson = null;
        AzureAiInferenceChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<
                object,
                AzureAiInferenceChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<
                object,
                AzureAiInferenceChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        AzureAiInferenceChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var content = ContentPartHelper.ExtractStringContent(choice?.Message.Content);

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage?.PromptTokens ?? 0,
            CompletionTokens: raw.Usage?.CompletionTokens ?? 0,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);

        var toolCalls = ToolCallingHelper.MapResponseToolCalls(
            choice?.Message.ToolCalls,
            tc => (tc.Id, tc.Function.Name, tc.Function.Arguments));

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static List<AzureAiInferenceToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new AzureAiInferenceToolDefinition
        {
            Type = "function",
            Function = new AzureAiInferenceToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
                Strict = t.Strict
            }
        }).ToList();
    }

    private static object? MapToolChoiceValue(ToolChoice? toolChoice) =>
        ToolCallingHelper.MapToolChoice(toolChoice, name => new AzureAiInferenceToolChoiceObject
        {
            Type = "function",
            Function = new AzureAiInferenceToolChoiceFunction { Name = name }
        });

    private static AzureAiInferenceResponseFormat BuildResponseFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new AzureAiInferenceResponseFormat { Type = "json_object" };
        }

        return new AzureAiInferenceResponseFormat
        {
            Type = "json_schema",
            JsonSchema = new AzureAiInferenceJsonSchemaSpec
            {
                Name = options.SchemaName!,
                Description = options.SchemaDescription,
                Strict = options.Strict,
                Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
            }
        };
    }

    private static async Task<List<AzureAiInferenceChatMessage>> MapMessagesAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken ct)
    {
        var result = new List<AzureAiInferenceChatMessage>();

        foreach (var m in messages)
        {
            var msg = new AzureAiInferenceChatMessage { Role = RoleMapper.MapRole(m.Role) };

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

    private static Task<List<AzureAiInferenceContentPart>> MapContentPartsAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        CancellationToken ct)
    {
        return ContentPartHelper.MapOpenAiStyleContentPartsAsync(
            contentParts,
            text => new AzureAiInferenceContentPart { Type = "text", Text = text },
            url => new AzureAiInferenceContentPart { Type = "image_url", ImageUrl = new AzureAiInferenceImageUrl { Url = url } },
            ct);
    }

    private static List<AzureAiInferenceToolCall> MapToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        return toolCalls.Select(tc => new AzureAiInferenceToolCall
        {
            Id = tc.Id,
            Type = "function",
            Function = new AzureAiInferenceToolCallFunction
            {
                Name = tc.FunctionName,
                Arguments = tc.Arguments.GetRawText()
            }
        }).ToList();
    }

    private static bool IsReasoningModel(string? model) =>
        model is not null &&
        (model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
         model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
         model.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
         model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase));

    private static string? MapIncompleteReason(string? finishReason) => finishReason switch
    {
        "length" => "Token limit reached",
        "content_filter" => "Content filtered",
        _ => null
    };
}
