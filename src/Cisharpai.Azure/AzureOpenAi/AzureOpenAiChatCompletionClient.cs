using Cisharpai.Features;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi.Models;

namespace Cisharpai.Azure.AzureOpenAi;

/// <summary>
/// Azure OpenAI chat completion client using HttpClient.
/// Supports both legacy models (GPT-4) and reasoning models (o1/o3/o4/GPT-5).
/// </summary>
public sealed class AzureOpenAiChatCompletionClient : IChatCompletionClient
{
    private readonly LlmHttpClient _client;
    private readonly AzureOpenAiClientOptions _options;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public AzureOpenAiChatCompletionClient(
        HttpClient httpClient,
        AzureOpenAiClientOptions options)
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
                .Select(m => new AzureOpenAiChatMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content
                })
                .ToList();

            var isReasoning = IsReasoningModel(request.Model);

            object providerRequest = isReasoning
                ? new AzureOpenAiReasoningChatRequest
                {
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens
                }
                : new AzureOpenAiChatRequest
                {
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Messages = messages
                };

            var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            AzureOpenAiChatResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<object, AzureOpenAiChatResponse>(
                    uri, providerRequest, cancellationToken, request.ExtraParameters);
            }
            else
            {
                raw = await _client.PostAsync<object, AzureOpenAiChatResponse>(
                    uri, providerRequest, cancellationToken, request.ExtraParameters);
            }

            return new ChatCompletionResponse(
                Content: raw.Choices.FirstOrDefault()?.Message.Content ?? string.Empty,
                Model: raw.Model,
                PromptTokens: raw.Usage.PromptTokens,
                CompletionTokens: raw.Usage.CompletionTokens,
                RawResponseJson: rawResponseJson,
                RawRequestJson: rawRequestJson);
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
}
