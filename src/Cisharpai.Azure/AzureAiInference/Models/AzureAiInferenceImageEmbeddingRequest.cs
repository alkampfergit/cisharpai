namespace Cisharpai.Azure.AzureAiInference.Models;

public sealed class AzureAiInferenceImageEmbeddingRequest
{
    public string? Model { get; set; }

    public List<AzureAiInferenceImageInput> Input { get; set; } = [];
}

public sealed class AzureAiInferenceImageInput
{
    public string Image { get; set; } = string.Empty;
}
