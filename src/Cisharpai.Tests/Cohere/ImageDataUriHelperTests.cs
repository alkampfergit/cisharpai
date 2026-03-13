namespace Cisharpai.Tests.Cohere;

public sealed class ImageDataUriHelperTests
{
    [Test]
    public async Task ToDataUriAsync_PngFile_ReturnsCorrectDataUri()
    {
        var path = CreateTempFile(".png", [0x89, 0x50, 0x4E, 0x47]);
        try
        {
            var result = await ImageDataUriHelper.ToDataUriAsync(path);

            Assert.That(result, Does.StartWith("data:image/png;base64,"));
            var base64Part = result["data:image/png;base64,".Length..];
            var decoded = Convert.FromBase64String(base64Part);
            Assert.That(decoded, Is.EqualTo(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ToDataUriAsync_JpegFile_ReturnsCorrectMimeType()
    {
        var path = CreateTempFile(".jpg", [0xFF, 0xD8, 0xFF]);
        try
        {
            var result = await ImageDataUriHelper.ToDataUriAsync(path);
            Assert.That(result, Does.StartWith("data:image/jpeg;base64,"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ToDataUriAsync_JpegExtension_ReturnsCorrectMimeType()
    {
        var path = CreateTempFile(".jpeg", [0xFF, 0xD8, 0xFF]);
        try
        {
            var result = await ImageDataUriHelper.ToDataUriAsync(path);
            Assert.That(result, Does.StartWith("data:image/jpeg;base64,"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ToDataUriAsync_WebpFile_ReturnsCorrectMimeType()
    {
        var path = CreateTempFile(".webp", [0x52, 0x49, 0x46, 0x46]);
        try
        {
            var result = await ImageDataUriHelper.ToDataUriAsync(path);
            Assert.That(result, Does.StartWith("data:image/webp;base64,"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ToDataUriAsync_GifFile_ReturnsCorrectMimeType()
    {
        var path = CreateTempFile(".gif", [0x47, 0x49, 0x46, 0x38]);
        try
        {
            var result = await ImageDataUriHelper.ToDataUriAsync(path);
            Assert.That(result, Does.StartWith("data:image/gif;base64,"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task ToDataUriAsync_UnknownExtension_ReturnsOctetStream()
    {
        var path = CreateTempFile(".bmp", [0x42, 0x4D]);
        try
        {
            var result = await ImageDataUriHelper.ToDataUriAsync(path);
            Assert.That(result, Does.StartWith("data:application/octet-stream;base64,"));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void GetMimeType_ReturnsCorrectTypes()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ImageDataUriHelper.GetMimeType("image.png"), Is.EqualTo("image/png"));
            Assert.That(ImageDataUriHelper.GetMimeType("image.jpg"), Is.EqualTo("image/jpeg"));
            Assert.That(ImageDataUriHelper.GetMimeType("image.jpeg"), Is.EqualTo("image/jpeg"));
            Assert.That(ImageDataUriHelper.GetMimeType("image.webp"), Is.EqualTo("image/webp"));
            Assert.That(ImageDataUriHelper.GetMimeType("image.gif"), Is.EqualTo("image/gif"));
            Assert.That(ImageDataUriHelper.GetMimeType("image.tiff"), Is.EqualTo("application/octet-stream"));
        });
    }

    private static string CreateTempFile(string extension, byte[] content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"test-{Guid.NewGuid()}{extension}");
        File.WriteAllBytes(path, content);
        return path;
    }
}
