using System.Text.Json;

namespace Cisharpai.Cohere.Models;

public sealed class CohereChatDocument
{
    public string? Id { get; set; }
    public JsonElement Data { get; set; }
}

public sealed class CohereCitationOptions
{
    public string Mode { get; set; } = string.Empty;
}
