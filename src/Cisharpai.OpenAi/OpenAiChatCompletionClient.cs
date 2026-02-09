using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi.Models;

namespace Cisharpai.OpenAi;

public sealed class OpenAiChatCompletionClient : IChatCompletionClient, IJsonOutputFeature
{
    private const string ChatCompletionsEndpoint = "chat/completions";
    private const string ResponsesEndpoint = "responses";

    private readonly LlmHttpClient _client;
    private readonly OpenAiClientOptions _options;

    public IFeatureCollection Features { get; }

    public OpenAiChatCompletionClient(HttpClient httpClient, OpenAiClientOptions options)
    {
        _client = new LlmHttpClient(httpClient);
        _options = options;

        var features = new FeatureCollection();
        features.Set<IJsonOutputFeature>(this);
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

    private async Task<ChatCompletionResponse> SendLegacyChatWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var providerRequest = new OpenAiChatRequest
        {
            Model = request.Model,
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            Messages = MapMessages(messages),
            ResponseFormat = BuildChatCompletionsResponseFormat(jsonOptions)
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

    private async Task<ChatCompletionResponse> SendReasoningChatWithJsonAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        var messages = EnsureJsonKeywordInSystemMessage(request.Messages, jsonOptions);

        var providerRequest = new OpenAiReasoningRequest
        {
            Model = request.Model,
            MaxCompletionTokens = request.MaxTokens,
            Messages = MapMessages(messages),
            ResponseFormat = BuildChatCompletionsResponseFormat(jsonOptions)
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
            Model = request.Model,
            MaxOutputTokens = request.MaxTokens,
            Input = MapMessages(messages),
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
            Content: choice?.Message.Content ?? string.Empty,
            Model: raw.Model,
            PromptTokens: raw.Usage.PromptTokens,
            CompletionTokens: raw.Usage.CompletionTokens,
            RawResponseJson: rawResponseJson,
            RawRequestJson: rawRequestJson,
            Refusal: refusal);
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
