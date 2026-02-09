using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic.Models;

namespace Cisharpai.Anthropic;

public sealed class AnthropicChatCompletionClient : IChatCompletionClient, IJsonOutputFeature
{
    private readonly LlmHttpClient _client;

    public IFeatureCollection Features { get; }

    public AnthropicChatCompletionClient(HttpClient httpClient)
    {
        _client = new LlmHttpClient(httpClient);

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
            var providerRequest = BuildRequest(request);

            return await ExecuteAsync(providerRequest, request, cancellationToken);
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

            var messages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOutputOptions);
            var updatedRequest = request with { Messages = messages };

            var providerRequest = BuildRequest(updatedRequest);
            providerRequest.OutputConfig = BuildOutputConfig(jsonOutputOptions);

            var response = await ExecuteAsync(providerRequest, request, cancellationToken);

            // Anthropic has no native json_object mode. For JsonMode we rely on system
            // message injection, but Claude often wraps output in markdown code fences.
            // Strip them so callers always receive raw JSON.
            if (jsonOutputOptions.Mode == JsonOutputMode.JsonMode && response.IsSuccess)
                response = response with { Content = StripMarkdownCodeFences(response.Content) };

            return response;
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

    private AnthropicChatRequest BuildRequest(ChatCompletionRequest request)
    {
        var systemMessage = request.Messages
            .FirstOrDefault(m => m.Role == LlmRole.System)?.Content;

        return new AnthropicChatRequest
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
    }

    private async Task<ChatCompletionResponse> ExecuteAsync(
        AnthropicChatRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
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

        var refusal = raw.StopReason == "refusal" ? content : null;
        if (refusal is not null)
            content = string.Empty;

        return new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Refusal: refusal);
    }

    private static AnthropicOutputConfig? BuildOutputConfig(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
            return null;

        return new AnthropicOutputConfig
        {
            Format = new AnthropicOutputFormat
            {
                Type = "json_schema",
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
            result[index] = new LlmMessage(LlmRole.System, systemMessage.Content + " Respond with raw JSON only, no markdown formatting.");
        }
        else
        {
            result.Insert(0, new LlmMessage(LlmRole.System, "Respond with raw JSON only, no markdown formatting."));
        }

        return result;
    }

    internal static string StripMarkdownCodeFences(string content)
    {
        var trimmed = content.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
            return content;

        // Remove opening fence (```json, ```JSON, or just ```)
        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return content;

        trimmed = trimmed[(firstNewline + 1)..];

        // Remove closing fence
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0)
            trimmed = trimmed[..lastFence];

        return trimmed.Trim();
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
