using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere.Models;

namespace Cisharpai.Cohere;

public sealed class CohereChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IGroundedChatFeature, IToolCallingFeature, IStreamingChatFeature
{
    private const string ChatEndpoint = "chat";
    private readonly LlmHttpClient _client;
    private readonly CohereClientOptions _options;

    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IFeatureCollection Features { get; }

    public CohereChatCompletionClient(HttpClient httpClient, CohereClientOptions options)
    {
        _client = new LlmHttpClient(httpClient, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        });
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IGroundedChatFeature>(this);
        features.Set<IToolCallingFeature>(this);
        features.Set<IStreamingChatFeature>(this);
        Features = features;
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

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
        request = request with { Model = ResolveModel(request.Model) };

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

    public async Task<GroundedChatCompletionResponse> GetGroundedChatCompletionAsync(
        ChatCompletionRequest request,
        GroundedChatOptions groundedChatOptions,
        CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

        try
        {
            groundedChatOptions.Validate();
            foreach (var doc in groundedChatOptions.Documents)
                doc.Validate();

            var providerRequest = BuildRequest(request);
            providerRequest.Documents = MapDocuments(groundedChatOptions.Documents);
            providerRequest.CitationOptions = new CohereCitationOptions
            {
                Mode = MapCitationMode(groundedChatOptions.CitationMode)
            };

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

            var chatCompletion = new ChatCompletionResponse(
                Content: content,
                Model: request.Model!,
                PromptTokens: raw.Usage.Tokens.InputTokens,
                CompletionTokens: raw.Usage.Tokens.OutputTokens,
                RawResponseJson: rawResponseJson,
                RawRequestJson: rawRequestJson);

            var citations = MapCitations(raw.Message.Citations);

            return new GroundedChatCompletionResponse(chatCompletion, citations);
        }
        catch (LlmHttpRequestException ex)
        {
            return GroundedChatCompletionResponse.Error(ex.Message, ex.ResponseBody);
        }
        catch (Exception ex)
        {
            return GroundedChatCompletionResponse.Error(ex.Message);
        }
    }

    public async Task<ToolCallingResponse> GetChatCompletionWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

        try
        {
            toolOptions.Validate();

            var providerRequest = BuildRequest(request);
            providerRequest.Tools = MapToolDefinitions(toolOptions.Tools);
            providerRequest.ToolChoice = MapToolChoice(toolOptions.ToolChoice);
            providerRequest.StrictTools = toolOptions.Tools.All(t => t.Strict) ? true : null;

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

            return MapToolCallingResponse(raw, request.Model!, rawResponseJson, rawRequestJson);
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

    public async IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

        var providerRequest = BuildRequest(request);
        providerRequest.Stream = true;

        string? model = null;
        int? inputTokens = null;

        await foreach (var json in _client.PostStreamAsync(ChatEndpoint, providerRequest, cancellationToken, request.ExtraParameters))
        {
            CohereStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<CohereStreamEvent>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "stream-start":
                    // No useful metadata in Cohere stream-start
                    model = request.Model;
                    break;

                case "content-delta":
                    var text = evt.Delta?.Message?.Content?.Text;
                    if (text is not null)
                    {
                        yield return new ChatCompletionChunk(
                            Content: text,
                            Model: model);
                    }
                    break;

                case "message-end":
                    var finishReason = evt.Delta?.FinishReason;
                    var outputTokens = evt.Delta?.Usage?.BilledUnits?.OutputTokens;
                    inputTokens = evt.Delta?.Usage?.BilledUnits?.InputTokens;

                    yield return new ChatCompletionChunk(
                        Content: string.Empty,
                        FinishReason: finishReason,
                        Model: model,
                        PromptTokens: inputTokens,
                        CompletionTokens: outputTokens);
                    break;
            }
        }
    }

    private string ResolveModel(string? model)
    {
        return model
            ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "Model must be specified either in the request or via DefaultModel in options.");
    }

    private static CohereChatRequest BuildRequest(ChatCompletionRequest request)
    {
        return new CohereChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = MapMessages(request.Messages)
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
            Model: request.Model!,
            PromptTokens: raw.Usage.Tokens.InputTokens,
            CompletionTokens: raw.Usage.Tokens.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        CohereChatResponse raw,
        string model,
        string? rawResponseJson,
        string? rawRequestJson)
    {
        var content = string.Join("", raw.Message.Content
            .Where(c => c.Type == "text")
            .Select(c => c.Text));

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: model,
            PromptTokens: raw.Usage.Tokens.InputTokens,
            CompletionTokens: raw.Usage.Tokens.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);

        var toolCalls = MapResponseToolCalls(raw.Message.ToolCalls);

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static List<ToolCall>? MapResponseToolCalls(List<CohereToolCall>? toolCalls)
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
                arguments = JsonDocument.Parse($"\"{tc.Function.Arguments}\"").RootElement.Clone();
            }

            return new ToolCall(tc.Id, tc.Function.Name, arguments);
        }).ToList();
    }

    private static List<CohereToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new CohereToolDefinition
        {
            Type = "function",
            Function = new CohereToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters
            }
        }).ToList();
    }

    private static string? MapToolChoice(ToolChoice? toolChoice)
    {
        if (toolChoice is null)
            return null;

        if (toolChoice == ToolChoice.Auto)
            return "AUTO";

        if (toolChoice == ToolChoice.None)
            return "NONE";

        if (toolChoice == ToolChoice.Required)
            return "REQUIRED";

        // Cohere doesn't support specific tool choice; degrade to REQUIRED
        if (toolChoice.IsSpecific)
            return "REQUIRED";

        return null;
    }

    private static List<CohereChatMessage> MapMessages(IReadOnlyList<LlmMessage> messages)
    {
        return messages.Select(m =>
        {
            var msg = new CohereChatMessage
            {
                Role = MapRole(m.Role),
                Content = ExtractTextContent(m)
            };

            // Tool result message: set tool_call_id
            if (m.Role == LlmRole.Tool && m.ToolCallId is not null)
            {
                msg.ToolCallId = m.ToolCallId;
            }

            // Assistant message with tool calls
            if (m.Role == LlmRole.Assistant && m.ToolCalls is not null && m.ToolCalls.Count > 0)
            {
                msg.ToolCalls = m.ToolCalls.Select(tc => new CohereToolCall
                {
                    Id = tc.Id,
                    Type = "function",
                    Function = new CohereToolCallFunction
                    {
                        Name = tc.FunctionName,
                        Arguments = tc.Arguments.GetRawText()
                    }
                }).ToList();
            }

            return msg;
        }).ToList();
    }

    /// <summary>
    /// Extracts text content from an LlmMessage. For messages with ContentParts,
    /// only text parts are used (image parts are silently skipped as Cohere chat
    /// does not support vision). If ContentParts contains only images with no text,
    /// returns an empty string rather than throwing.
    /// </summary>
    private static string? ExtractTextContent(LlmMessage message)
    {
        if (message.ContentParts is { Count: > 0 })
        {
            var textParts = message.ContentParts.OfType<TextContentPart>().ToList();
            if (textParts.Count == 0)
                return string.Empty;
            return string.Join("", textParts.Select(t => t.Text));
        }

        return message.Content;
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

    private static List<CohereChatDocument> MapDocuments(IReadOnlyList<DocumentChunk> documents)
    {
        return documents.Select(doc =>
        {
            var cohereDoc = new CohereChatDocument { Id = doc.Id };

            if (doc.Data is not null)
                cohereDoc.Data = JsonSerializer.SerializeToElement(doc.Data);
            else
                cohereDoc.Data = JsonSerializer.SerializeToElement(doc.Text);

            return cohereDoc;
        }).ToList();
    }

    private static List<Citation> MapCitations(List<CohereChatCitation>? citations)
    {
        if (citations is null || citations.Count == 0)
            return [];

        return citations.Select(c => new Citation(
            Start: c.Start,
            End: c.End,
            Text: c.Text,
            Sources: c.Sources.Select(s => new CitationSource(
                Id: s.Id,
                Data: s.Document is not null
                    ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(s.Document)
                    : null
            )).ToList(),
            Type: c.Type
        )).ToList();
    }

    private static string MapCitationMode(CitationMode mode) => mode switch
    {
        CitationMode.Accurate => "ACCURATE",
        CitationMode.Fast => "FAST",
        CitationMode.Enabled => "ENABLED",
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
    };

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        LlmRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
