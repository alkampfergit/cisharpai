namespace Cisharpai.Rag.Pipeline;

/// <summary>
/// Composable pipeline: query transform → retrieve → rerank → pack → grounded chat.
/// </summary>
public interface IRagPipeline
{
    Task<RagResult> AskAsync(
        string query,
        RagPipelineOptions? options = null,
        CancellationToken cancellationToken = default);

    IAsyncEnumerable<RagStreamingChunk> AskStreamingAsync(
        string query,
        RagPipelineOptions? options = null,
        CancellationToken cancellationToken = default);
}
