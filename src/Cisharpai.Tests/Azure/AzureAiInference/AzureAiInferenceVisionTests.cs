using System.Net;
using System.Text;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceVisionTests
{
    private const string SuccessResponseJson = """
        {
            "id": "chatcmpl-123",
            "object": "chat.completion",
            "model": "Phi-3-vision",
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

    private static (AzureAiInferenceChatCompletionClient, Func<string?>) CreateCapturingClient(string responseJson)
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

        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://mymodel.eastus.models.ai.azure.com/")
        };
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://mymodel.eastus.models.ai.azure.com/",
            ApiKey = "test-key"
        };
        return (new AzureAiInferenceChatCompletionClient(httpClient, options), () => capturedBody);
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
            Model: "Phi-3-vision");

        var response = await client.GetChatCompletionAsync(request);
        Assert.That(response.IsSuccess, Is.True);

        var body = getBody()!;
        Assert.That(body, Does.Contain("\"type\":\"image_url\""));
        Assert.That(body, Does.Contain("data:image/png;base64,abc123"));
        Assert.That(body, Does.Contain("What is this?"));
    }

    [Test]
    public async Task WithBase64Image_FactoryMethod_Uses_Provided_MediaType()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);
        var message = LlmMessage.WithBase64Image("Analyze:", "base64data==", "image/jpeg");

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "Phi-3-vision");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        Assert.That(body, Does.Contain("data:image/jpeg;base64,base64data=="));
        Assert.That(body, Does.Contain("Analyze:"));
    }

    [Test]
    public async Task Normal_String_Message_Sends_String_Content()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello!")],
            Model: "Phi-3-vision");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        Assert.That(body, Does.Contain("Hello!"));
        Assert.That(body, Does.Not.Contain("\"type\":\"image_url\""));
    }

    [Test]
    public async Task TextOnly_ContentParts_Sends_Array_Format()
    {
        var (client, getBody) = CreateCapturingClient(SuccessResponseJson);

        var message = new LlmMessage(
            LlmRole.User,
            string.Empty,
            ContentParts: [new TextContentPart("Hello")]);

        var request = new ChatCompletionRequest(
            Messages: [message],
            Model: "Phi-3-vision");

        await client.GetChatCompletionAsync(request);

        var body = getBody()!;
        Assert.That(body, Does.Contain("\"type\":\"text\""));
        Assert.That(body, Does.Contain("Hello"));
    }
}
