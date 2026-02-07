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
public sealed class AzureAiInferenceChatCompletionClient : IChatCompletionClient, IJsonOutputFeature
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
            return ChatCompletionResponse.Error($"Unexpected error: {ex.Message}");
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
            return ChatCompletionResponse.Error($"Unexpected error: {ex.Message}");
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
            .Select(m => new AzureAiInferenceChatMessage
            {
                Role = MapRole(m.Role),
                Content = m.Content
            })
            .ToList();
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private static bool IsReasoningModel(string model) =>
        model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
        model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);

    private static string? MapIncompleteReason(string? finishReason) => finishReason switch
    {
        "length" => "Token limit reached",
        "content_filter" => "Content filtered",
        _ => null
    };
}
