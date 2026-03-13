using System.Runtime.CompilerServices;
using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Helpers;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi.Models;

namespace Cisharpai.Azure.AzureOpenAi;

/// <summary>
/// Azure OpenAI chat completion client using HttpClient.
/// Supports both legacy models (GPT-4) and reasoning models (o1/o3/o4/GPT-5).
/// </summary>
public sealed class AzureOpenAiChatCompletionClient : IChatCompletionClient, IJsonOutputFeature, IToolCallingFeature, IStreamingChatFeature
{
    private static readonly JsonSerializerOptions StreamJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly LlmHttpClient _client;
    private readonly AzureOpenAiClientOptions _options;

    public IFeatureCollection Features { get; }

    public AzureOpenAiChatCompletionClient(
        HttpClient httpClient,
        AzureOpenAiClientOptions options)
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
        try
        {
            var model = ResolveModel(request.Model);
            var messages = await MapMessagesAsync(request.Messages, cancellationToken);
            var isReasoning = IsReasoningModel(model);

            object providerRequest = isReasoning
                ? new AzureOpenAiReasoningChatRequest
                {
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens
                }
                : new AzureOpenAiChatRequest
                {
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Messages = messages
                };

            return await ExecuteRequestAsync(providerRequest, request, cancellationToken);
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

            var model = ResolveModel(request.Model);
            var adjustedMessages = JsonOutputHelper.EnsureJsonKeywordInSystemMessage(request.Messages, jsonOutputOptions);
            var messages = await MapMessagesAsync(adjustedMessages, cancellationToken);
            var isReasoning = IsReasoningModel(model);
            var responseFormat = BuildResponseFormat(jsonOutputOptions);

            object providerRequest = isReasoning
                ? new AzureOpenAiReasoningChatRequest
                {
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens,
                    ResponseFormat = responseFormat
                }
                : new AzureOpenAiChatRequest
                {
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Messages = messages,
                    ResponseFormat = responseFormat
                };

            return await ExecuteRequestAsync(providerRequest, request, cancellationToken);
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
        try
        {
            toolOptions.Validate();

            var model = ResolveModel(request.Model);
            var messages = await MapMessagesAsync(request.Messages, cancellationToken);
            var isReasoning = IsReasoningModel(model);
            var tools = MapToolDefinitions(toolOptions.Tools);
            var toolChoice = MapToolChoiceValue(toolOptions.ToolChoice);

            object providerRequest = isReasoning
                ? new AzureOpenAiReasoningChatRequest
                {
                    Messages = messages,
                    MaxCompletionTokens = request.MaxTokens,
                    Tools = tools,
                    ToolChoice = toolChoice
                }
                : new AzureOpenAiChatRequest
                {
                    Temperature = request.Temperature,
                    MaxTokens = request.MaxTokens,
                    Messages = messages,
                    Tools = tools,
                    ToolChoice = toolChoice
                };

            return await ExecuteToolCallingRequestAsync(providerRequest, request, cancellationToken);
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
        var messages = await MapMessagesAsync(request.Messages, cancellationToken);
        var isReasoning = IsReasoningModel(model);
        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        object providerRequest = isReasoning
            ? new AzureOpenAiReasoningChatRequest
            {
                Messages = messages,
                MaxCompletionTokens = request.MaxTokens,
                Stream = true,
                StreamOptions = new AzureOpenAiStreamOptions { IncludeUsage = true }
            }
            : new AzureOpenAiChatRequest
            {
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                Messages = messages,
                Stream = true,
                StreamOptions = new AzureOpenAiStreamOptions { IncludeUsage = true }
            };

        await foreach (var json in _client.PostStreamAsync<object>(uri, providerRequest, request.ExtraParameters, cancellationToken))
        {
            AzureOpenAiStreamChunk? chunk;
            try
            {
                chunk = JsonSerializer.Deserialize<AzureOpenAiStreamChunk>(json, StreamJsonOptions);
            }
            catch
            {
                continue;
            }

            if (chunk is null) continue;

            if (chunk.Choices is { Count: > 0 })
            {
                var choice = chunk.Choices[0];
                var toolDelta = ToolCallingHelper.MapStreamToolCallDelta(
                    choice.Delta?.ToolCalls,
                    tc => (tc.Index, tc.Id, tc.Function?.Name, tc.Function?.Arguments));
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
                yield return new ChatCompletionChunk(
                    Content: string.Empty,
                    PromptTokens: chunk.Usage.PromptTokens,
                    CompletionTokens: chunk.Usage.CompletionTokens);
            }
        }
    }

    private async Task<ChatCompletionResponse> ExecuteRequestAsync(
        object providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        string? rawResponseJson = null;
        string? rawRequestJson = null;
        AzureOpenAiChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<object, AzureOpenAiChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<object, AzureOpenAiChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }

        var choice = raw.Choices.FirstOrDefault();

        return new ChatCompletionResponse(
            Content: ContentPartHelper.ExtractStringContent(choice?.Message.Content),
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Refusal: choice?.Message.Refusal);
    }

    private async Task<ToolCallingResponse> ExecuteToolCallingRequestAsync(
        object providerRequest,
        ChatCompletionRequest request,
        CancellationToken cancellationToken)
    {
        var uri = $"openai/deployments/{_options.DeploymentName}/chat/completions?api-version={_options.ApiVersion}";

        string? rawResponseJson = null;
        string? rawRequestJson = null;
        AzureOpenAiChatResponse raw;

        if (request.IncludeRawResponse)
        {
            (raw, rawResponseJson, rawRequestJson) = await _client.PostWithRawAsync<object, AzureOpenAiChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }
        else
        {
            raw = await _client.PostAsync<object, AzureOpenAiChatResponse>(
                uri, providerRequest, request.ExtraParameters, cancellationToken);
        }

        return MapToolCallingResponse(raw, rawResponseJson, rawRequestJson);
    }

    private static ToolCallingResponse MapToolCallingResponse(
        AzureOpenAiChatResponse raw,
        string? rawResponseJson = null,
        string? rawRequestJson = null)
    {
        var choice = raw.Choices.FirstOrDefault();
        var content = ContentPartHelper.ExtractStringContent(choice?.Message.Content);

        var chatCompletion = new ChatCompletionResponse(
            Content: content,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson);

        var toolCalls = ToolCallingHelper.MapResponseToolCalls(
            choice?.Message.ToolCalls,
            tc => (tc.Id, tc.Function.Name, tc.Function.Arguments));

        return new ToolCallingResponse(chatCompletion, toolCalls);
    }

    private static List<AzureOpenAiToolDefinition> MapToolDefinitions(IReadOnlyList<ToolDefinition> tools)
    {
        return tools.Select(t => new AzureOpenAiToolDefinition
        {
            Type = "function",
            Function = new AzureOpenAiToolFunction
            {
                Name = t.Name,
                Description = t.Description,
                Parameters = t.Parameters,
                Strict = t.Strict
            }
        }).ToList();
    }

    private static object? MapToolChoiceValue(ToolChoice? toolChoice) =>
        ToolCallingHelper.MapToolChoice(toolChoice, name => new AzureOpenAiToolChoiceObject
        {
            Type = "function",
            Function = new AzureOpenAiToolChoiceFunction { Name = name }
        });

    private static AzureOpenAiResponseFormat BuildResponseFormat(JsonOutputOptions options)
    {
        if (options.Mode == JsonOutputMode.JsonMode)
        {
            return new AzureOpenAiResponseFormat { Type = "json_object" };
        }

        return new AzureOpenAiResponseFormat
        {
            Type = "json_schema",
            JsonSchema = new AzureOpenAiJsonSchemaSpec
            {
                Name = options.SchemaName!,
                Description = options.SchemaDescription,
                Strict = options.Strict,
                Schema = JsonDocument.Parse(options.JsonSchema!).RootElement.Clone()
            }
        };
    }

    private static async Task<List<AzureOpenAiChatMessage>> MapMessagesAsync(
        IReadOnlyList<LlmMessage> messages,
        CancellationToken ct)
    {
        var result = new List<AzureOpenAiChatMessage>();

        foreach (var m in messages)
        {
            var msg = new AzureOpenAiChatMessage { Role = RoleMapper.MapRole(m.Role) };

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

    private static Task<List<AzureOpenAiContentPart>> MapContentPartsAsync(
        IReadOnlyList<MessageContentPart> contentParts,
        CancellationToken ct)
    {
        return ContentPartHelper.MapOpenAiStyleContentPartsAsync(
            contentParts,
            text => new AzureOpenAiContentPart { Type = "text", Text = text },
            url => new AzureOpenAiContentPart { Type = "image_url", ImageUrl = new AzureOpenAiImageUrl { Url = url } },
            ct);
    }

    private static List<AzureOpenAiToolCall> MapToolCalls(IReadOnlyList<ToolCall> toolCalls)
    {
        return toolCalls.Select(tc => new AzureOpenAiToolCall
        {
            Id = tc.Id,
            Type = "function",
            Function = new AzureOpenAiToolCallFunction
            {
                Name = tc.FunctionName,
                Arguments = tc.Arguments.GetRawText()
            }
        }).ToList();
    }

    private string? ResolveModel(string? model)
    {
        return model ?? _options.DefaultModel;
    }

    private static bool IsReasoningModel(string? model) =>
        model is not null &&
        (model.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
         model.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
         model.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
         model.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase));
}
