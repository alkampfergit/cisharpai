using Cisharpai.Models;
using Cisharpai.Anthropic;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Anthropic;

public sealed class AnthropicVisionIntegrationTests
{
    // Minimal valid PNG: a 4x4 solid red image
    private static readonly byte[] RedPngBytes = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAQAAAAECAIAAAAmkwkpAAAADklEQVQI12P4z8BQDwAEgAF/QualIQAAAABJRU5ErkJggg==");

    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private IChatCompletionClient CreateClient()
    {
        var apiKey = Environment.GetEnvironmentVariable(DotEnv.AnthropicTestApiKey);
        Assert.That(apiKey, Is.Not.Null.And.Not.Empty,
            $"Environment variable {DotEnv.AnthropicTestApiKey} must be set.");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAnthropicClient(options => { options.ApiKey = apiKey!; });

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
            Model: "claude-haiku-4-5-20251001",
            MaxTokens: 100);

        var response = await client.GetChatCompletionAsync(request);

        Assert.That(response.IsSuccess, Is.True, response.ErrorMessage);
        Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
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
                Model: "claude-haiku-4-5-20251001",
                MaxTokens: 100);

            var response = await client.GetChatCompletionAsync(request);

            Assert.That(response.IsSuccess, Is.True, response.ErrorMessage);
            Assert.That(response.Content, Is.Not.Null.And.Not.Empty);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
