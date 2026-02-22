using System.Net;
using System.Text;
using System.Text.Json;
using Cisharpai.Models;
using Cisharpai.OpenAi;

namespace Cisharpai.Tests.OpenAi;

public sealed class OpenAiVisionTests
{
    private const string SuccessResponseJson = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "model": "gpt-4o",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "I see an image."
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 100,
                "completion_tokens": 10,
                "total_tokens": 110
            }
        }
        """;

    private static (OpenAiChatCompletionClient, Func<string?>) CreateCapturingClient(string responseJson)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { ApiKey = "test-key" };
        return (new OpenAiChatCompletionClient(httpClient, options), () => capturedBody);
    }

    [Test]
    public async Task Base64Image_ContentPart_Sends_DataUri()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var message = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts:
            [
                new TextContentPart("What is this?"),
                new ImageBase64ContentPart("abc123", "image/png")
            ]);

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "gpt-4o");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody()!;
        Assert.That(body, Does.Contain("\"type\":\"image_url\""));
        Assert.That(body, Does.Contain("data:image/png;base64,abc123"));
        Assert.That(body, Does.Contain("What is this?"));
    }

    [Test]
    public async Task WithImage_FactoryMethod_Sends_ImageUrl_Content_Parts()
    {
        // Create a temporary image file for testing
        var tempPath = Path.GetTempFileName() + ".png";
        try
        {
            // Write minimal PNG content (just some bytes for test)
            await File.WriteAllBytesAsync(tempPath, new byte[] { 0x89, 0x50, 0x4E, 0x47 }); // PNG magic bytes

            var (client, getBody) = CreateCapturingClient(SuccessResponseJson);
            var message = LlmMessage.WithImage("Describe:", tempPath);

            var request = new ChatCompletionRequest(
                Messages: [message],
                Model: "gpt-4o");

            var response = await client.GetChatCompletionAsync(request);
            Assert.That(response.IsSuccess, Is.True);

            var body = getBody()!;
            Assert.That(body, Does.Contain("\"type\":\"image_url\""));
            Assert.That(body, Does.Contain("data:image/png;base64,"));
            Assert.That(body, Does.Contain("Describe:"));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Test]
    public async Task TextOnly_ContentParts_Sends_Text_Part_Array()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var message = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts: [new TextContentPart("Hello")]);

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "gpt-4o");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody()!;
        // Content should be serialized as array with type:text
        Assert.That(body, Does.Contain("\"type\":\"text\""));
        Assert.That(body, Does.Contain("Hello"));
    }

    [Test]
    public async Task Normal_String_Message_Sends_String_Content()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello!")],
            Model: "gpt-4o");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        // For string content, it should be serialized as a plain string
        Assert.That(body, Does.Contain("Hello!"));
        Assert.That(body, Does.Not.Contain("\"type\":\"image_url\""));
    }

    [Test]
    public async Task WithBase64Image_FactoryMethod_Uses_Provided_MediaType()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);
        var message = LlmMessage.WithBase64Image("Analyze:", "base64data==", "image/jpeg");

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "gpt-4o");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        Assert.That(body, Does.Contain("data:image/jpeg;base64,base64data=="));
        Assert.That(body, Does.Contain("Analyze:"));
    }
}
