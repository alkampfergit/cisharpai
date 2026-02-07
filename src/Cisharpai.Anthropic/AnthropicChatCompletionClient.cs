using Cisharpai.Features;
using Cisharpai.Models;
using Cisharpai.Anthropic.Models;

namespace Cisharpai.Anthropic;

public sealed class AnthropicChatCompletionClient : IChatCompletionClient
{
    private readonly LlmHttpClient _client;

    public IFeatureCollection Features { get; } = new FeatureCollection();

    public AnthropicChatCompletionClient(HttpClient httpClient)
    {
        _client = new LlmHttpClient(httpClient);
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var systemMessage = request.Messages
                .FirstOrDefault(m => m.Role == LlmRole.System)?.Content;

            var providerRequest = new AnthropicChatRequest
            {
                Model = request.Model,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens ?? 1024,
                System = systemMessage,
                Messages = request.Messages
                    .Where(m => m.Role != LlmRole.System)
                    .Select(m => new AnthropicMessage
                    {
                        Role = MapRole(m.Role),
                        Content = m.Content
                    })
                    .ToList()
            };

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            AnthropicChatResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<AnthropicChatRequest, AnthropicChatResponse>(
                    "messages", providerRequest, cancellationToken, request.ExtraParameters);
            }
            else
            {
                raw = await _client.PostAsync<AnthropicChatRequest, AnthropicChatResponse>(
                    "messages", providerRequest, cancellationToken, request.ExtraParameters);
            }

            var content = string.Join("", raw.Content
                .Where(c => c.Type == "text")
                .Select(c => c.Text));

            return new ChatCompletionResponse(
                Content: content,
                Model: raw.Model,
                PromptTokens: raw.Usage.InputTokens,
                CompletionTokens: raw.Usage.OutputTokens,
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
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
