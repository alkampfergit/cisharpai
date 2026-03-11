using System.Runtime.CompilerServices;
using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.OpenAi;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature
{
    private const string ChatCompletionsEndpoint = "chat/completions";
    private const string ResponsesEndpoint = "responses";

    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LlmHttpClient _client;
    private readonly OpenAiClientOptions _options;

    public IFeatureCollection Features { get; }

    public OpenAiChatCompletionClient(HttpClient httpClient, OpenAiClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
        features.Set<IToolCallingFeature>(this);
        features.Set<IStreamingChatFeature>(this);
        Features = features;
    }

    public async Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            var modelType = DetectModelType(model);

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

    public async Task<ChatCompletionResponse> GetChatCompletionWithJsonOutputAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            jsonOutputOptions.Validate();

            var modelType = DetectModelType(model);

            return modelType switch
            {
                OpenAiModelType.Gpt5 => await SendResponsesApiWithJsonAsync(request, jsonOutputOptions, cancellationToken),
                OpenAiModelType.Reasoning => await SendReasoningChatWithJsonAsync(request, jsonOutputOptions, cancellationToken),
                _ => await SendLegacyChatWithJsonAsync(request, jsonOutputOptions, cancellationToken)
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

    public async Task<ToolCallingResponse> GetChatCompletionWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        try
        {
            toolOptions.Validate();

            var modelType = DetectModelType(model);

            return modelType switch
            {
                OpenAiModelType.Reasoning => await SendReasoningChatWithToolsAsync(request, toolOptions, cancellationToken),
                _ => await SendLegacyChatWithToolsAsync(request, toolOptions, cancellationToken)
            };
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
        var model = ResolveModel(request.Model);
        request = request with { Model = model };

        var modelType = DetectModelType(model);

        if (modelType == OpenAiModelType.Gpt5)
        {
            await foreach (var chunk in StreamResponsesApiAsync(request, cancellationToken))
                yield return chunk;
        }
        else
        {
            await foreach (var chunk in StreamLegacyChatAsync(request, cancellationToken))
                yield return chunk;
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamLegacyChatAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken),
            Stream = true,
            StreamOptions = new OpenAiStreamOptions { IncludeUsage = true }
        };

        await foreach (var json in _client.PostStreamAsync(ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken))
        {
            OpenAiStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<OpenAiStreamChunk>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (chunk is null) continue;

            if (chunk.Choices is { Count: > 0 })
            {
                var choice = chunk.Choices[0];
                var toolDelta = MapStreamToolCallDelta(choice.Delta?.ToolCalls);
                yield return new ChatCompletionChunk(
                    Content: choice.Delta?.Content ?? string.Empty,
                    FinishReason: choice.FinishReason,
                    Model: chunk.Model,
                    PromptTokens: chunk.Usage?.PromptTokens,
                    CompletionTokens: chunk.Usage?.CompletionTokens,
                    ToolCallDelta: toolDelta);
            }
            else if (chunk.Usage is not null)
            {
                // Usage-only final chunk (when stream_options.include_usage is true)
                yield return new ChatCompletionChunk(
                    Content: string.Empty,
                    PromptTokens: chunk.Usage.PromptTokens,
                    CompletionTokens: chunk.Usage.CompletionTokens);
            }
        }
    }

    private async IAsyncEnumerable<ChatCompletionChunk> StreamResponsesApiAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model!,
            MaxOutputTokens = request.MaxTokens,
            Input = await MapMessagesAsync(request.Messages, cancellationToken),
            Stream = true,
            Reasoning = _options.ReasoningEffort is not null
                ? new OpenAiReasoningOption { Effort = _options.ReasoningEffort }
                : null,
            Text = _options.TextVerbosity is not null
                ? new OpenAiTextOption { Verbosity = _options.TextVerbosity }
                : null
        };

        string? model = null;
        int? promptTokens = null;
        int? completionTokens = null;

        await foreach (var json in _client.PostStreamAsync(ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken))
        {
            OpenAiResponsesStreamEvent? evt;
            try
            {
                evt = JsonSerializer.Deserialize<OpenAiResponsesStreamEvent>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (evt is null) continue;

            switch (evt.Type)
            {
                case "response.output_text.delta":
                    yield return new ChatCompletionChunk(
                        Content: evt.Delta ?? string.Empty,
                        Model: model);
                    break;

                case "response.completed":
                    if (evt.Response is not null)
                    {
                        model = evt.Response.Model;
                        promptTokens = evt.Response.Usage?.InputTokens;
                        completionTokens = evt.Response.Usage?.OutputTokens;

                        yield return new ChatCompletionChunk(
                            Content: string.Empty,
                            FinishReason: evt.Response.Status,
                            Model: model,
                            PromptTokens: promptTokens,
                            CompletionTokens: completionTokens);
                    }
                    break;
            }
        }
    }

    private async Task<ChatCompletionResponse> SendLegacyChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ChatCompletionResponse> SendLegacyChatWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(messages, cancellationToken),
            ResponseFormat = BuildChatCompletionsResponseFormat(jsonOptions)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ToolCallingResponse> SendLegacyChatWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model!,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken),
            Tools = MapToolDefinitions(toolOptions.Tools),
            ToolChoice = MapToolChoice(toolOptions.ToolChoice)
        };

        return await ExecuteToolCallingAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> SendReasoningChatAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model!,
            MaxCompletionTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ChatCompletionResponse> SendReasoningChatWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model!,
            MaxCompletionTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(messages, cancellationToken),
            ResponseFormat = BuildChatCompletionsResponseFormat(jsonOptions)
        };

        if (request.IncludeRawResponse)
        {
            var (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
            return MapChatResponse(raw, rawResponseJson, rawRequestJson);
        }

        return MapChatResponse(
            await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken));
    }

    private async Task<ToolCallingResponse> SendReasoningChatWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model!,
            MaxCompletionTokens = request.MaxTokens,
            Messages = await MapMessagesAsync(request.Messages, cancellationToken),
            Tools = MapToolDefinitions(toolOptions.Tools),
            ToolChoice = MapToolChoice(toolOptions.ToolChoice)
        };

        string? rawResponseJson = null;
        string? rawRequestJson = null;
        OpenAiChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiReasoningRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private async Task<ToolCallingResponse> ExecuteToolCallingAsync(
        OpenAiChatRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        string? rawResponseJson = null;
        string? rawRequestJson = null;
        OpenAiChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiChatRequest, OpenAiChatResponse>(
                ChatCompletionsEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private async Task<ChatCompletionResponse> SendResponsesApiAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model!,
            MaxOutputTokens = request.MaxTokens,
            Input = await MapMessagesAsync(request.Messages, cancellationToken),
            Reasoning = _options.ReasoningEffort is not null
                ? new OpenAiReasoningOption { Effort = _options.ReasoningEffort }
                : null,
            Text = _options.TextVerbosity is not null
                ? new OpenAiTextOption { Verbosity = _options.TextVerbosity }
                : null
        };

        return await ExecuteResponsesApiAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> SendResponsesApiWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var textOption = new OpenAiTextOption
        {
            Verbosity = _options.TextVerbosity,
            Format = BuildResponsesApiTextFormat(jsonOptions)
        };

        var providerRequest = new OpenAiResponsesApiRequest
        {
            Model = request.Model!,
            MaxOutputTokens = request.MaxTokens,
            Input = await MapMessagesAsync(messages, cancellationToken),
            Reasoning = _options.ReasoningEffort is not null
                ? new OpenAiReasoningOption { Effort = _options.ReasoningEffort }
                : null,
            Text = textOption
        };

        return await ExecuteResponsesApiAsync(providerRequest, request, cancellationToken);
    }

    private async Task<ChatCompletionResponse> ExecuteResponsesApiAsync(
        OpenAiResponsesApiRequest providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        string? rawResponseJson = null;
        string? rawRequestJson = null;
        OpenAiResponsesApiResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<OpenAiResponsesApiRequest, OpenAiResponsesApiResponse>(
                ResponsesEndpoint, providerRequest, request.ExtraParameters, cancellationToken);
        }

        var content = raw.Output
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "output_text")
            .Select(c => c.Text)
            .FirstOrDefault() ?? string.Empty;

        var refusal = raw.Output
            .Where(o => o.Type == "message")
            .SelectMany(o => o.Content)
            .Where(c => c.Type == "refusal")
            .Select(c => c.Refusal)
            .FirstOrDefault();

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
            ErrorMessage: errorMessage,
            Refusal: refusal);
    }

    private static ChatCompletionResponse MapChatResponse(
        OpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var refusal = choice?.Message.Refusal;

        return new ChatCompletionResponse(
            Content: ExtractStringContent(choice?.Message.Content),
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Refusal: refusal);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        OpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var content = ExtractStringContent(choice?.Message.Content);

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);

        var toolCalls = MapResponseToolCalls(choice?.Message.ToolCalls);

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static List<ToolCall>? MapResponseToolCalls(List<OpenAiToolCall>? toolCalls)
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
                // If arguments can't be parsed, wrap them as a raw string
                arguments = JsonDocument.Parse($"\"{tc.Function.Arguments}\"").RootElement.Clone();
            }

            return new ToolCall(tc.Id, tc.Function.Name, arguments);
        }).ToList();
    }

    private static List<OpenAiToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new OpenAiToolDefinition
        {
            Type = "function",
            Function = new OpenAiToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
                Strict = t.Strict
            }
        }).ToList();
    }

    private static object? MapToolChoice(ToolChoice? toolChoice)
    {
        if (toolChoice is null)
            return null;

        if (toolChoice == ToolChoice.Auto)
            return "auto";

        if (toolChoice == ToolChoice.None)
            return "none";

        if (toolChoice == ToolChoice.Required)
            return "required";

        if (toolChoice.IsSpecific)
        {
            return new OpenAiToolChoiceObject
            {
                Type = "function",
                Function = new OpenAiToolChoiceFunction { Name = toolChoice.FunctionName! }
            };
        }

        return null;
    }

    private static OpenAiResponseFormat BuildChatCompletionsResponseFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new OpenAiResponseFormat { Type = "json_object" };
        }

        return new OpenAiResponseFormat
        {
            Type = "json_schema",
            JsonSchema = new OpenAiJsonSchemaSpec
            {
                Name = options.SchemaName!,
                Description = options.SchemaDescription,
                Strict = options.Strict,
                Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
            }
        };
    }

    private static OpenAiTextFormat BuildResponsesApiTextFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new OpenAiTextFormat { Type = "json_object" };
        }

        return new OpenAiTextFormat
        {
            Type = "json_schema",
            Name = options.SchemaName,
            Description = options.SchemaDescription,
            Strict = options.Strict,
            Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
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
            result[index] = new LlmMessage(LlmRole.System, systemMessage.Content + " Respond in JSON.");
        }
        else
        {
            result.Insert(0, new LlmMessage(LlmRole.System, "Respond in JSON."));
        }

        return result;
    }

    private static async Task<List<OpenAiChatMessage>> MapMessagesAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken ct)
    {
        var result = new List<OpenAiChatMessage>();

        foreach (var m in messages)
        {
            var msg = new OpenAiChatMessage { Role = MapRole(m.Role) };

            if (m.ContentParts is { Count: > 0 })
                msg.Content = await MapContentPartsAsync(m.ContentParts, ct);
            else
                msg.Content = m.Content;

            if (m.Role == LlmRole.Tool && m.ToolCallId is not null)
                msg.ToolCallId = m.ToolCallId;

            if (m.Role == LlmRole.Assistant && m.ToolCalls is { Count: > 0 })
                msg.ToolCalls = MapToolCalls(m.ToolCalls);

            result.Add(msg);
        }

        return result;
    }

    private static async Task<List<OpenAiContentPart>> MapContentPartsAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        CancellationToken ct)
    {
        var parts = new List<OpenAiContentPart>();
        foreach (var part in contentParts)
        {
            switch (part)
            {
                case TextContentPart text:
                    parts.Add(new OpenAiContentPart { Type = "text", Text = text.Text });
                    break;
                case ImageFileContentPart file:
                    var dataUri = await ImageDataUriHelper.ToDataUriAsync(file.FilePath, ct);
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAiImageUrl { Url = dataUri }
                    });
                    break;
                case ImageBase64ContentPart base64:
                    parts.Add(new OpenAiContentPart
                    {
                        Type = "image_url",
                        ImageUrl = new OpenAiImageUrl
                        {
                            Url = $"data:{base64.MediaType};base64,{base64.Base64Data}"
                        }
                    });
                    break;
            }
        }
        return parts;
    }

    private static List<OpenAiToolCall> MapToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        return toolCalls.Select(tc => new OpenAiToolCall
        {
            Id = tc.Id,
            Type = "function",
            Function = new OpenAiToolCallFunction
            {
                Name = tc.FunctionName,
                Arguments = tc.Arguments.GetRawText()
            }
        }).ToList();
    }

    private static ToolCallDelta? MapStreamToolCallDelta(List<OpenAiStreamToolCallDelta>? toolCalls)
    {
        if (toolCalls is null || toolCalls.Count == 0)
            return null;

        var first = toolCalls[0];
        return new ToolCallDelta(
            Index: first.Index,
            Id: first.Id,
            FunctionName: first.Function?.Name,
            ArgumentsDelta: first.Function?.Arguments);
    }

    private static string ExtractStringContent(object? content)
    {
        return content switch
        {
            string s => s,
            System.Text.Json.JsonElement je when je.ValueKind == System.Text.Json.JsonValueKind.String => je.GetString() ?? string.Empty,
            _ => string.Empty
        };
    }

    private static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        LlmRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };

    private string ResolveModel(string? model)
    {
        return model
            ?? _options.DefaultModel
            ?? throw new InvalidOperationException(
                "Model must be specified either in the request or via DefaultModel in options.");
    }

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
