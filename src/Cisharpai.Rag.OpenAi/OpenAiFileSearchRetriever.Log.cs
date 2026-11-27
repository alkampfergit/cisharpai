using Microsoft.Extensions.Logging;

namespace Cisharpai.Rag.OpenAi;

internal sealed partial class OpenAiFileSearchRetriever
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "File search retrieval failed for store {VectorStoreId}: HTTP {StatusCode}")]
    private partial void LogFileSearchHttpFailure(string vectorStoreId, System.Net.HttpStatusCode? statusCode, System.Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File search response parsing failed for store {VectorStoreId}")]
    private partial void LogFileSearchParsingFailure(string vectorStoreId, System.Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "File search retrieval failed for store {VectorStoreId}")]
    private partial void LogFileSearchRetrievalFailure(string vectorStoreId, System.Exception ex);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{FailedCount} of {TotalCount} file search call(s) failed in store {VectorStoreId} (ids: {FailedIds})")]
    private partial void LogFileSearchCallsFailed(int failedCount, int totalCount, string vectorStoreId, string failedIds);
}
