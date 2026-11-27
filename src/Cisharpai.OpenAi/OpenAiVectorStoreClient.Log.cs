using Microsoft.Extensions.Logging;

namespace Cisharpai.OpenAi;

public sealed partial class OpenAiVectorStoreClient
{
    [LoggerMessage(Level = LogLevel.Debug, Message = "File {FileId} in store {VectorStoreId} status: {Status}, polling again in {IntervalMs}ms")]
    private partial void LogFilePollingStatus(string fileId, string vectorStoreId, string status, double intervalMs);
}
