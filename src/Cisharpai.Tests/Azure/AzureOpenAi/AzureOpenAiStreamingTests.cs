using System.Net;
using System.Text;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiStreamingTests
{
    private const string StreamSse = """
        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"role":"assistant","content":""},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"content":"Hello"},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{"content":", world!"},"finish_reason":null}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[{"index":0,"delta":{},"finish_reason":"stop"}]}

        data: {"id":"chatcmpl-abc","model":"gpt-4o","choices":[],"usage":{"prompt_tokens":10,"completion_tokens":5,"total_tokens":15}}

        data: [DONE]
        """;

    private static AzureOpenAiChatCompletionClient CreateStreamingClient(string sseContent, string deployment = "gpt-4o-deployment")
    {
        var bytes = Encoding.UTF8.GetBytes(sseContent);
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var stream = new MemoryStream(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            });
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://myaccount.openai.azure.com/")
        };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myaccount.openai.azure.com/",
            ApiKey = "test-key",
            DeploymentName = deployment
        };
        return new AzureOpenAiChatCompletionClient(httpClient, options);
    }

    private static (AzureOpenAiChatCompletionClient, Func<string?>) CreateCapturingClient(string sseContent, string deployment = "gpt-4o-deployment")
    {
        string? capturedBody = null;
        var bytes = Encoding.UTF8.GetBytes(sseContent);
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync();
            var stream = new MemoryStream(bytes);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            };
        });
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://myaccount.openai.azure.com/")
        };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myaccount.openai.azure.com/",
            ApiKey = "test-key",
            DeploymentName = deployment
        };
        return (new AzureOpenAiChatCompletionClient(httpClient, options), () => capturedBody);
    }

    [Test]
    public async Task Streams_Text_Chunks_In_Order()
    {
        var client = CreateStreamingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Say hello")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;

        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var textChunks = chunks.Where(c => !string.IsNullOrEmpty(c.Content)).ToList();
        var combined = string.Concat(textChunks.Select(c => c.Content));
        Assert.That(combined, Is.EqualTo("Hello, world!"));
    }

    [Test]
    public async Task Final_Chunk_Has_FinishReason()
    {
        var client = CreateStreamingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var finishChunk = chunks.FirstOrDefault(c => c.FinishReason is not null);
        Assert.Multiple(() =>
        {
            Assert.That(finishChunk, Is.Not.Null);
            Assert.That(finishChunk!.FinishReason, Is.EqualTo("stop"));
        });
    }

    [Test]
    public async Task Usage_Chunk_Contains_Token_Counts()
    {
        var client = CreateStreamingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        var chunks = new List<ChatCompletionChunk>();
        await foreach (var chunk in feature.GetChatCompletionStreamAsync(request))
        {
            chunks.Add(chunk);
        }

        var usageChunk = chunks.FirstOrDefault(c => c.PromptTokens.HasValue);
        Assert.Multiple(() =>
        {
            Assert.That(usageChunk, Is.Not.Null);
            Assert.That(usageChunk!.PromptTokens, Is.EqualTo(10));
            Assert.That(usageChunk.CompletionTokens, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Sets_Stream_True_In_Request()
    {
        var (client, getBody) = CreateCapturingClient(StreamSse);

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "gpt-4o");

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        await foreach (var _ in feature.GetChatCompletionStreamAsync(request)) { }

        var body = getBody()!;
        Assert.That(body, Does.Contain("\"stream\":true"));
    }

    [Test]
    public void Feature_Is_Discoverable_Via_IStreamingChatFeature()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://myaccount.openai.azure.com/")
        };
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myaccount.openai.azure.com/",
            ApiKey = "test-key",
            DeploymentName = "deployment"
        };
        var client = new AzureOpenAiChatCompletionClient(httpClient, options);

        var feature = client.Features.Get<IStreamingChatFeature>();
        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    [Test]
    public async Task Reasoning_Model_Uses_MaxCompletionTokens()
    {
        var (client, getBody) = CreateCapturingClient(StreamSse, deployment: "o3-deployment");

        var request = new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "test")],
            Model: "o3",
            MaxTokens: 500);

        var feature = client.Features.Get<IStreamingChatFeature>()!;
        await foreach (var _ in feature.GetChatCompletionStreamAsync(request)) { }

        var body = getBody()!;
        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Contain("\"stream\":true"));
            Assert.That(body, Does.Contain("max_completion_tokens"));
            Assert.That(body, Does.Not.Contain("\"max_tokens\""));
        });
    }
}
