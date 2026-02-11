using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference.Models;

namespace Cisharpai.Azure.AzureAiInference;

/// <summary>
/// Azure AI Inference chat completion client using HttpClient.
/// Supports Azure AI model-as-a-service offerings including Phi-3, Llama-3, Mistral, and others.
/// </summary>
public sealed class AzureAiInferenceChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature
{
    private readonly LlmHttpClient _client;
    private readonly AzureAiInferenceClientOptions _options;

    public IFeatureCollection Features { get; }

    public AzureAiInferenceChatCompletionClient(
        HttpClient httpClient,
        AzureAiInferenceClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
        Features = features;
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = MapMessages(request.Messages);

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

            var adjustedMessages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOutputOptions);
            var messages = MapMessages(adjustedMessages);

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

            var messages = MapMessages(request.Messages);

            var modelId = !string.IsNullOrWhiteSpace(request.Model)
                ? request.Model
                : _options.ModelId;

            var isReasoning = IsReasoningModel(modelId);
            var tools = MapToolDefinitions(toolOptions.Tools);
            var toolChoice = MapToolChoice(toolOptions.ToolChoice);

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
                uri, providerRequest, cancellationToken, request.ExtraParameters);
        }
        else
        {
            raw = await _client.PostAsync<
                object,
                AzureAiInferenceChatResponse>(
                uri, providerRequest, cancellationToken, request.ExtraParameters);
        }

        var choice = raw.Choices.FirstOrDefault();

        return new ChatCompletionResponse(
            Content: choice?.Message.Content ?? string.Empty,
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
                uri, providerRequest, cancellationToken, request.ExtraParameters);
        }
        else
        {
            raw = await _client.PostAsync<
                object,
                AzureAiInferenceChatResponse>(
                uri, providerRequest, cancellationToken, request.ExtraParameters);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        AzureAiInferenceChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var content = choice?.Message.Content ?? string.Empty;

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage?.PromptTokens ?? 0,
            CompletionTokens: raw.Usage?.CompletionTokens ?? 0,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);

        var toolCalls = MapResponseToolCalls(choice?.Message.ToolCalls);

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static IReadOnlyList<ToolCall>? MapResponseToolCalls(List<AzureAiInferenceToolCall>? toolCalls)
    {
        if (toolCalls is null || toolCalls.Count == 0)
            return null;

        return toolCalls.Select(tc =>
        {
            JsonElement arguments;
            try
            {
                arguments = JsonDocument.Parse(tc.Function.Arguments).RootElement.Clone();
            }
            catch
            {
                // If arguments can't be parsed, wrap them as a raw string
                arguments = JsonDocument.Parse($"\"{tc.Function.Arguments}\"").RootElement.Clone();
            }

            return new ToolCall(tc.Id, tc.Function.Name, arguments);
        }).ToList();
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

    private static object? MapToolChoice(ToolChoice? toolChoice)
    {
        if (toolChoice is null)
            return null;

        if (toolChoice == ToolChoice.Auto)
            return "auto";

        if (toolChoice == ToolChoice.None)
            return "none";

        if (toolChoice == ToolChoice.Required)
            return "required";

        if (toolChoice.IsSpecific)
        {
            return new AzureAiInferenceToolChoiceObject
            {
                Type = "function",
                Function = new AzureAiInferenceToolChoiceFunction { Name = toolChoice.FunctionName! }
            };
        }

        return null;
    }

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

    private static IReadOnlyList<LlmMessage> EnsureJsonKeywordInSystemMessage(
        IReadOnlyList<LlmMessage> messages,
        JsonOutputOptions options)
    {
        if (options.Mode != JsonOutputMode.JsonMode)
            return messages;

        var systemMessage = messages.FirstOrDefault(m => m.Role == LlmRole.System);

        if (systemMessage is not null &&
            systemMessage.Content.Contains("JSON", StringComparison.OrdinalIgnoreCase))
            return messages;

        var result = new List<LlmMessage>(messages);

        if (systemMessage is not null)
        {
            var index = result.IndexOf(systemMessage);
            result[index] = new LlmMessage(LlmRole.System, systemMessage.Content + " Respond in JSON.");
        }
        else
        {
            result.Insert(0, new LlmMessage(LlmRole.System, "Respond in JSON."));
        }

        return result;
    }

    private static List<AzureAiInferenceChatMessage> MapMessages(IReadOnlyList<LlmMessage> messages)
    {
        return messages
            .Select(m =>
            {
                var msg = new AzureAiInferenceChatMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content
                };

                // Tool result message: set tool_call_id, content is the result
                if (m.Role == LlmRole.Tool && m.ToolCallId is not null)
                {
                    msg.ToolCallId = m.ToolCallId;
                }

                // Assistant message with tool calls
                if (m.Role == LlmRole.Assistant && m.ToolCalls is not null && m.ToolCalls.Count > 0)
                {
                    msg.ToolCalls = m.ToolCalls.Select(tc => new AzureAiInferenceToolCall
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

                return msg;
            })
            .ToList();
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        LlmRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

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
