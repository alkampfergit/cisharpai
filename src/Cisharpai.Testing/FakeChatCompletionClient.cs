using System.Runtime.CompilerServices;
using System.Text.Json;
using Cisharpai.Features;
using Cisharpai.Features.Chat;
using Cisharpai.Models;

namespace Cisharpai.Testing;

/// <summary>
/// A fake implementation of <see cref="IChatCompletionClient"/> for unit testing.
/// Supports canned responses via queues and defaults, and captures all received requests.
/// </summary>
public sealed class FakeChatCompletionClient :
    IChatCompletionClient,
    IStreamingChatFeature,
    IToolCallingFeature,
    IJsonOutputFeature,
    IGroundedChatFeature,
    IPromptCachingFeature
{
    private readonly Queue<ChatCompletionResponse> _responses = new();
    private readonly Queue<ToolCallingResponse> _toolCallingResponses = new();
    private readonly Queue<ChatCompletionResponse> _jsonOutputResponses = new();
    private readonly Queue<GroundedChatCompletionResponse> _groundedChatResponses = new();
    private readonly Queue<IReadOnlyList<ChatCompletionChunk>> _streamingResponses = new();
    private readonly Queue<ChatCompletionResponse> _promptCachingResponses = new();
    private readonly Queue<GroundedChatCompletionResponse> _groundedCachingResponses = new();
    private readonly Queue<ToolCallingResponse> _toolCachingResponses = new();

    private readonly List<ChatCompletionRequest> _receivedRequests = new();
    private readonly List<(ChatCompletionRequest Request, ToolCallingOptions Options)> _receivedToolCallingRequests = new();
    private readonly List<(ChatCompletionRequest Request, JsonOutputOptions Options)> _receivedJsonOutputRequests = new();
    private readonly List<(ChatCompletionRequest Request, GroundedChatOptions Options)> _receivedGroundedChatRequests = new();
    private readonly List<ChatCompletionRequest> _receivedStreamingRequests = new();
    private readonly List<(ChatCompletionRequest Request, PromptCachingOptions Options)> _receivedPromptCachingRequests = new();
    private readonly List<(ChatCompletionRequest Request, GroundedChatOptions GroundedOptions, PromptCachingOptions CachingOptions)> _receivedGroundedCachingRequests = new();
    private readonly List<(ChatCompletionRequest Request, ToolCallingOptions ToolOptions, PromptCachingOptions CachingOptions)> _receivedToolCachingRequests = new();

    public FakeChatCompletionClient(FakeChatFeatures enabledFeatures = FakeChatFeatures.All)
    {
        Features = new FeatureCollection();

        if (enabledFeatures.HasFlag(FakeChatFeatures.Streaming))
            Features.Set<IStreamingChatFeature>(this);
        if (enabledFeatures.HasFlag(FakeChatFeatures.ToolCalling))
            Features.Set<IToolCallingFeature>(this);
        if (enabledFeatures.HasFlag(FakeChatFeatures.JsonOutput))
            Features.Set<IJsonOutputFeature>(this);
        if (enabledFeatures.HasFlag(FakeChatFeatures.GroundedChat))
            Features.Set<IGroundedChatFeature>(this);
        if (enabledFeatures.HasFlag(FakeChatFeatures.PromptCaching))
            Features.Set<IPromptCachingFeature>(this);
    }

    public IFeatureCollection Features { get; }

    // --- Response configuration ---

    /// <summary>
    /// Default response returned when the queue is empty.
    /// </summary>
    public ChatCompletionResponse? DefaultResponse { get; set; }

    public ChatCompletionResponse? DefaultJsonOutputResponse { get; set; }
    public ToolCallingResponse? DefaultToolCallingResponse { get; set; }
    public GroundedChatCompletionResponse? DefaultGroundedChatResponse { get; set; }
    public IReadOnlyList<ChatCompletionChunk>? DefaultStreamingResponse { get; set; }
    public ChatCompletionResponse? DefaultPromptCachingResponse { get; set; }
    public GroundedChatCompletionResponse? DefaultGroundedCachingResponse { get; set; }
    public ToolCallingResponse? DefaultToolCachingResponse { get; set; }

    /// <summary>Enqueues a response to be returned by the next call.</summary>
    public void EnqueueResponse(ChatCompletionResponse response) => _responses.Enqueue(response);

    /// <summary>Enqueues a tool calling response.</summary>
    public void EnqueueToolCallingResponse(ToolCallingResponse response) => _toolCallingResponses.Enqueue(response);

    /// <summary>Enqueues a JSON output response.</summary>
    public void EnqueueJsonOutputResponse(ChatCompletionResponse response) => _jsonOutputResponses.Enqueue(response);

    /// <summary>Enqueues a grounded chat response.</summary>
    public void EnqueueGroundedChatResponse(GroundedChatCompletionResponse response) => _groundedChatResponses.Enqueue(response);

    /// <summary>Enqueues streaming chunks for the next streaming call.</summary>
    public void EnqueueStreamingResponse(IReadOnlyList<ChatCompletionChunk> chunks) => _streamingResponses.Enqueue(chunks);

    /// <summary>Enqueues a prompt caching response.</summary>
    public void EnqueuePromptCachingResponse(ChatCompletionResponse response) => _promptCachingResponses.Enqueue(response);

    /// <summary>Enqueues a grounded chat + caching response.</summary>
    public void EnqueueGroundedCachingResponse(GroundedChatCompletionResponse response) => _groundedCachingResponses.Enqueue(response);

    /// <summary>Enqueues a tool calling + caching response.</summary>
    public void EnqueueToolCachingResponse(ToolCallingResponse response) => _toolCachingResponses.Enqueue(response);

    // --- Request capture ---

    public IReadOnlyList<ChatCompletionRequest> ReceivedRequests => _receivedRequests;
    public IReadOnlyList<(ChatCompletionRequest Request, ToolCallingOptions Options)> ReceivedToolCallingRequests => _receivedToolCallingRequests;
    public IReadOnlyList<(ChatCompletionRequest Request, JsonOutputOptions Options)> ReceivedJsonOutputRequests => _receivedJsonOutputRequests;
    public IReadOnlyList<(ChatCompletionRequest Request, GroundedChatOptions Options)> ReceivedGroundedChatRequests => _receivedGroundedChatRequests;
    public IReadOnlyList<ChatCompletionRequest> ReceivedStreamingRequests => _receivedStreamingRequests;
    public IReadOnlyList<(ChatCompletionRequest Request, PromptCachingOptions Options)> ReceivedPromptCachingRequests => _receivedPromptCachingRequests;
    public IReadOnlyList<(ChatCompletionRequest Request, GroundedChatOptions GroundedOptions, PromptCachingOptions CachingOptions)> ReceivedGroundedCachingRequests => _receivedGroundedCachingRequests;
    public IReadOnlyList<(ChatCompletionRequest Request, ToolCallingOptions ToolOptions, PromptCachingOptions CachingOptions)> ReceivedToolCachingRequests => _receivedToolCachingRequests;

    /// <summary>Total number of calls across all methods.</summary>
    public int CallCount => _receivedRequests.Count + _receivedToolCallingRequests.Count +
                            _receivedJsonOutputRequests.Count + _receivedGroundedChatRequests.Count +
                            _receivedStreamingRequests.Count + _receivedPromptCachingRequests.Count +
                            _receivedGroundedCachingRequests.Count + _receivedToolCachingRequests.Count;

    /// <summary>Clears all queued responses and captured requests.</summary>
    public void Reset()
    {
        _responses.Clear();
        _toolCallingResponses.Clear();
        _jsonOutputResponses.Clear();
        _groundedChatResponses.Clear();
        _streamingResponses.Clear();
        _receivedRequests.Clear();
        _receivedToolCallingRequests.Clear();
        _receivedJsonOutputRequests.Clear();
        _receivedGroundedChatRequests.Clear();
        _receivedStreamingRequests.Clear();
        _promptCachingResponses.Clear();
        _receivedPromptCachingRequests.Clear();
        _groundedCachingResponses.Clear();
        _receivedGroundedCachingRequests.Clear();
        _toolCachingResponses.Clear();
        _receivedToolCachingRequests.Clear();
    }

    // --- Interface implementations ---

    public Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        _receivedRequests.Add(request);
        return Task.FromResult(Dequeue(_responses, DefaultResponse));
    }

    public Task<ChatCompletionResponse> GetChatCompletionWithJsonOutputAsync(
        ChatCompletionRequest request,
        JsonOutputOptions jsonOutputOptions,
        CancellationToken cancellationToken = default)
    {
        _receivedJsonOutputRequests.Add((request, jsonOutputOptions));
        return Task.FromResult(Dequeue(_jsonOutputResponses, DefaultJsonOutputResponse ?? DefaultResponse));
    }

    public Task<ToolCallingResponse> GetChatCompletionWithToolsAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        CancellationToken cancellationToken = default)
    {
        _receivedToolCallingRequests.Add((request, toolOptions));
        return Task.FromResult(Dequeue(_toolCallingResponses, DefaultToolCallingResponse));
    }

    public Task<GroundedChatCompletionResponse> GetGroundedChatCompletionAsync(
        ChatCompletionRequest request,
        GroundedChatOptions groundedChatOptions,
        CancellationToken cancellationToken = default)
    {
        _receivedGroundedChatRequests.Add((request, groundedChatOptions));
        return Task.FromResult(Dequeue(_groundedChatResponses, DefaultGroundedChatResponse));
    }

    public Task<ChatCompletionResponse> GetChatCompletionWithCachingAsync(
        ChatCompletionRequest request,
        PromptCachingOptions cachingOptions,
        CancellationToken cancellationToken = default)
    {
        _receivedPromptCachingRequests.Add((request, cachingOptions));
        return Task.FromResult(Dequeue(_promptCachingResponses, DefaultPromptCachingResponse ?? DefaultResponse));
    }

    public Task<GroundedChatCompletionResponse> GetGroundedChatCompletionWithCachingAsync(
        ChatCompletionRequest request,
        GroundedChatOptions groundedChatOptions,
        PromptCachingOptions cachingOptions,
        CancellationToken cancellationToken = default)
    {
        _receivedGroundedCachingRequests.Add((request, groundedChatOptions, cachingOptions));
        return Task.FromResult(Dequeue(_groundedCachingResponses, DefaultGroundedCachingResponse ?? DefaultGroundedChatResponse));
    }

    public Task<ToolCallingResponse> GetChatCompletionWithToolsAndCachingAsync(
        ChatCompletionRequest request,
        ToolCallingOptions toolOptions,
        PromptCachingOptions cachingOptions,
        CancellationToken cancellationToken = default)
    {
        _receivedToolCachingRequests.Add((request, toolOptions, cachingOptions));
        return Task.FromResult(Dequeue(_toolCachingResponses, DefaultToolCachingResponse ?? DefaultToolCallingResponse));
    }

    public async IAsyncEnumerable<ChatCompletionChunk> GetChatCompletionStreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _receivedStreamingRequests.Add(request);
        var chunks = Dequeue(_streamingResponses, DefaultStreamingResponse);

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return chunk;
            await Task.Yield();
        }
    }

    private static T Dequeue<T>(Queue<T> queue, T? fallback)
    {
        if (queue.Count > 0)
            return queue.Dequeue();

        if (fallback is not null)
            return fallback;

        throw new InvalidOperationException(
            $"No queued response and no default configured for {typeof(T).Name}. " +
            "Enqueue a response or set a default before calling the client.");
    }
}
