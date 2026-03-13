using System.Net;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

/// <summary>
/// Tests for Cohere vision handling: image content parts are skipped silently
/// because Cohere chat does not support images.
/// </summary>
public sealed class CohereVisionTests
{
    private const string SuccessResponseJson = """
        {
            "id": "abc-123",
            "finish_reason": "COMPLETE",
            "message": {
                "role": "assistant",
                "content": [
                    {
                        "type": "text",
                        "text": "I can only see text"
                    }
                ]
            },
            "usage": {
                "billed_units": {
                    "input_tokens": 10,
                    "output_tokens": 8
                },
                "tokens": {
                    "input_tokens": 100,
                    "output_tokens": 8
                }
            }
        }
        """;

    private static CohereChatCompletionClient CreateClient(string responseJson)
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { ApiKey = "test-key" };
        return new CohereChatCompletionClient(httpClient, options);
    }

    private static CohereChatCompletionClient CreateCapturingClient(string responseJson, out Func<string?> getBody)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, ct) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.cohere.com/v2/") };
        var options = new CohereClientOptions { ApiKey = "test-key" };
        getBody = () => capturedBody;
        return new CohereChatCompletionClient(httpClient, options);
    }

    [Test]
    public async Task Mixed_TextAndImage_ContentParts_Sends_OnlyText()
    {
        var client = CreateCapturingClient(SuccessResponseJson, out var getBody);

        var request = new ChatCompletionRequest(
            Messages:
            [
                LlmMessage.WithImage("Describe this image:", "/fake/path/image.png")
            ],
            Model: "command-a-03-2025");

        await client.GetChatCompletionAsync(request);

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Is.Not.Null);

            // Should only contain the text part, not any image data
            Assert.That(body, Does.Contain("Describe this image:"));
            Assert.That(body, Does.Not.Contain("image_url"));
            Assert.That(body, Does.Not.Contain("base64"));
        });
    }

    [Test]
    public async Task ImageOnly_ContentParts_Sends_EmptyContent()
    {
        var client = CreateCapturingClient(SuccessResponseJson, out var getBody);

        // A message that only has an image (no text)
        var imageOnlyMessage = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts: [new ImageFileContentPart("/fake/image.png")]);

        var request = new ChatCompletionRequest(
            Messages: [imageOnlyMessage],
            Model: "command-a-03-2025");

        // Should succeed without throwing
        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Is.Not.Null);
            // Content should be empty (no text parts)
            Assert.That(body, Does.Not.Contain("image_url"));
        });
    }

    [Test]
    public async Task Base64Image_ContentParts_Skipped_OnlyTextSent()
    {
        var client = CreateCapturingClient(SuccessResponseJson, out var getBody);

        var messageWithBase64 = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts:
            [
                new TextContentPart("What do you see?"),
                new ImageBase64ContentPart("abc123base64", "image/png")
            ]);

        var request = new ChatCompletionRequest(
            Messages: [messageWithBase64],
            Model: "command-a-03-2025");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Is.Not.Null);
            Assert.That(body, Does.Contain("What do you see?"));
            Assert.That(body, Does.Not.Contain("abc123base64"));
        });
    }

    [Test]
    public async Task Multiple_TextParts_Concatenated()
    {
        var client = CreateCapturingClient(SuccessResponseJson, out var getBody);

        var messageWithMultipleText = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts:
            [
                new TextContentPart("First part. "),
                new ImageFileContentPart("/img.png"),
                new TextContentPart("Second part.")
            ]);

        var request = new ChatCompletionRequest(
            Messages: [messageWithMultipleText],
            Model: "command-a-03-2025");

        await client.GetChatCompletionAsync(request);

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Is.Not.Null);
            // Both text parts should be concatenated
            Assert.That(body, Does.Contain("First part."));
            Assert.That(body, Does.Contain("Second part."));
        });
    }

    [Test]
    public async Task Normal_Message_Without_ContentParts_Works_As_Before()
    {
        var client = CreateCapturingClient(SuccessResponseJson, out var getBody);

        var request = new ChatCompletionRequest(
            Messages:
            [
                new LlmMessage(LlmRole.User, "Hello, world!")
            ],
            Model: "command-a-03-2025");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Is.Not.Null);
            Assert.That(body, Does.Contain("Hello, world!"));
        });
    }

    [Test]
    public async Task WithImage_FactoryMethod_OnlyTextSentToCohere()
    {
        var client = CreateCapturingClient(SuccessResponseJson, out var getBody);

        // Use the LlmMessage.WithImage factory (which sets ContentParts)
        var msg = LlmMessage.WithImage("Describe:", "/some/image.jpg");

        var request = new ChatCompletionRequest(
            Messages: [msg],
            Model: "command-a-03-2025");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody();
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("Describe:"));
            Assert.That(body, Does.Not.Contain("image_url"));
        });
    }
}
