using Cisharpai.Models;
using Cisharpai.Rag.Models;
using Cisharpai.Rag.Packing;
using Cisharpai.Rag.Pipeline;
using Cisharpai.Testing;
using NSubstitute;

namespace Cisharpai.Tests.Rag.Pipeline;

[TestFixture]
public class ConversationalRagPipelineTests
{
    private static TextChunk MakeChunk(string docId, int index, string text) =>
        new(docId, index, 0, text.Length, text);

    private static ScoredChunk Scored(string docId, int index, string text, double score) =>
        new(MakeChunk(docId, index, text), score);

    private static IReadOnlyList<ScoredChunk> SampleChunks() =>
    [
        Scored("doc1", 0, "The capital of France is Paris.", 0.9),
        Scored("doc1", 1, "Paris is known for the Eiffel Tower.", 0.8),
        Scored("doc2", 0, "France is a country in Western Europe.", 0.7)
    ];

    [Test]
    public async Task AskAsync_FullPipeline_ReturnsAnswer()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var reranker = new FakeRerankerClient();
        reranker.DefaultResponse = FakeResponses.Rerank(3);

        var packer = Substitute.For<IContextPacker>();
        packer.PackAsync(Arg.Any<IReadOnlyList<ScoredChunk>>(), Arg.Any<ContextPackingOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var chunks = callInfo.ArgAt<IReadOnlyList<ScoredChunk>>(0);
                return new ContextPackingResult(chunks, [], 100, 3996);
            });

        var chatClient = new FakeChatCompletionClient();
        chatClient.DefaultGroundedChatResponse = FakeResponses.GroundedChat(
            "The capital of France is Paris.",
            [new Citation(0, 30, "The capital of France is Paris.",
                [new CitationSource("doc1_0")])]);

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithReranker(reranker)
            .WithContextPacker(packer)
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("What is the capital of France?",
            new RagPipelineOptions { PackingOptions = new ContextPackingOptions { TokenBudget = 4096 } });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Answer, Is.EqualTo("The capital of France is Paris."));
        Assert.That(result.Citations, Has.Count.EqualTo(1));
        Assert.That(result.RetrievedChunks, Has.Count.EqualTo(3));
        Assert.That(result.PackedChunks, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task AskAsync_RetrieverOnly_ReturnsChunksWithEmptyAnswer()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .Build();

        var result = await pipeline.AskAsync("What is the capital of France?");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Answer, Is.Empty);
        Assert.That(result.RetrievedChunks, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task AskAsync_ChatClientOnly_ReturnsAnswerWithNoChunks()
    {
        var chatClient = new FakeChatCompletionClient();
        chatClient.DefaultResponse = FakeResponses.Chat("Paris is the capital of France.");

        var pipeline = new RagPipelineBuilder()
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("What is the capital of France?");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Answer, Is.EqualTo("Paris is the capital of France."));
        Assert.That(result.RetrievedChunks, Is.Empty);
    }

    [Test]
    public async Task AskAsync_MultipleRetrievers_FusesResults()
    {
        var vectorRetriever = new FakeRetriever
        {
            DefaultResponse = new[]
            {
                Scored("doc1", 0, "Paris is the capital.", 0.9),
                Scored("doc2", 0, "Berlin is the capital.", 0.5)
            }
        };
        var bm25Retriever = new FakeRetriever
        {
            DefaultResponse = new[]
            {
                Scored("doc2", 0, "Berlin is the capital.", 0.8),
                Scored("doc3", 0, "Madrid is the capital.", 0.6)
            }
        };

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(vectorRetriever)
            .WithRetriever(bm25Retriever)
            .Build();

        var result = await pipeline.AskAsync("capital?");

        Assert.That(result.RetrievedChunks, Has.Count.EqualTo(3));
        // doc2 should rank highest (appears in both lists)
        Assert.That(result.RetrievedChunks[0].Chunk.DocumentId, Is.EqualTo("doc2"));
    }

    [Test]
    public async Task AskAsync_WithQueryTransformer_TransformsBeforeRetrieval()
    {
        var transformer = new FakeQueryTransformer
        {
            DefaultResponse = new[] { "transformed query" }
        };
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithQueryTransformer(transformer)
            .WithRetriever(retriever)
            .Build();

        await pipeline.AskAsync("original query");

        Assert.That(transformer.ReceivedQueries[0], Is.EqualTo("original query"));
        Assert.That(retriever.ReceivedQueries[0].Query, Is.EqualTo("transformed query"));
    }

    [Test]
    public async Task AskAsync_WithMultiQueryExpansion_RetrievesForEachQuery()
    {
        var transformer = new FakeQueryTransformer
        {
            DefaultResponse = new[] { "query A", "query B" }
        };
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithQueryTransformer(transformer)
            .WithRetriever(retriever)
            .Build();

        var result = await pipeline.AskAsync("original");

        Assert.That(retriever.CallCount, Is.EqualTo(2));
        Assert.That(result.ExpandedQueries, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task AskAsync_WithConversationRewriter_RewritesQuery()
    {
        var rewriterClient = new FakeChatCompletionClient();
        rewriterClient.DefaultResponse = FakeResponses.Chat("standalone query about capitals");

        var conversationRewriter = new ConversationQueryRewriter(rewriterClient);
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithConversationRewriter(conversationRewriter)
            .WithRetriever(retriever)
            .Build();

        var options = new RagPipelineOptions
        {
            ConversationHistory =
            [
                new LlmMessage(LlmRole.User, "Tell me about France."),
                new LlmMessage(LlmRole.Assistant, "France is a country in Europe.")
            ]
        };

        var result = await pipeline.AskAsync("What is its capital?", options);

        Assert.That(result.RewrittenQuery, Is.EqualTo("standalone query about capitals"));
        Assert.That(retriever.ReceivedQueries[0].Query, Is.EqualTo("standalone query about capitals"));
    }

    [Test]
    public async Task AskAsync_ConversationRewriter_NoHistory_SkipsRewrite()
    {
        var rewriterClient = new FakeChatCompletionClient();
        var conversationRewriter = new ConversationQueryRewriter(rewriterClient);
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithConversationRewriter(conversationRewriter)
            .WithRetriever(retriever)
            .Build();

        var result = await pipeline.AskAsync("standalone query");

        Assert.That(result.RewrittenQuery, Is.Null);
        Assert.That(rewriterClient.CallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task AskAsync_RerankerError_FallsBackToOriginalRanking()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var reranker = new FakeRerankerClient();
        reranker.DefaultResponse = FakeResponses.RerankError("rate limit");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithReranker(reranker)
            .Build();

        var result = await pipeline.AskAsync("test");

        Assert.That(result.RetrievedChunks, Has.Count.EqualTo(3));
        Assert.That(result.RetrievedChunks[0].Score, Is.EqualTo(0.9));
    }

    [Test]
    public async Task AskAsync_ChatError_ReturnsErrorResultWithProviderMessage()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var chatClient = new FakeChatCompletionClient();
        chatClient.DefaultGroundedChatResponse = GroundedChatCompletionResponse.Error("model overloaded");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("test",
            new RagPipelineOptions { PackingOptions = new ContextPackingOptions { TokenBudget = 4096 } });

        Assert.That(result.IsSuccess, Is.False);
        Assert.That(result.ErrorMessage, Is.EqualTo("model overloaded"));
        Assert.That(result.RetrievedChunks, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task AskAsync_NoPackerConfigured_PassesThroughAllChunks()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .Build();

        var result = await pipeline.AskAsync("test");

        Assert.That(result.PackedChunks, Has.Count.EqualTo(3));
        Assert.That(result.DroppedChunks, Is.Empty);
    }

    [Test]
    public async Task AskAsync_WithPacker_DropsChunks()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var packer = Substitute.For<IContextPacker>();
        packer.PackAsync(Arg.Any<IReadOnlyList<ScoredChunk>>(), Arg.Any<ContextPackingOptions>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var chunks = callInfo.ArgAt<IReadOnlyList<ScoredChunk>>(0);
                return new ContextPackingResult(
                    chunks.Take(2).ToList(),
                    [new DroppedChunk(chunks[2], 50, DropReason.BudgetExhausted)],
                    200,
                    3796);
            });

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithContextPacker(packer)
            .Build();

        var result = await pipeline.AskAsync("test",
            new RagPipelineOptions { PackingOptions = new ContextPackingOptions { TokenBudget = 4096 } });

        Assert.That(result.PackedChunks, Has.Count.EqualTo(2));
        Assert.That(result.DroppedChunks, Has.Count.EqualTo(1));
    }

    [Test]
    public void AskAsync_NullQuery_Throws()
    {
        var pipeline = new RagPipelineBuilder()
            .WithRetriever(FakeResponses.Retriever())
            .Build();

        Assert.ThrowsAsync<ArgumentNullException>(() => pipeline.AskAsync(null!));
    }

    [Test]
    public void AskAsync_WhitespaceQuery_Throws()
    {
        var pipeline = new RagPipelineBuilder()
            .WithRetriever(FakeResponses.Retriever())
            .Build();

        Assert.ThrowsAsync<ArgumentException>(() => pipeline.AskAsync("   "));
    }

    [Test]
    public async Task AskAsync_FallbackChatWithoutGroundedFeature_UsesPromptInjection()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat(
            "«cite:0»The capital of France is Paris.«/cite»");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("What is the capital of France?");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Answer, Is.EqualTo("The capital of France is Paris."));
        Assert.That(result.Citations, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task AskAsync_CustomSystemPrompt_IsUsed()
    {
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat("answer");

        var pipeline = new RagPipelineBuilder()
            .WithChatClient(chatClient)
            .Build();

        await pipeline.AskAsync("test",
            new RagPipelineOptions { SystemPrompt = "You are a pirate." });

        var request = chatClient.ReceivedRequests[0];
        Assert.That(request.Messages[0].Content, Is.EqualTo("You are a pirate."));
    }

    [Test]
    public async Task AskAsync_ModelAndTemperature_PassedThrough()
    {
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat("answer");

        var pipeline = new RagPipelineBuilder()
            .WithChatClient(chatClient)
            .Build();

        await pipeline.AskAsync("test",
            new RagPipelineOptions { Model = "gpt-4o", Temperature = 0.3 });

        var request = chatClient.ReceivedRequests[0];
        Assert.That(request.Model, Is.EqualTo("gpt-4o"));
        Assert.That(request.Temperature, Is.EqualTo(0.3));
    }

    [Test]
    public async Task AskAsync_ConversationHistory_IncludedInChatMessages()
    {
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat("answer");

        var pipeline = new RagPipelineBuilder()
            .WithChatClient(chatClient)
            .Build();

        var options = new RagPipelineOptions
        {
            ConversationHistory =
            [
                new LlmMessage(LlmRole.User, "previous question"),
                new LlmMessage(LlmRole.Assistant, "previous answer")
            ]
        };

        await pipeline.AskAsync("follow up", options);

        var messages = chatClient.ReceivedRequests[0].Messages;
        Assert.That(messages[0].Role, Is.EqualTo(LlmRole.System));
        Assert.That(messages[1].Content, Is.EqualTo("previous question"));
        Assert.That(messages[2].Content, Is.EqualTo("previous answer"));
        Assert.That(messages[3].Content, Is.EqualTo("follow up"));
    }

    [Test]
    public async Task AskAsync_EmptyRetrieverResults_ChatStillCalled()
    {
        var retriever = FakeResponses.Retriever();
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat("I don't have context.");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("unknown question");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Answer, Is.EqualTo("I don't have context."));
        Assert.That(result.RetrievedChunks, Is.Empty);
    }

    [Test]
    public void AskAsync_ZeroTopK_ThrowsArgumentOutOfRange()
    {
        var pipeline = new RagPipelineBuilder()
            .WithRetriever(FakeResponses.Retriever())
            .Build();

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            pipeline.AskAsync("test", new RagPipelineOptions { TopK = 0 }));
    }

    [Test]
    public void AskAsync_NegativeRerankerTopN_ThrowsArgumentOutOfRange()
    {
        var pipeline = new RagPipelineBuilder()
            .WithRetriever(FakeResponses.Retriever())
            .Build();

        Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            pipeline.AskAsync("test", new RagPipelineOptions { RerankerTopN = -1 }));
    }

    [Test]
    public async Task AskAsync_TransformerReturnsEmpty_PreservesOriginalQuery()
    {
        var transformer = new FakeQueryTransformer();
        transformer.EnqueueResponse([]);
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };

        var pipeline = new RagPipelineBuilder()
            .WithQueryTransformer(transformer)
            .WithRetriever(retriever)
            .Build();

        await pipeline.AskAsync("my query");

        Assert.That(retriever.ReceivedQueries[0].Query, Is.EqualTo("my query"));
    }

    [Test]
    public async Task AskAsync_NativeGroundedChat_DoesNotDuplicateContextInPrompt()
    {
        var retriever = new FakeRetriever { DefaultResponse = SampleChunks() };
        var chatClient = new FakeChatCompletionClient();
        chatClient.DefaultGroundedChatResponse = FakeResponses.GroundedChat("answer");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        await pipeline.AskAsync("test",
            new RagPipelineOptions { PackingOptions = new ContextPackingOptions { TokenBudget = 4096 } });

        var (request, _) = chatClient.ReceivedGroundedChatRequests[0];
        var systemMsg = request.Messages[0].Content;
        Assert.That(systemMsg, Does.Not.Contain("REFERENCE DOCUMENTS"));
    }

    [Test]
    public async Task AskAsync_PrePackedChunks_UsedWhenNoRetriever()
    {
        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat(
            "«cite:0»The capital is Paris.«/cite»");

        var prePackedChunks = SampleChunks();
        var pipeline = new RagPipelineBuilder()
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("What is the capital?",
            new RagPipelineOptions { PrePackedChunks = prePackedChunks });

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Answer, Is.EqualTo("The capital is Paris."));
        Assert.That(result.Citations, Has.Count.EqualTo(1));
        Assert.That(result.PackedChunks, Has.Count.EqualTo(3));
    }

    [Test]
    public async Task AskAsync_DocumentChunks_IncludeSourceFromMetadata()
    {
        var chunkWithSource = new TextChunk("doc1", 0, 0, 10, "chunk text",
            new Dictionary<string, object?> { ["source"] = "https://example.com/doc1", ["title"] = "Doc One" });
        var retriever = new FakeRetriever
        {
            DefaultResponse = [new ScoredChunk(chunkWithSource, 0.9)]
        };

        var chatClient = new FakeChatCompletionClient(FakeChatFeatures.Streaming);
        chatClient.DefaultResponse = FakeResponses.Chat("«cite:0»chunk text«/cite»");

        var pipeline = new RagPipelineBuilder()
            .WithRetriever(retriever)
            .WithChatClient(chatClient)
            .Build();

        var result = await pipeline.AskAsync("test");

        Assert.That(result.IsSuccess, Is.True);
        Assert.That(result.Citations, Has.Count.EqualTo(1));
    }
}
