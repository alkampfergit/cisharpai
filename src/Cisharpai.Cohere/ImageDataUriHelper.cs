namespace Cisharpai.Cohere;

internal static class ImageDataUriHelper
{
    internal static async Task<string> ToDataUriAsync(
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        var imageBytes = await File.ReadAllBytesAsync(imagePath, cancellationToken);
        var base64 = Convert.ToBase64String(imageBytes);
        var mime = GetMimeType(imagePath);
        return $"data:{mime};base64,{base64}";
    }

    internal static string GetMimeType(string path)
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
