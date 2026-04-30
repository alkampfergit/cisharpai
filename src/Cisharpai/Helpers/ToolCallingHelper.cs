using System.Text.Json;
using Cisharpai.Models;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared helper methods for tool calling across providers.
/// </summary>
public static class ToolCallingHelper
{
    /// <summary>
    /// Maps provider-specific tool call objects to unified ToolCall models.
    /// The extractor function pulls (Id, FunctionName, Arguments) from each provider-specific type.
    /// </summary>
    public static List<ToolCall>? MapResponseToolCalls<T>(
        List<T>? toolCalls,
        Func<T, (string Id, string FunctionName, string Arguments)> extractor)
    {
        if (toolCalls is null || toolCalls.Count == 0)
            return null;

        return toolCalls.Select(tc =>
        {
            var (id, functionName, arguments) = extractor(tc);
            JsonElement parsedArguments;
            try
            {
                parsedArguments = JsonDocument.Parse(arguments).RootElement.Clone();
            }
            catch
            {
                // If arguments can't be parsed, wrap them as a raw string
                parsedArguments = JsonDocument.Parse($"\"{arguments}\"").RootElement.Clone();
            }

            return new ToolCall(id, functionName, parsedArguments);
        }).ToList();
    }

    /// <summary>
    /// Maps a ToolChoice to the standard string/object representation used by
    /// OpenAI, Azure OpenAI, and Azure AI Inference. The specificMapper creates
    /// the provider-specific object for ToolChoice.Specific.
    /// </summary>
    public static object? MapToolChoice(ToolChoice? toolChoice, Func<string, object> specificMapper)
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
            return specificMapper(toolChoice.FunctionName!);

        return null;
    }

    /// <summary>
    /// Maps provider-specific stream tool call delta objects to unified ToolCallDelta.
    /// The extractor function pulls (Index, Id, FunctionName, ArgumentsDelta) from the provider type.
    /// </summary>
    public static ToolCallDelta? MapStreamToolCallDelta<T>(
        List<T>? toolCalls,
        Func<T, (int Index, string? Id, string? FunctionName, string? ArgumentsDelta)> extractor)
    {
        if (toolCalls is null || toolCalls.Count == 0)
            return null;

        var (index, id, functionName, argumentsDelta) = extractor(toolCalls[0]);
        return new ToolCallDelta(
            Index: index,
            Id: id,
            FunctionName: functionName,
            ArgumentsDelta: argumentsDelta);
    }
}
