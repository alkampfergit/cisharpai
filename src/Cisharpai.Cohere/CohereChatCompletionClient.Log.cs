using Microsoft.Extensions.Logging;

namespace Cisharpai.Cohere;

public sealed partial class CohereChatCompletionClient
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "CitationMode.SearchResult is not supported by Cohere; falling back to CitationMode.Enabled.")]
    private partial void LogSearchResultCitationNotSupported();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Cohere model '{Model}' does not support CitationMode.Accurate; falling back to CitationMode.Fast.")]
    private partial void LogAccurateCitationNotSupported(string? model);
}
