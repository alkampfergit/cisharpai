using Cisharpai.Models;

namespace Cisharpai.Features.Embeddings;

/// <summary>
/// Optional feature for embedding images. Clients that support multimodal
/// embedding register this feature in their <see cref="IFeatureCollection"/>.
/// </summary>
public interface IImageEmbeddingFeature
{
    /// <summary>
    /// Embeds a single image.
    /// </summary>
    /// <param name="imagePath">Local file path to the image.</param>
    /// <param name="model">The model to use (must be multimodal compatible).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<EmbeddingResponse> GetImageEmbeddingAsync(
        string imagePath,
        string model,
        CancellationToken cancellationToken = default);
}
