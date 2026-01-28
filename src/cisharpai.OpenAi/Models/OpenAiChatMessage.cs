namespace cisharpai.OpenAi.Models;

public sealed class OpenAiChatMessage
{
    public string Role { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}
