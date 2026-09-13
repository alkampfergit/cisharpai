using System.Runtime.CompilerServices;
using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Anthropic.Models;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Anthropic;

public sealed class AnthropicChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature, IGroundedChatFeature, IPromptCachingFeature
{
    private const string MessagesEndpoint = "messages";

    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LlmHttpClient _client;
    private readonly AnthropicClientOptions _options;
    private readonly ILogger<AnthropicChatCompletionClient>? _logger;

    public IFeatureCollection Features { get; }

    public AnthropicChatCompletionClient(HttpClient httpClient, AnthropicClientOptions options, ILoggerFactory? loggerFactory = null)
    {
        _client = new LlmHttpClient(httpClient, logger: loggerFactory?.CreateLogger<LlmHttpClient>());
        _options = options;
        _logger = loggerFactory?.CreateLogger<AnthropicChatCompletionClient>();

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
        features.Set<IStreamingChatFeature>(this);
        features.Set<IGroundedChatFeature>(this);
        features.Set<IPromptCachingFeature>(this);
        Features = features;
    }

    public static AnthropicChatCompletionClient Create(
        IHttpMessageHandlerFactory handlerFactory,
        AnthropicClientOptions options,
        string handlerName = "cisharpai",
        ILoggerFactory? loggerFactory = null)
    {
        var http = new HttpClient(new AnthropicAuthenticationHandler(options) { InnerHandler = handlerFactory.CreateHandler(handlerName) })
        {
            BaseAddress = new Uri(options.BaseUrl),
            Timeout = TimeSpan.FromMinutes(2)
        };
        return new AnthropicChatCompletionClient(http, options, loggerFactory);
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

        try
        {
            var providerRequest = await BuildRequestAsync(request, cancellationToken);

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

            var messages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(
                request.Messages, jsonOutputOptions, "Respond with raw JSON only, no markdown formatting.");
            var updatedRequest = request with { Messages = messages };

            var providerRequest = await BuildRequestAsync(updatedRequest, cancellationToken);
            providerRequest.OutputConfig = BuildOutputConfig(jsonOutputOptions);

            var response = await ExecuteAsync(providerRequest, request, cancellationToken);

            // Anthropic has no native json_object mode. For JsonMode we rely on system
            // message injection, but Claude often wraps output in markdown code fences.
            // Strip them so callers always receive raw JSON.
            if (jsonOutputOptions.Mode == JsonOutputMode.JsonMode && response.IsSuccess)
                response = response with { Content = JsonOutputHelper.StripMarkdownCodeFences(response.Content) };

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

            var providerRequest = await BuildRequestAsync(request, cancellationToken);
            providerRequest.Tools = MapToolDefinitions(toolOptions.Tools);
            providerRequest.ToolChoice = MapToolChoice(toolOptions.ToolChoice);

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            AnthropicChatResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<AnthropicChatRequest, AnthropicChatResponse>(
                    MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            }
            else
            {
                raw = await _client.PostAsync<AnthropicChatRequest, AnthropicChatResponse>(
                    MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
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

    public async IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

        var providerRequest = await BuildRequestAsync(request, cancellationToken);
        providerRequest.Stream = true;

        string? model = null;
        int? inputTokens = null;
        int? cacheReadInputTokens = null;
        int? cacheCreationInputTokens = null;

        await foreach (var json in _client.PostStreamAsync(MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken))
        {
            AnthropicStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<AnthropicStreamEvent>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "message_start":
                    model = evt.Message?.Model;
                    inputTokens = evt.Message?.Usage?.InputTokens;
                    cacheReadInputTokens = evt.Message?.Usage?.CacheReadInputTokens;
                    cacheCreationInputTokens = evt.Message?.Usage?.CacheCreationInputTokens;
                    break;

                case "content_block_delta":
                    if (evt.Delta?.Type == "text_delta")
                    {
                        yield return new ChatCompletionChunk(
                            Content: evt.Delta.Text ?? string.Empty,
                            Model: model);
                    }
                    break;

                case "message_delta":
                    yield return new ChatCompletionChunk(
                        Content: string.Empty,
                        FinishReason: evt.Delta?.StopReason,
                        Model: model,
                        PromptTokens: inputTokens,
                        CompletionTokens: evt.Usage?.OutputTokens,
                        CachedInputTokens: cacheReadInputTokens,
                        CacheCreationInputTokens: cacheCreationInputTokens);
                    break;
            }
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

            if (groundedChatOptions.CitationMode is CitationMode.Accurate or CitationMode.Fast
                && groundedChatOptions.CitationMode != CitationMode.Enabled)
            {
                _logger?.LogWarning(
                    "Anthropic does not distinguish citation modes; CitationMode.{Mode} is treated as Enabled.",
                    groundedChatOptions.CitationMode);
            }

            var providerRequest = await BuildRequestAsync(request, cancellationToken);
            InjectDocumentBlocks(providerRequest, groundedChatOptions.Documents);

            string? rawResponseJson = null;
            string? rawRequestJson = null;
            AnthropicChatResponse raw;

            if (request.IncludeRawResponse)
            {
                (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<AnthropicChatRequest, AnthropicChatResponse>(
                    MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            }
            else
            {
                raw = await _client.PostAsync<AnthropicChatRequest, AnthropicChatResponse>(
                    MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            }

            var (content, citations) = ExtractContentAndCitations(raw.Content, groundedChatOptions.Documents);

            var refusal = raw.StopReason == "refusal" ? content : null;
            if (refusal is not null)
                content = string.Empty;

            var incompleteReason = raw.StopReason == "max_tokens" ? "max_tokens" : null;

            var chatCompletion = new ChatCompletionResponse(
                Content: content,
                Model: raw.Model,
                PromptTokens: raw.Usage.InputTokens,
                CompletionTokens: raw.Usage.OutputTokens,
                RawResponseJson: rawResponseJson,
                RawRequestJson: rawRequestJson,
                Status: raw.StopReason,
                IncompleteReason: incompleteReason,
                Refusal: refusal,
                CachedInputTokens: raw.Usage.CacheReadInputTokens,
                CacheCreationInputTokens: raw.Usage.CacheCreationInputTokens);

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

    public async Task<ChatCompletionResponse> GetChatCompletionWithCachingAsync(
        ChatCompletionRequest request,
        PromptCachingOptions cachingOptions,
        CancellationToken cancellationToken = default)
    {
        request = request with { Model = ResolveModel(request.Model) };

        try
        {
            var providerRequest = await BuildRequestAsync(request, cancellationToken);
            ApplyCacheBreakpoints(providerRequest, cachingOptions);

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

    private static void ApplyCacheBreakpoints(
        AnthropicChatRequest providerRequest,
        PromptCachingOptions cachingOptions)
    {
        if (cachingOptions.CacheSystemMessage && providerRequest.System is string systemText)
        {
            providerRequest.System = new List<AnthropicSystemBlock>
            {
                new()
                {
                    Type = "text",
                    Text = systemText,
                    CacheControl = new AnthropicCacheControl { Type = "ephemeral" }
                }
            };
        }

        foreach (var index in cachingOptions.MessageBreakpoints)
        {
            if (index < 0 || index >= providerRequest.Messages.Count)
                continue;

            var msg = providerRequest.Messages[index];
            if (msg.Content is string text)
            {
                msg.Content = new List<AnthropicContentBlock>
                {
                    new()
                    {
                        Type = "text",
                        Text = text,
                        CacheControl = new AnthropicCacheControl { Type = "ephemeral" }
                    }
                };
            }
            else if (msg.Content is List<AnthropicContentBlock> blocks && blocks.Count > 0)
            {
                blocks[^1].CacheControl = new AnthropicCacheControl { Type = "ephemeral" };
            }
            else if (msg.Content is IList<object> mixedBlocks && mixedBlocks.Count > 0)
            {
                if (mixedBlocks[^1] is AnthropicContentBlock lastBlock)
                    lastBlock.CacheControl = new AnthropicCacheControl { Type = "ephemeral" };
            }
        }

        if (providerRequest.Tools is not null)
        {
            foreach (var index in cachingOptions.ToolBreakpoints)
            {
                if (index < 0 || index >= providerRequest.Tools.Count)
                    continue;

                providerRequest.Tools[index].CacheControl = new AnthropicCacheControl { Type = "ephemeral" };
            }
        }
    }

    private static void InjectDocumentBlocks(
        AnthropicChatRequest providerRequest,
        IReadOnlyList<DocumentChunk> documents)
    {
        var lastUserMessage = providerRequest.Messages.LastOrDefault(m => m.Role == "user");
        if (lastUserMessage is null)
            return;

        var documentBlocks = documents.Select(MapDocumentChunkToBlock).ToList<object>();

        if (lastUserMessage.Content is List<AnthropicContentBlock> existingBlocks)
        {
            var mixed = new List<object>();
            mixed.AddRange(documentBlocks);
            mixed.AddRange(existingBlocks);
            lastUserMessage.Content = mixed;
        }
        else if (lastUserMessage.Content is string textContent)
        {
            var mixed = new List<object>();
            mixed.AddRange(documentBlocks);
            mixed.Add(new AnthropicContentBlock { Type = "text", Text = textContent });
            lastUserMessage.Content = mixed;
        }
    }

    private static AnthropicDocumentBlock MapDocumentChunkToBlock(DocumentChunk chunk)
    {
        AnthropicDocumentSource source;

        if (chunk.Data is not null)
        {
            source = new AnthropicDocumentSource
            {
                Type = "custom_content",
                Content = chunk.Data.Select(kvp => new AnthropicCustomContentBlock
                {
                    Type = "text",
                    Text = $"{kvp.Key}: {kvp.Value}"
                }).ToList()
            };
        }
        else
        {
            source = new AnthropicDocumentSource
            {
                Type = "text",
                MediaType = "text/plain",
                Data = chunk.Text!
            };
        }

        return new AnthropicDocumentBlock
        {
            Source = source,
            Title = chunk.Id,
            Citations = new AnthropicCitationConfig { Enabled = true }
        };
    }

    private static (string content, List<Citation> citations) ExtractContentAndCitations(
        List<AnthropicContentBlock> contentBlocks,
        IReadOnlyList<DocumentChunk> documents)
    {
        var textBuilder = new System.Text.StringBuilder();
        var citations = new List<Citation>();

        foreach (var block in contentBlocks.Where(b => b.Type == "text"))
        {
            var blockStart = textBuilder.Length;
            var blockText = block.Text ?? string.Empty;
            textBuilder.Append(blockText);

            if (block.Citations is null || block.Citations.Count == 0)
                continue;

            foreach (var cite in block.Citations)
            {
                var responseStart = blockStart;
                var responseEnd = blockStart + blockText.Length;

                string? sourceId = cite.DocumentTitle;
                IReadOnlyDictionary<string, string>? sourceData = null;

                if (cite.DocumentIndex is not null && cite.DocumentIndex.Value < documents.Count)
                {
                    var doc = documents[cite.DocumentIndex.Value];
                    sourceId ??= doc.Id;
                    sourceData = doc.Data is not null
                        ? new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(
                            new Dictionary<string, string>(doc.Data))
                        : null;
                }

                var citationSource = new CitationSource(
                    Id: sourceId ?? $"doc-{cite.DocumentIndex}",
                    Data: sourceData,
                    CitedText: cite.CitedText);

                citations.Add(new Citation(
                    Start: responseStart,
                    End: responseEnd,
                    Text: blockText,
                    Sources: [citationSource],
                    Type: cite.Type));
            }
        }

        return (textBuilder.ToString(), citations);
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

    private static async Task<AnthropicChatRequest> BuildRequestAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var systemMessage = request.Messages
            .FirstOrDefault(m => m.Role == LlmRole.System)?.Content;

        return new AnthropicChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens ?? DefaultMaxTokens,
            System = systemMessage,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken)
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
                MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<AnthropicChatRequest, AnthropicChatResponse>(
                MessagesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
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
            Refusal: refusal,
            CachedInputTokens: raw.Usage.CacheReadInputTokens,
            CacheCreationInputTokens: raw.Usage.CacheCreationInputTokens);
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
            Status: raw.StopReason,
            CachedInputTokens: raw.Usage.CacheReadInputTokens,
            CacheCreationInputTokens: raw.Usage.CacheCreationInputTokens);

        var toolCalls = MapResponseToolCalls(raw.Content);

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static List<ToolCall>? MapResponseToolCalls(List<AnthropicContentBlock> contentBlocks)
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

    private static async Task<List<AnthropicMessage>> MapMessagesAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken ct)
    {
        var result = new List<AnthropicMessage>();

        foreach (var m in messages)
        {
            if (m.Role == LlmRole.System)
                continue; // System messages are handled via the top-level system field

            if (m.Role == LlmRole.Tool && m.ToolCallId is not null)
            {
                result.Add(MapToolResultMessage(m));
                continue;
            }

            if (m.Role == LlmRole.Assistant && m.ToolCalls is { Count: > 0 })
            {
                result.Add(MapAssistantToolCallMessage(m));
                continue;
            }

            if (m.ContentParts is { Count: > 0 })
            {
                result.Add(new AnthropicMessage
                {
                    Role = MapRole(m.Role),
                    Content = await MapContentPartsAsync(m.ContentParts, ct)
                });
                continue;
            }

            result.Add(new AnthropicMessage
            {
                Role = MapRole(m.Role),
                Content = m.Content
            });
        }

        return result;
    }

    private static AnthropicMessage MapToolResultMessage(LlmMessage m)
    {
        return new AnthropicMessage
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
        };
    }

    private static AnthropicMessage MapAssistantToolCallMessage(LlmMessage m)
    {
        var blocks = new List<AnthropicContentBlock>();

        if (!string.IsNullOrEmpty(m.Content))
            blocks.Add(new AnthropicContentBlock { Type = "text", Text = m.Content });

        foreach (var tc in m.ToolCalls!)
        {
            blocks.Add(new AnthropicContentBlock
            {
                Type = "tool_use",
                Id = tc.Id,
                Name = tc.FunctionName,
                Input = tc.Arguments
            });
        }

        return new AnthropicMessage
        {
            Role = "assistant",
            Content = blocks
        };
    }

    private static async Task<List<AnthropicContentBlock>> MapContentPartsAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        CancellationToken ct)
    {
        var blocks = new List<AnthropicContentBlock>();
        foreach (var part in contentParts)
        {
            switch (part)
            {
                case TextContentPart text:
                    blocks.Add(new AnthropicContentBlock { Type = "text", Text = text.Text });
                    break;
                case ImageFileContentPart file:
                    var bytes = await File.ReadAllBytesAsync(file.FilePath, ct);
                    blocks.Add(new AnthropicContentBlock
                    {
                        Type = "image",
                        Source = new AnthropicImageSource
                        {
                            MediaType = ImageDataUriHelper.GetMimeType(file.FilePath),
                            Data = Convert.ToBase64String(bytes)
                        }
                    });
                    break;
                case ImageBase64ContentPart base64:
                    blocks.Add(new AnthropicContentBlock
                    {
                        Type = "image",
                        Source = new AnthropicImageSource
                        {
                            MediaType = base64.MediaType,
                            Data = base64.Base64Data
                        }
                    });
                    break;
            }
        }
        return blocks;
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
