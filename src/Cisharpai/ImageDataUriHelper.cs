namespace Cisharpai;

/// <summary>
/// Utility for converting image file paths to data URIs and detecting MIME types.
/// </summary>
public static class ImageDataUriHelper
{
    /// <summary>
    /// Reads an image file and returns it as a data URI string (data:{mime};base64,{base64data}).
    /// </summary>
    public static async Task<string> ToDataUriAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var base64 = Convert.ToBase64String(imageBytes);
        var mime = GetMimeType(imagePath);
        return $"data:{mime};base64,{base64}";
    }

    /// <summary>
    /// Returns the MIME type for a given image file path based on its extension.
    /// </summary>
    public static string GetMimeType(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            _ => "application/octet-stream"
        };
    }
}
