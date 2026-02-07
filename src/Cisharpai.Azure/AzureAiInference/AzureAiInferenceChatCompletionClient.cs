using Cisharpai.Features;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference.Models;

namespace Cisharpai.Azure.AzureAiInference;

/// <summary>
/// Azure AI Inference chat completion client using HttpClient.
/// Supports Azure AI model-as-a-service offerings including Phi-3, Llama-3, Mistral, and others.
/// </summary>
public sealed class AzureAiInferenceChatCompletionClient : IChatCompletionClient
{
    private readonly LlmHttpClient _client;
    private readonly AzureAiInferenceClientOptions _options;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public AzureAiInferenceChatCompletionClient(
        HttpClient httpClient,
        AzureAiInferenceClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var messages = request.Messages
                .Select(m => new AzureAiInferenceChatMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content
                })
                .ToList();

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

            // Azure AI Inference endpoint format:
            // POST /models/chat/completions?api-version=2024-05-01-preview
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
                IncompleteReason: MapIncompleteReason(choice?.FinishReason));
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
