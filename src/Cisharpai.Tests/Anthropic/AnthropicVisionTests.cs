using System.Net;
using System.Text;
using Cisharpai.Models;
using Cisharpai.Anthropic;

namespace Cisharpai.Tests.Anthropic;

public sealed class AnthropicVisionTests
{
    private const string SuccessResponseJson = """
        {
            "id": "msg-123",
            "type": "message",
            "role": "assistant",
            "model": "claude-3-5-sonnet-20241022",
            "content": [{"type": "text", "text": "I see an image."}],
            "stop_reason": "end_turn",
            "usage": {"input_tokens": 100, "output_tokens": 10}
        }
        """;

    private static (AnthropicChatCompletionClient, Func<string?>) CreateCapturingClient(string responseJson)
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

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.anthropic.com/v1/") };
        var options = new AnthropicClientOptions { ApiKey = "test-key" };
        return (new AnthropicChatCompletionClient(httpClient, options), () => capturedBody);
    }

    [Test]
    public async Task Base64Image_ContentPart_Sends_Raw_Base64_Not_DataUri()
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
            Model: "claude-3-5-sonnet-20241022");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody()!;
        // Anthropic uses raw base64, NOT data URIs
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"type\":\"image\""));
            Assert.That(body, Does.Contain("\"type\":\"base64\""));
            Assert.That(body, Does.Contain("abc123"));
            Assert.That(body, Does.Not.Contain("data:image/png;base64,"));
            Assert.That(body, Does.Contain("\"media_type\":\"image/png\""));
        });
    }

    [Test]
    public async Task Base64Image_ContentPart_Uses_Source_Format()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var message = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts:
            [
                new TextContentPart("Describe:"),
                new ImageBase64ContentPart("base64data==", "image/jpeg")
            ]);

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "claude-3-5-sonnet-20241022");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"source\""));
            Assert.That(body, Does.Contain("\"media_type\":\"image/jpeg\""));
            Assert.That(body, Does.Contain("\"data\":\"base64data==\""));
        });
    }

    [Test]
    public async Task ImageFile_ContentPart_Reads_File_And_Encodes_As_Base64()
    {
        var tempPath = Path.GetTempFileName() + ".png";
        try
        {
            var imgBytes = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A }; // PNG bytes
            await File.WriteAllBytesAsync(tempPath, imgBytes);

            var (client, getBody) = CreateCapturingClient(SuccessResponseJson);
            var message = LlmMessage.WithImage("Describe:", tempPath);

            var request = new ChatCompletionRequest(
                Messages: [message],
                Model: "claude-3-5-sonnet-20241022");

            var response = await client.GetChatCompletionAsync(request);
            Assert.That(response.IsSuccess, Is.True);

            var body = getBody()!;
            Assert.Multiple(() =>
            {
                Assert.That(body, Does.Contain("\"type\":\"image\""));
                Assert.That(body, Does.Contain("\"type\":\"base64\""));
            });
            // Should be actual base64 of the file bytes
            var expectedBase64 = Convert.ToBase64String(imgBytes);
            Assert.That(body, Does.Contain($"\"data\":\"{expectedBase64}\""));
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }

    [Test]
    public async Task Normal_String_Message_Sends_As_String()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello!")],
            Model: "claude-3-5-sonnet-20241022");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("Hello!"));
            Assert.That(body, Does.Not.Contain("\"type\":\"image\""));
        });
    }

    [Test]
    public async Task Image_Content_Block_Has_Correct_Structure()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var message = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts:
            [
                new ImageBase64ContentPart("mydata", "image/webp")
            ]);

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "claude-3-5-sonnet-20241022");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        // Anthropic image block: {"type":"image","source":{"type":"base64","media_type":"...","data":"..."}}
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"type\":\"image\""));
            Assert.That(body, Does.Contain("\"media_type\":\"image/webp\""));
            Assert.That(body, Does.Contain("\"data\":\"mydata\""));
        });
    }
}
