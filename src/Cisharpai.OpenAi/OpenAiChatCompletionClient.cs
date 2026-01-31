using Cisharpai.Models;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.OpenAi;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient
{
    private const string ChatCompletionsEndpoint = "chat/completions";
    private const string ResponsesEndpoint = "responses";

    private readonly LlmHttpClient _client;
    private readonly OpenAiClientOptions _options;

    public OpenAiChatCompletionClient(HttpClient httpClient, OpenAiClientOptions options)
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
            var modelType = DetectModelType(request.Model);

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

    private async Task<ChatCompletionResponse> SendLegacyChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = MapMessages(request.Messages)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, cancellationToken, request.ExtraParameters));
    }

    private async Task<ChatCompletionResponse> SendReasoningChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model,
            MaxCompletionTokens = request.MaxTokens,
            Messages = MapMessages(request.Messages)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, cancellationToken, request.ExtraParameters));
    }

    private async Task<ChatCompletionResponse> SendResponsesApiAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model,
            MaxOutputTokens = request.MaxTokens,
            Input = MapMessages(request.Messages),
            Reasoning = _options.ReasoningEffort is not null
                ? new OpenAiReasoningOption { Effort = _options.ReasoningEffort }
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
                ResponsesEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                ResponsesEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
        }

        var content = raw.Output
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "output_text")
            .Select(c => c.Text)
            .FirstOrDefault() ?? string.Empty;

        var isError = raw.Status is "incomplete" or "failed";
        var errorMessage = isError
            ? (raw.IncompleteDetails?.Reason ?? $"Response status: {raw.Status}")
            : null;

        return new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: raw.Status,
            IncompleteReason: raw.IncompleteDetails?.Reason,
            IsSuccess: !isError,
            ErrorMessage: errorMessage);
    }

    private static ChatCompletionResponse MapChatResponse(
        OpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        return new ChatCompletionResponse(
            Content: raw.Choices.FirstOrDefault()?.Message.Content ?? string.Empty,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }

    private static List<OpenAiChatMessage> MapMessages(IReadOnlyList<LlmMessage> messages)
    {
        return messages
            .Select(m => new OpenAiChatMessage
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

    internal enum OpenAiModelType
    {
        Legacy,
        Reasoning,
        Gpt5
    }
}
