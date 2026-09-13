namespace Cisharpai.Testing;

/// <summary>
/// Controls which optional features are registered on a <see cref="FakeChatCompletionClient"/>.
/// </summary>
[Flags]
public enum FakeChatFeatures
{
    None = 0,
    Streaming = 1 << 0,
    ToolCalling = 1 << 1,
    JsonOutput = 1 << 2,
    GroundedChat = 1 << 3,
    PromptCaching = 1 << 4,
    All = Streaming | ToolCalling | JsonOutput | GroundedChat | PromptCaching
}
