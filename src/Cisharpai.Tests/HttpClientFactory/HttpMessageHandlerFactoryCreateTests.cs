using System.Net;
using Cisharpai.Anthropic;
using Cisharpai.Azure.AzureAiInference;
using Cisharpai.Azure.AzureOpenAi;
using Cisharpai.Cohere;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.OpenAi;
using Microsoft.Extensions.Http;
using NSubstitute;

namespace Cisharpai.Tests.HttpClientFactory;

public sealed class HttpMessageHandlerFactoryCreateTests
{
    private const string SimpleChatResponseJson = """
        {
            "id": "chatcmpl-1",
            "model": "test-model",
            "choices": [
                {
                    "index": 0,
                    "message": { "role": "assistant", "content": "Hello" },
                    "finish_reason": "stop"
                }
            ],
            "usage": { "prompt_tokens": 5, "completion_tokens": 3, "total_tokens": 8 }
        }
        """;

    private const string CohereResponseJson = """
        {
            "id": "chat-1",
            "message": { "role": "assistant", "content": [{ "type": "text", "text": "Hello" }] },
            "finish_reason": "COMPLETE",
            "usage": {
                "billed_units": { "input_tokens": 5, "output_tokens": 3 },
                "tokens": { "input_tokens": 5, "output_tokens": 3 }
            }
        }
        """;

    private const string AnthropicResponseJson = """
        {
            "id": "msg-1",
            "type": "message",
            "role": "assistant",
            "model": "claude-haiku-4-5-20251001",
            "content": [{ "type": "text", "text": "Hello" }],
            "stop_reason": "end_turn",
            "usage": { "input_tokens": 5, "output_tokens": 3 }
        }
        """;

    private static IHttpMessageHandlerFactory BuildFactory(string responseBody, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            }));

        var factory = Substitute.For<IHttpMessageHandlerFactory>();
        factory.CreateHandler(Arg.Any<string>()).Returns(handler);
        return factory;
    }

    [Test]
    public async Task OpenAiChatClient_Create_UsesPooledHandler()
    {
        var factory = BuildFactory(SimpleChatResponseJson);
        var options = new OpenAiClientOptions { ApiKey = "sk-test", DefaultModel = "gpt-4.1-nano" };

        var client = OpenAiChatCompletionClient.Create(factory, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello"));
        });
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task OpenAiChatClient_Create_CustomHandlerName()
    {
        var factory = BuildFactory(SimpleChatResponseJson);
        var options = new OpenAiClientOptions { ApiKey = "sk-test", DefaultModel = "gpt-4.1-nano" };

        OpenAiChatCompletionClient.Create(factory, options, handlerName: "my-pool");

        factory.Received(1).CreateHandler("my-pool");
    }

    [Test]
    public async Task OpenAiChatClient_Create_ExposesFeatures()
    {
        var factory = BuildFactory(SimpleChatResponseJson);
        var client = OpenAiChatCompletionClient.Create(factory, new OpenAiClientOptions { ApiKey = "x", DefaultModel = "m" });

        Assert.That(client.Features.Get<IStreamingChatFeature>(), Is.Not.Null);
        await Task.CompletedTask;
    }

    [Test]
    public async Task OpenAiEmbeddingClient_Create_UsesPooledHandler()
    {
        const string embeddingResponse = """
            {
                "object": "list",
                "model": "text-embedding-3-small",
                "data": [{ "object": "embedding", "index": 0, "embedding": [0.1, 0.2] }],
                "usage": { "prompt_tokens": 3, "total_tokens": 3 }
            }
            """;
        var factory = BuildFactory(embeddingResponse);
        var options = new OpenAiClientOptions { ApiKey = "sk-test", DefaultModel = "text-embedding-3-small" };

        var client = OpenAiEmbeddingClient.Create(factory, options);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(Input: ["hello"]));

        Assert.That(response.IsSuccess, Is.True);
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task AnthropicChatClient_Create_UsesPooledHandler()
    {
        var factory = BuildFactory(AnthropicResponseJson);
        var options = new AnthropicClientOptions { ApiKey = "test-key", DefaultModel = "claude-haiku-4-5-20251001" };

        var client = AnthropicChatCompletionClient.Create(factory, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello"));
        });
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task CohereChatClient_Create_UsesPooledHandler()
    {
        var factory = BuildFactory(CohereResponseJson);
        var options = new CohereClientOptions { ApiKey = "test-key", DefaultModel = "command-a-03-2025" };

        var client = CohereChatCompletionClient.Create(factory, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello"));
        });
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task CohereEmbeddingClient_Create_UsesPooledHandler()
    {
        const string embeddingResponse = """
            {
                "id": "emb-1",
                "embeddings": { "float": [[0.1, 0.2]] },
                "texts": ["hello"],
                "meta": { "billed_units": { "input_tokens": 1 } }
            }
            """;
        var factory = BuildFactory(embeddingResponse);
        var options = new CohereClientOptions { ApiKey = "test-key", DefaultModel = "embed-v4" };

        var client = CohereEmbeddingClient.Create(factory, options);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(Input: ["hello"]));

        Assert.That(response.IsSuccess, Is.True);
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task AzureOpenAiChatClient_Create_UsesPooledHandler()
    {
        var factory = BuildFactory(SimpleChatResponseJson);
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://my-resource.openai.azure.com",
            ApiKey = "az-key",
            DeploymentName = "gpt-4o"
        };

        var client = AzureOpenAiChatCompletionClient.Create(factory, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")],
            Model: "gpt-4o"));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello"));
        });
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task AzureOpenAiEmbeddingClient_Create_UsesPooledHandler()
    {
        const string azureEmbeddingResponse = """
            {
                "object": "list",
                "model": "text-embedding-ada-002",
                "data": [{ "object": "embedding", "index": 0, "embedding": [0.1, 0.2] }],
                "usage": { "prompt_tokens": 2, "total_tokens": 2 }
            }
            """;
        var factory = BuildFactory(azureEmbeddingResponse);
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://my-resource.openai.azure.com",
            ApiKey = "az-key",
            DeploymentName = "text-embedding-ada-002"
        };

        var client = AzureOpenAiEmbeddingClient.Create(factory, options);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(Input: ["hello"]));

        Assert.That(response.IsSuccess, Is.True);
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task AzureAiInferenceChatClient_Create_UsesPooledHandler()
    {
        var factory = BuildFactory(SimpleChatResponseJson);
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-model.inference.azure.com",
            ApiKey = "az-key",
            ModelId = "Phi-3-mini"
        };

        var client = AzureAiInferenceChatCompletionClient.Create(factory, options);

        var response = await client.GetChatCompletionAsync(new ChatCompletionRequest(
            Messages: [new LlmMessage(LlmRole.User, "Hi")]));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Hello"));
        });
        factory.Received(1).CreateHandler("cisharpai");
    }

    [Test]
    public async Task AzureAiInferenceEmbeddingClient_Create_UsesPooledHandler()
    {
        const string inferenceEmbeddingResponse = """
            {
                "object": "list",
                "model": "cohere-embed-v3",
                "data": [{ "object": "embedding", "index": 0, "embedding": [0.1, 0.2] }],
                "usage": { "prompt_tokens": 2, "total_tokens": 2 }
            }
            """;
        var factory = BuildFactory(inferenceEmbeddingResponse);
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-model.inference.azure.com",
            ApiKey = "az-key",
            ModelId = "cohere-embed-v3"
        };

        var client = AzureAiInferenceEmbeddingClient.Create(factory, options);

        var response = await client.GetEmbeddingsAsync(new EmbeddingRequest(Input: ["hello"]));

        Assert.That(response.IsSuccess, Is.True);
        factory.Received(1).CreateHandler("cisharpai");
    }
}
