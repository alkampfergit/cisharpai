using System.Net;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiChatCompletionClientTests
{
    [Test]
    public async Task GetChatCompletionAsync_MapsResponseToSharedModel()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AzureResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4"));

        Assert.Multiple(() =>
        {
            Assert.That(response.Content, Is.EqualTo("Hello there!"));
            Assert.That(response.Model, Is.EqualTo("gpt-4"));
            Assert.That(response.PromptTokens, Is.EqualTo(10));
            Assert.That(response.CompletionTokens, Is.EqualTo(20));
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_HttpError_ReturnsErrorResponse()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error":{"message":"Internal error"}}""",
                    System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hello")],
            Model: "gpt-4"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("500"));
            Assert.That(response.Content, Is.EqualTo(string.Empty));
            Assert.That(response.Model, Is.EqualTo(string.Empty));
        });
    }

    [Test]
    public async Task GetChatCompletionAsync_FinishReasonLength_ReturnsFailureWithIncompleteReason()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(AzureLengthResponseJson, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://myresource.openai.azure.com/") };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com/",
            DeploymentName = "gpt-4",
            ApiVersion = "2024-10-21",
            ApiKey = "test-key"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Write a long answer")],
            Model: "gpt-4",
            MaxTokens: 1));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.Status, Is.EqualTo("length"));
            Assert.That(response.IncompleteReason, Is.EqualTo("length"));
            Assert.That(response.ErrorMessage, Does.Contain("finish_reason"));
            Assert.That(response.Content, Is.EqualTo("This"));
            Assert.That(response.Model, Is.EqualTo("gpt-4"));
            Assert.That(response.PromptTokens, Is.EqualTo(12));
            Assert.That(response.CompletionTokens, Is.EqualTo(1));
        });
    }

    private const string AzureResponseJson = """
        {
            "model": "gpt-4",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "Hello there!"
                    }
                }
            ],
            "usage": {
                "prompt_tokens": 10,
                "completion_tokens": 20
            }
        }
        """;

    private const string AzureLengthResponseJson = """
        {
            "model": "gpt-4",
            "choices": [
                {
                    "message": {
                        "role": "assistant",
                        "content": "This"
                    },
                    "finish_reason": "length"
                }
            ],
            "usage": {
                "prompt_tokens": 12,
                "completion_tokens": 1
            }
        }
        """;
}
