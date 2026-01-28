using cisharpai.Models;
using cisharpai.OpenAi.Models;

namespace cisharpai.OpenAi;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient
{
    private readonly LlmHttpClient _client;

    public OpenAiChatCompletionClient(HttpClient httpClient)
    {
        _client = new LlmHttpClient(httpClient);
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = request.Messages
                .Select(m => new OpenAiChatMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content
                })
                .ToList()
        };

        var raw = await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
            "chat/completions",
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
