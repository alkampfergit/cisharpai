using Cisharpai.Models;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Anthropic;

public sealed partial class AnthropicChatCompletionClient
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping null citation entry in content block.")]
    private partial void LogNullCitationEntry();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping search_result_location citation with missing source.")]
    private partial void LogMissingCitationSource();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping null citation entry in web search content block.")]
    private partial void LogNullWebSearchCitationEntry();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping unexpected citation type '{Type}' in web search response.")]
    private partial void LogUnexpectedCitationType(string? type);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Skipping web_search_result_location citation with missing URL.")]
    private partial void LogMissingWebSearchCitationUrl();

    [LoggerMessage(Level = LogLevel.Warning, Message = "Anthropic does not distinguish citation modes; CitationMode.{Mode} is treated as Enabled.")]
    private partial void LogCitationModeNotDistinguished(CitationMode mode);
}
