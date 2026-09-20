using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiInputFile
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "input_file";

    [JsonPropertyName("filename")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Filename { get; set; }

    [JsonPropertyName("file_data")]
    public string FileData { get; set; } = string.Empty;
}
