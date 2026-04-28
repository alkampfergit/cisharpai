using Cisharpai.Models;
using Cisharpai.Anthropic;
using Cisharpai.Tests.Common;
using Microsoft.Extensions.DependencyInjection;

namespace Cisharpai.Integration.Tests.Anthropic;

public sealed class AnthropicVisionIntegrationTests
{
    // Anthropic rejects very small images ("Could not process image"), so use a 256x256 solid red PNG.
    private static readonly byte[] RedPngBytes = PngGenerator.CreateSolidColorPng(256, 256, 255, 0, 0);

    [OneTimeSetUp]
    public void LoadEnvironment()
    {
        DotEnv.Load();
    }

    private static IChatCompletionClient CreateClient()
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
                Model: "claude-haiku-4-5-20251001",
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
}
