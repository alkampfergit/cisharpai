using Cisharpai.Models;

namespace Cisharpai.Helpers;

/// <summary>
/// Shared role mapping for providers that use standard system/user/assistant/tool role strings.
/// Anthropic uses different mapping and should not use this helper.
/// </summary>
public static class RoleMapper
{
    /// <summary>
    /// Maps an LlmRole to the standard role string used by OpenAI, Azure OpenAI,
    /// Azure AI Inference, and Cohere providers.
    /// </summary>
    public static string MapRole(LlmRole role) => role switch
    {
        LlmRole.System => "system",
        LlmRole.User => "user",
        LlmRole.Assistant => "assistant",
        LlmRole.Tool => "tool",
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null)
    };
}
