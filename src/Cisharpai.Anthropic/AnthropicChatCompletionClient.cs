using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Anthropic.Models;

namespace Cisharpai.Anthropic;

public sealed class AnthropicChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature
{
    private readonly LlmHttpClient _client;
    private readonly AnthropicClientOptions _options;

    public IFeatureCollection Features { get; }

    public AnthropicChatCompletionClient(HttpClient httpClient, AnthropicClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
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

            return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
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

    private string ResolveModel(string? model)
    {
        return model
            ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "Model must be specified either in the request or via DefaultModel in options.");
    }

    /// <summary>
    /// Anthropic API requires max_tokens. When the caller does not specify a
    /// value we fall back to this default so the request does not fail.
    /// </summary>
    public const int DefaultMaxTokens = 8192;

    private AnthropicChatRequest BuildRequest(ChatCompletionRequest request)
    {
        var systemMessage = request.Messages
            .FirstOrDefault(m => m.Role == LlmRole.System)?.Content;

        return new AnthropicChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens ?? DefaultMaxTokens,
            System = systemMessage,
            Messages = MapMessages(request.Messages)
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

        var incompleteReason = raw.StopReason == "max_tokens" ? "max_tokens" : null;

        return new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: raw.StopReason,
            IncompleteReason: incompleteReason,
            Refusal: refusal);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        AnthropicChatResponse raw,
        string? rawResponseJson,
        string? rawRequestJson)
    {
        var content = string.Join("", raw.Content
            .Where(c => c.Type == "text")
            .Select(c => c.Text));

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.InputTokens,
            CompletionTokens: raw.Usage.OutputTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Status: raw.StopReason);

        var toolCalls = MapResponseToolCalls(raw.Content);

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static IReadOnlyList<ToolCall>? MapResponseToolCalls(List<AnthropicContentBlock> contentBlocks)
    {
        var toolUseBlocks = contentBlocks
            .Where(c => c.Type == "tool_use" && c.Id is not null && c.Name is not null && c.Input.HasValue)
            .ToList();

        if (toolUseBlocks.Count == 0)
            return null;

        return toolUseBlocks.Select(b =>
            new ToolCall(b.Id!, b.Name!, b.Input!.Value.Clone())
        ).ToList();
    }

    private static List<AnthropicToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new AnthropicToolDefinition
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = t.Parameters
        }).ToList();
    }

    private static AnthropicToolChoice? MapToolChoice(ToolChoice? toolChoice)
    {
        if (toolChoice is null)
            return null;

        if (toolChoice == ToolChoice.Auto)
            return new AnthropicToolChoice { Type = "auto" };

        if (toolChoice == ToolChoice.None)
            // Anthropic doesn't have a "none" type for tool_choice.
            // Omitting tool_choice with no tools would achieve this, but since
            // we're sending tools, use auto and let the model decide.
            return null;

        if (toolChoice == ToolChoice.Required)
            return new AnthropicToolChoice { Type = "any" };

        if (toolChoice.IsSpecific)
            return new AnthropicToolChoice { Type = "tool", Name = toolChoice.FunctionName };

        return null;
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

    private static List<AnthropicMessage> MapMessages(IReadOnlyList<LlmMessage> messages)
    {
        var result = new List<AnthropicMessage>();

        foreach (var m in messages)
        {
            if (m.Role == LlmRole.System)
                continue; // System messages are handled via the top-level system field

            // Tool result messages: convert to user role with tool_result content blocks
            if (m.Role == LlmRole.Tool && m.ToolCallId is not null)
            {
                result.Add(new AnthropicMessage
                {
                    Role = "user",
                    Content = new List<AnthropicContentBlock>
                    {
                        new()
                        {
                            Type = "tool_result",
                            ToolUseId = m.ToolCallId,
                            Content = m.Content
                        }
                    }
                });
                continue;
            }

            // Assistant message with tool calls: serialize as tool_use content blocks
            if (m.Role == LlmRole.Assistant && m.ToolCalls is not null && m.ToolCalls.Count > 0)
            {
                var blocks = new List<AnthropicContentBlock>();

                // If there's text content, add it as a text block
                if (!string.IsNullOrEmpty(m.Content))
                {
                    blocks.Add(new AnthropicContentBlock { Type = "text", Text = m.Content });
                }

                // Add tool_use blocks
                foreach (var tc in m.ToolCalls)
                {
                    blocks.Add(new AnthropicContentBlock
                    {
                        Type = "tool_use",
                        Id = tc.Id,
                        Name = tc.FunctionName,
                        Input = tc.Arguments
                    });
                }

                result.Add(new AnthropicMessage
                {
                    Role = "assistant",
                    Content = blocks
                });
                continue;
            }

            // Normal messages
            result.Add(new AnthropicMessage
            {
                Role = MapRole(m.Role),
                Content = m.Content
            });
        }

        return result;
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
