using Cisharpai.Models;
using Cisharpai.AzureOpenAi.Models;

namespace Cisharpai.AzureOpenAi;

public sealed class AzureOpenAiChatCompletionClient : IChatCompletionClient
{
    private readonly LlmHttpClient _client;
    private readonly AzureOpenAiClientOptions _options;

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
        var providerRequest = new AzureOpenAiChatRequest
        {
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = request.Messages
                .Select(m => new AzureOpenAiChatMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content
                })
                .ToList()
        };

        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        var raw = await _client.PostAsync<AzureOpenAiChatRequest, AzureOpenAiChatResponse>(
            uri,
            providerRequest,
            cancellationToken);

        return new ChatCompletionResponse(
            Content: raw.Choices.FirstOrDefault()?.Message.Content ?? string.Empty,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens);
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
