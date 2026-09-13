using System.Net;
using System.Text.Json;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceGroundedChatTests
{
    #region Response Fixtures

    private const string ChatResponseWithMarkers = """
        {
            "id": "chatcmpl-123",
            "model": "Phi-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "The capital is «cite:0»Paris«/cite»."
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 200,
                "completion_tokens": 30
            }
        }
        """;

    private const string ChatResponseNoMarkers = """
        {
            "id": "chatcmpl-456",
            "model": "Phi-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "I don't know the answer."
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 150,
                "completion_tokens": 10
            }
        }
        """;

    private const string ChatResponseMultipleMarkers = """
        {
            "id": "chatcmpl-789",
            "model": "Phi-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "«cite:0»Paris is in France«/cite», and «cite:1»Berlin is in Germany«/cite»."
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 250,
                "completion_tokens": 40
            }
        }
        """;

    private const string ChatResponseMalformedMarkers = """
        {
            "id": "chatcmpl-bad",
            "model": "Phi-4",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "The answer is «cite:0»Paris and also unclosed «cite:1»Berlin."
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 200,
                "completion_tokens": 30
            }
        }
        """;

    #endregion

    private static AzureAiInferenceClientOptions CreateOptions() => new()
    {
        ModelId = "Phi-4",
        ApiKey = "test-key",
        Endpoint = "https://test.inference.azure.com"
    };

    private static ChatCompletionRequest CreateRequest() =>
        new(Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")]);

    private static ChatCompletionRequest CreateRequestWithSystem() =>
        new(Messages:
        [
            new LlmMessage(LlmRole.System, "You are a helpful assistant."),
            new LlmMessage(LlmRole.User, "What is the capital of France?")
        ]);

    private static GroundedChatOptions CreateTextDocs() =>
        new(Documents:
        [
            new DocumentChunk(Id: "doc-1", Text: "Paris is the capital of France."),
            new DocumentChunk(Id: "doc-2", Text: "Berlin is the capital of Germany.")
        ]);

    private static GroundedChatOptions CreateDataDocs() =>
        new(Documents:
        [
            new DocumentChunk(
                Id: "doc-1",
                Data: new Dictionary<string, string>
                {
                    ["title"] = "France",
                    ["snippet"] = "Paris is the capital of France."
                }),
            new DocumentChunk(
                Id: "doc-2",
                Data: new Dictionary<string, string>
                {
                    ["title"] = "Germany",
                    ["snippet"] = "Berlin is the capital of Germany."
                })
        ]);

    private static async Task<(GroundedChatCompletionResponse Response, string? CapturedBody)> ExecuteGroundedChat(
        string responseJson,
        GroundedChatOptions? options = null,
        ChatCompletionRequest? request = null)
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (req, _) =>
        {
            capturedBody = await req.Content!.ReadAsStringAsync(CancellationToken.None);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            request ?? CreateRequest(),
            options ?? CreateTextDocs());

        return (response, capturedBody);
    }

    #region Feature Registration

    [Test]
    public void ExposesGroundedChatFeature()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var feature = client.Features.Get<IGroundedChatFeature>();

        Assert.Multiple(() =>
        {
            Assert.That(feature, Is.Not.Null);
            Assert.That(feature, Is.SameAs(client));
        });
    }

    #endregion

    #region GroundingKind

    [Test]
    public async Task GroundedChat_ReturnsGroundingKindSynthesized()
    {
        var (response, _) = await ExecuteGroundedChat(ChatResponseWithMarkers);

        Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.Synthesized));
    }

    [Test]
    public async Task GroundedChat_ErrorResponse_StillHasSynthesizedKind()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error": {"message": "Server error"}}""")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.Synthesized));
        });
    }

    #endregion

    #region Request Building — grounding injected

    [Test]
    public async Task GroundedChat_InjectsGroundingIntoSystemMessage()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(ChatResponseNoMarkers);

        Assert.That(capturedBody, Is.Not.Null);
        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");

        var systemMsg = messages.EnumerateArray()
            .FirstOrDefault(e => e.TryGetProperty("role", out var r) && r.GetString() == "system");

        Assert.Multiple(() =>
        {
            Assert.That(systemMsg.ValueKind, Is.Not.EqualTo(JsonValueKind.Undefined));
            Assert.That(systemMsg.GetProperty("content").GetString(), Does.Contain("REFERENCE DOCUMENTS"));
            Assert.That(systemMsg.GetProperty("content").GetString(), Does.Contain("doc-1"));
        });
    }

    [Test]
    public async Task GroundedChat_AppendsToExistingSystemMessage()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(
            ChatResponseNoMarkers,
            request: CreateRequestWithSystem());

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");

        var systemMsg = messages.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "system");

        var content = systemMsg.GetProperty("content").GetString()!;
        Assert.Multiple(() =>
        {
            Assert.That(content, Does.StartWith("You are a helpful assistant."));
            Assert.That(content, Does.Contain("REFERENCE DOCUMENTS"));
        });
    }

    [Test]
    public async Task GroundedChat_PreservesUserMessage()
    {
        var (_, capturedBody) = await ExecuteGroundedChat(ChatResponseNoMarkers);

        var doc = JsonDocument.Parse(capturedBody!);
        var messages = doc.RootElement.GetProperty("messages");

        var userMsg = messages.EnumerateArray()
            .First(e => e.TryGetProperty("role", out var r) && r.GetString() == "user");

        Assert.That(userMsg.GetProperty("content").GetString(),
            Is.EqualTo("What is the capital of France?"));
    }

    #endregion

    #region Response Mapping — markers stripped

    [Test]
    public async Task GroundedChat_StripsMarkers_ReturnsCleanContent()
    {
        var (response, _) = await ExecuteGroundedChat(ChatResponseWithMarkers);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("The capital is Paris."));
            Assert.That(response.Content, Does.Not.Contain("«"));
            Assert.That(response.Content, Does.Not.Contain("»"));
        });
    }

    [Test]
    public async Task GroundedChat_ExtractsCitations_WithCorrectOffsets()
    {
        var (response, _) = await ExecuteGroundedChat(ChatResponseWithMarkers);

        Assert.That(response.Citations, Has.Count.EqualTo(1));
        var citation = response.Citations[0];
        Assert.Multiple(() =>
        {
            Assert.That(citation.Text, Is.EqualTo("Paris"));
            Assert.That(citation.Start, Is.EqualTo(15));
            Assert.That(citation.End, Is.EqualTo(20));
            Assert.That(citation.Sources[0].Id, Is.EqualTo("doc-1"));
        });
    }

    [Test]
    public async Task GroundedChat_MultipleCitations_AllExtracted()
    {
        var (response, _) = await ExecuteGroundedChat(ChatResponseMultipleMarkers);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("Paris is in France, and Berlin is in Germany."));
            Assert.That(response.Citations, Has.Count.EqualTo(2));
            Assert.That(response.Citations[0].Sources[0].Id, Is.EqualTo("doc-1"));
            Assert.That(response.Citations[1].Sources[0].Id, Is.EqualTo("doc-2"));
        });
    }

    [Test]
    public async Task GroundedChat_PopulatesSourceData_ForStructuredDocs()
    {
        var (response, _) = await ExecuteGroundedChat(
            ChatResponseMultipleMarkers,
            options: CreateDataDocs());

        var source = response.Citations[0].Sources[0];
        Assert.Multiple(() =>
        {
            Assert.That(source.Data, Is.Not.Null);
            Assert.That(source.Data!["title"], Is.EqualTo("France"));
        });
    }

    #endregion

    #region Graceful Degradation

    [Test]
    public async Task GroundedChat_NoMarkers_ReturnsContentWithZeroCitations()
    {
        var (response, _) = await ExecuteGroundedChat(ChatResponseNoMarkers);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Is.EqualTo("I don't know the answer."));
            Assert.That(response.Citations, Is.Empty);
        });
    }

    [Test]
    public async Task GroundedChat_MalformedMarkers_GracefulDegradation()
    {
        var (response, _) = await ExecuteGroundedChat(ChatResponseMalformedMarkers);

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.True);
            Assert.That(response.Content, Does.Not.Contain("«/cite»"));
        });
    }

    #endregion

    #region Validation

    [Test]
    public async Task GroundedChat_ReturnsError_WhenNoDocuments()
    {
        var (response, _) = await ExecuteGroundedChat(
            ChatResponseNoMarkers,
            options: new GroundedChatOptions(Documents: []));

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.ErrorMessage, Does.Contain("document"));
        });
    }

    [Test]
    public async Task GroundedChat_ReturnsError_OnHttpFailure()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"error": {"message": "Server error"}}""")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.inference.azure.com/") };
        var client = new AzureAiInferenceChatCompletionClient(httpClient, CreateOptions());

        var response = await client.GetGroundedChatCompletionAsync(
            CreateRequest(),
            CreateTextDocs());

        Assert.Multiple(() =>
        {
            Assert.That(response.IsSuccess, Is.False);
            Assert.That(response.GroundingKind, Is.EqualTo(GroundingKind.Synthesized));
        });
    }

    #endregion

    #region CitationMode Semantics

    [Test]
    public async Task GroundedChat_AccurateModeWorks_SameAsFast()
    {
        var options = new GroundedChatOptions(
            Documents: CreateTextDocs().Documents,
            CitationMode: CitationMode.Accurate);

        var (response, _) = await ExecuteGroundedChat(ChatResponseWithMarkers, options: options);

        Assert.That(response.IsSuccess, Is.True);
    }

    [Test]
    public async Task GroundedChat_EnabledModeWorks()
    {
        var options = new GroundedChatOptions(
            Documents: CreateTextDocs().Documents,
            CitationMode: CitationMode.Enabled);

        var (response, _) = await ExecuteGroundedChat(ChatResponseWithMarkers, options: options);

        Assert.That(response.IsSuccess, Is.True);
    }

    #endregion
}
