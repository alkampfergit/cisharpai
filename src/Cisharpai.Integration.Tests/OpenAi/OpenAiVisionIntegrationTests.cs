using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.OpenAi;

public sealed class OpenAiVisionIntegrationTests
{
    // Minimal 64x64 solid red PNG (generated programmatically, not a checked-in binary)
    // This is a tiny but valid PNG with a red 4x4 image in RGBA format
    private static readonly byte[] RedPngBytes = CreateMinimalRedPng();

    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IChatCompletionClient CreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.OpenAiTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.OpenAiTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAiClient(options => { options.ApiKey = apiKey!; });

        var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IChatCompletionClient>();
    }

    [Test]
    public async Task Can_Describe_Image_With_Base64()
    {
        var client = CreateClient();
        var base64 = Convert.ToBase64String(RedPngBytes);

        var message = LlmMessage.WithBase64Image(
            "What color is the main color in this image? Reply with just the color name.",
            base64,
            "image/png");

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "gpt-4.1-nano",
            MaxTokens: 100);

        var response = await client.GetChatCompletionAsync(request);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True, response.ErrorMessage);
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        });
    }

    [Test]
    public async Task Can_Describe_Image_With_File_Path()
    {
        var tempPath = Path.GetTempFileName() + ".png";
        try
        {
            await File.WriteAllBytesAsync(tempPath, RedPngBytes);

            var client = CreateClient();
            var message = LlmMessage.WithImage(
                "What color is the main color in this image? Reply with just the color name.",
                tempPath);

            var request = new ChatCompletionRequest(
                Messages: [message],
                Model: "gpt-4.1-nano",
                MaxTokens: 100);

            var response = await client.GetChatCompletionAsync(request);

            Assert.Multiple(() =>
            {
                Assert.That(response.IsSuccess, Is.True, response.ErrorMessage);
                Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
            });
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    /// <summary>
    /// Creates a minimal valid PNG: a 4x4 solid red image.
    /// </summary>
    private static byte[] CreateMinimalRedPng()
    {
        // This is a pre-computed minimal valid PNG (4x4 red image)
        // Generated from Python: img = Image.new('RGB', (4,4), color=(255,0,0)); img.save(...)
        return Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAADklEQVQI12P4z8BQDwAEgAF/QualIQAAAABJRU5ErkJggg==");
    }
}
