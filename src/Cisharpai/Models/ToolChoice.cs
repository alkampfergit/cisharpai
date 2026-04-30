namespace Cisharpai.Models;

/// <summary>
/// Controls how the model selects which tool to call.
/// </summary>
public abstract record ToolChoice
{
    private ToolChoice() { }

    /// <summary>
    /// Model decides whether to call a tool or generate text.
    /// </summary>
    public static readonly ToolChoice Auto = new AutoChoice();

    /// <summary>
    /// Model must not call any tool and must generate text.
    /// </summary>
    public static readonly ToolChoice None = new NoneChoice();

    /// <summary>
    /// Model must call at least one tool.
    /// </summary>
    public static readonly ToolChoice Required = new RequiredChoice();

    /// <summary>
    /// Model must call the specified function.
    /// Note: Not all providers support this (e.g. Cohere degrades to Required).
    /// </summary>
    public static ToolChoice Specific(string functionName) => new SpecificChoice(functionName);

    /// <summary>Returns true if this is a <see cref="SpecificChoice"/>.</summary>
    public bool IsSpecific => this is SpecificChoice;

    /// <summary>Gets the function name when this is a <see cref="SpecificChoice"/>, otherwise null.</summary>
    public string? FunctionName => (this as SpecificChoice)?.Name;

    internal sealed record AutoChoice() : ToolChoice;
    internal sealed record NoneChoice() : ToolChoice;
    internal sealed record RequiredChoice() : ToolChoice;
    internal sealed record SpecificChoice(string Name) : ToolChoice;
}
