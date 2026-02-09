using System.Text.Json;
using System.Text.Json.Serialization;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere.Models;

namespace Cisharpai.Cohere;

public sealed class CohereChatCompletionClient : IChatCompletionClient, IJsonOutputFeature
{
    private const string ChatEndpoint = "chat";
    private readonly LlmHttpClient _client;

    public IFeatureCollection Features { get; }

    public CohereChatCompletionClient(HttpClient httpClient)
    {
        _client = new LlmHttpClient(httpClient, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });

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
            providerRequest.ResponseFormat = BuildResponseFormat(jsonOutputOptions);

            var response = await ExecuteAsync(providerRequest, request, cancellationToken);

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

    private static CohereChatRequest BuildRequest(ChatCompletionRequest request)
    {
        return new CohereChatRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = request.Messages
                .Select(m => new CohereChatMessage
                {
                    Role = MapRole(m.Role),
                    Content = m.Content
                })
                .ToList()
        };
    }

    private async Task<ChatCompletionResponse> ExecuteAsync(
        CohereChatRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        string? rawResponseJson = null;
        string? rawRequestJson = null;
        CohereChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<CohereChatRequest, CohereChatResponse>(
                ChatEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
        }
        else
        {
            raw = await _client.PostAsync<CohereChatRequest, CohereChatResponse>(
                ChatEndpoint, providerRequest, cancellationToken, request.ExtraParameters);
        }

        var content = string.Join("", raw.Message.Content
            .Where(c => c.Type == "text")
            .Select(c => c.Text));

        return new ChatCompletionResponse(
            Content: content,
            Model: request.Model,
            PromptTokens: raw.Usage.Tokens.InputTokens,
            CompletionTokens: raw.Usage.Tokens.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }

    private static CohereChatResponseFormat? BuildResponseFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new CohereChatResponseFormat { Type = "json_object" };
        }

        return new CohereChatResponseFormat
        {
            Type = "json_object",
            JsonSchema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
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

        var firstNewline = trimmed.IndexOf('\n');
        if (firstNewline < 0)
            return content;

        trimmed = trimmed[(firstNewline + 1)..];

        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence >= 0)
            trimmed = trimmed[..lastFence];

        return trimmed.Trim();
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
