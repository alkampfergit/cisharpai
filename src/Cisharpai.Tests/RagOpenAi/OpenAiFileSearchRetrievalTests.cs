using System.Net;
using System.Text.Json;
using Cisharpai.OpenAi;
using Cisharpai.Rag;
using Cisharpai.Rag.OpenAi;

namespace Cisharpai.Tests.RagOpenAi;

public sealed class OpenAiFileSearchRetrievalTests
{
    #region Response Fixtures

    private const string FileSearchSuccessResponse = """
        {
            "id": "resp_fs_001",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_001",
                    "status": "completed",
                    "queries": ["search query"],
                    "results": [
                        {
                            "file_id": "file-abc123",
                            "filename": "document.pdf",
                            "score": 0.92,
                            "text": "This is the relevant passage from the document.",
                            "attributes": { "category": "technical" }
                        },
                        {
                            "file_id": "file-def456",
                            "filename": "notes.txt",
                            "score": 0.78,
                            "text": "Another relevant passage.",
                            "attributes": {}
                        }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Based on the documents, here is the answer."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 20
            }
        }
        """;

    private const string FileSearchNoResultsResponse = """
        {
            "id": "resp_fs_002",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_002",
                    "status": "completed",
                    "queries": ["obscure query"],
                    "results": []
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "I could not find relevant information."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 80,
                "output_tokens": 10
            }
        }
        """;

    private const string FileSearchFailedCallResponse = """
        {
            "id": "resp_fs_003",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_003",
                    "status": "failed"
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "I was unable to search the documents."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 60,
                "output_tokens": 10
            }
        }
        """;

    private const string FileSearchMixedStatusResponse = """
        {
            "id": "resp_fs_004",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_ok",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-ok",
                            "filename": "good.pdf",
                            "score": 0.85,
                            "text": "Valid result."
                        }
                    ]
                },
                {
                    "type": "file_search_call",
                    "id": "fs_fail",
                    "status": "failed"
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "Partial results."
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 90,
                "output_tokens": 10
            }
        }
        """;

    private const string FileSearchWithCitationsResponse = """
        {
            "id": "resp_fs_005",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_005",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-abc",
                            "filename": "doc.pdf",
                            "score": 0.90,
                            "text": "The answer is 42."
                        }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        {
                            "type": "output_text",
                            "text": "The answer is 42.",
                            "annotations": [
                                {
                                    "type": "file_citation",
                                    "file_id": "file-abc",
                                    "filename": "doc.pdf",
                                    "start_index": 0,
                                    "end_index": 17
                                }
                            ]
                        }
                    ]
                }
            ],
            "usage": {
                "input_tokens": 100,
                "output_tokens": 10
            }
        }
        """;

    private const string FileSearchSameFileMultiplePassagesResponse = """
        {
            "id": "resp_fs_006",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_006",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-same",
                            "filename": "big.pdf",
                            "score": 0.95,
                            "text": "First passage from the file."
                        },
                        {
                            "file_id": "file-same",
                            "filename": "big.pdf",
                            "score": 0.80,
                            "text": "Second passage from the file."
                        },
                        {
                            "file_id": "file-same",
                            "filename": "big.pdf",
                            "score": 0.70,
                            "text": "Third passage from the file."
                        }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        { "type": "output_text", "text": "Answer." }
                    ]
                }
            ],
            "usage": { "input_tokens": 100, "output_tokens": 10 }
        }
        """;

    private const string FileSearchMultiCallUnsortedResponse = """
        {
            "id": "resp_fs_007",
            "model": "gpt-5-0",
            "status": "completed",
            "output": [
                {
                    "type": "file_search_call",
                    "id": "fs_call_a",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-a",
                            "filename": "a.pdf",
                            "score": 0.50,
                            "text": "Low score from first call."
                        },
                        {
                            "file_id": "file-a",
                            "filename": "a.pdf",
                            "score": 0.40,
                            "text": "Even lower score from first call."
                        }
                    ]
                },
                {
                    "type": "file_search_call",
                    "id": "fs_call_b",
                    "status": "completed",
                    "queries": ["query"],
                    "results": [
                        {
                            "file_id": "file-b",
                            "filename": "b.pdf",
                            "score": 0.99,
                            "text": "Highest score from second call."
                        },
                        {
                            "file_id": "file-b",
                            "filename": "b.pdf",
                            "score": 0.85,
                            "text": "Second highest from second call."
                        }
                    ]
                },
                {
                    "type": "message",
                    "role": "assistant",
                    "content": [
                        { "type": "output_text", "text": "Answer." }
                    ]
                }
            ],
            "usage": { "input_tokens": 100, "output_tokens": 10 }
        }
        """;

    #endregion

    #region Helpers

    private static (OpenAiChatCompletionClient Client, OpenAiHostedRetrievalFeature Feature, Func<string?> GetCapturedBody) CreateClientWithFeature(
        string responseJson)
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

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { DefaultModel = "gpt-5-0" };
        var client = new OpenAiChatCompletionClient(httpClient, options);
        var feature = new OpenAiHostedRetrievalFeature(httpClient, options);
        client.Features.Set<IHostedRetrievalFeature>(feature);
        return (client, feature, () => capturedBody);
    }

    private static (OpenAiChatCompletionClient Client, OpenAiHostedRetrievalFeature Feature) CreateClientWithStatusAndFeature(
        HttpStatusCode statusCode,
        string responseBody = "error")
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
            }));

        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { DefaultModel = "gpt-5-0" };
        var client = new OpenAiChatCompletionClient(httpClient, options);
        var feature = new OpenAiHostedRetrievalFeature(httpClient, options);
        client.Features.Set<IHostedRetrievalFeature>(feature);
        return (client, feature);
    }

    #endregion

    #region IHostedRetrievalFeature Discovery

    [Test]
    public void HostedRetrievalFeature_IsDiscoverable()
    {
        var (client, _, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var feature = client.Features.Get<IHostedRetrievalFeature>();
        Assert.That(feature, Is.Not.Null);
    }

    #endregion

    #region ForStore

    [Test]
    public void ForStore_ReturnsRetriever()
    {
        var (client, _, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        var retriever = feature.ForStore("vs_test");
        Assert.That(retriever, Is.Not.Null);
    }

    [Test]
    public void ForStore_NullStoreId_Throws()
    {
        var (client, _, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        Assert.Throws<ArgumentNullException>(() => feature.ForStore(null!));
    }

    [Test]
    public void ForStore_EmptyStoreId_Throws()
    {
        var (client, _, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var feature = client.Features.Get<IHostedRetrievalFeature>()!;
        Assert.Throws<ArgumentException>(() => feature.ForStore(""));
    }

    #endregion

    #region Request Shape

    [Test]
    public async Task Retrieve_SendsFileSearchTool_WithVectorStoreId()
    {
        var (_, feature, getCapturedBody) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_my_store");

        await retriever.RetrieveAsync("test query", 5);

        var body = getCapturedBody()!;
        var doc = JsonDocument.Parse(body);
        var tools = doc.RootElement.GetProperty("tools");
        Assert.That(tools.GetArrayLength(), Is.EqualTo(1));

        var tool = tools[0];
        Assert.Multiple(() =>
        {
            Assert.That(tool.GetProperty("type").GetString(), Is.EqualTo("file_search"));
            Assert.That(tool.GetProperty("vector_store_ids")[0].GetString(), Is.EqualTo("vs_my_store"));
            Assert.That(tool.GetProperty("max_num_results").GetInt32(), Is.EqualTo(5));
        });
    }

    [Test]
    public async Task Retrieve_SetsIncludeFileSearchCallResults()
    {
        var (_, feature, getCapturedBody) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        await retriever.RetrieveAsync("query", 10);

        var body = getCapturedBody()!;
        var doc = JsonDocument.Parse(body);
        var include = doc.RootElement.GetProperty("include");
        Assert.That(include.GetArrayLength(), Is.EqualTo(1));
        Assert.That(include[0].GetString(), Is.EqualTo("file_search_call.results"));
    }

    [Test]
    public async Task Retrieve_PostsToResponsesEndpoint()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(FileSearchSuccessResponse, System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var options = new OpenAiClientOptions { DefaultModel = "gpt-5-0" };
        var feature = new OpenAiHostedRetrievalFeature(httpClient, options);
        var retriever = feature.ForStore("vs_test");

        await retriever.RetrieveAsync("query", 5);

        Assert.That(handler.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/responses"));
    }

    [Test]
    public async Task Retrieve_SendsQueryAsUserMessage()
    {
        var (_, feature, getCapturedBody) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        await retriever.RetrieveAsync("my search query", 5);

        var body = getCapturedBody()!;
        var doc = JsonDocument.Parse(body);
        var input = doc.RootElement.GetProperty("input");
        Assert.That(input.GetArrayLength(), Is.EqualTo(1));

        var msg = input[0];
        Assert.Multiple(() =>
        {
            Assert.That(msg.GetProperty("role").GetString(), Is.EqualTo("user"));
            Assert.That(msg.GetProperty("content").GetString(), Is.EqualTo("my search query"));
        });
    }

    #endregion

    #region Result Mapping

    [Test]
    public async Task Retrieve_MapsResultsToScoredChunks()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Retrieve_FirstResult_HasCorrectScore()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Score, Is.EqualTo(0.92).Within(0.001));
    }

    [Test]
    public async Task Retrieve_MapsDocumentIdToFileId()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Chunk.DocumentId, Is.EqualTo("file-abc123"));
    }

    [Test]
    public async Task Retrieve_MapsIndex_PerFileOrdinal()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Chunk.Index, Is.EqualTo(0));
            Assert.That(results[1].Chunk.Index, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task Retrieve_MapsOffsetsAsPassageRelative()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Chunk.StartOffset, Is.EqualTo(0));
            Assert.That(results[0].Chunk.EndOffset, Is.EqualTo(results[0].Chunk.Text.Length));
        });
    }

    [Test]
    public async Task Retrieve_MapsText()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results[0].Chunk.Text, Is.EqualTo("This is the relevant passage from the document."));
    }

    [Test]
    public async Task Retrieve_MapsMetadata_IncludesFileIdAndFilename()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.Multiple(() =>
        {
            Assert.That(results[0].Chunk.Metadata["file_id"], Is.EqualTo("file-abc123"));
            Assert.That(results[0].Chunk.Metadata["filename"], Is.EqualTo("document.pdf"));
        });
    }

    [Test]
    public async Task Retrieve_NoResults_ReturnsEmptyList()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchNoResultsResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("obscure", 10);

        Assert.That(results, Is.Empty);
    }

    #endregion

    #region Error Handling

    [Test]
    public async Task Retrieve_HttpError_ReturnsEmptyList_DoesNotThrow()
    {
        var (_, feature) = CreateClientWithStatusAndFeature(HttpStatusCode.InternalServerError);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Retrieve_FailedFileSearchCall_ReturnsEmptyList()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchFailedCallResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 5);

        Assert.That(results, Is.Empty);
    }

    [Test]
    public async Task Retrieve_MixedStatus_ReturnsOnlyCompletedResults()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchMixedStatusResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(1));
        Assert.That(results[0].Chunk.DocumentId, Is.EqualTo("file-ok"));
    }

    [Test]
    public void Retrieve_CancellationRequested_ThrowsOperationCanceled()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSuccessResponse);
        var retriever = feature.ForStore("vs_test");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(
            () => retriever.RetrieveAsync("query", 5, cts.Token));
    }

    [Test]
    public void Retrieve_NoDefaultModel_Throws()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.openai.com/v1/") };
        var feature = new OpenAiHostedRetrievalFeature(httpClient, new OpenAiClientOptions());
        var retriever = feature.ForStore("vs_test");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => retriever.RetrieveAsync("query", 5));
    }

    #endregion

    #region Per-File Ordinals and Sorting

    [Test]
    public async Task Retrieve_SameFile_AssignsDistinctPerFileOrdinals()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSameFileMultiplePassagesResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(results.Select(r => r.Chunk.Index).Distinct().Count(), Is.EqualTo(3));
            Assert.That(results.All(r => r.Chunk.DocumentId == "file-same"), Is.True);
        });
    }

    [Test]
    public async Task Retrieve_SameFile_OrdinalsDerivedFromProviderOrder_NotSortOrder()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSameFileMultiplePassagesResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        // Provider order: score 0.95 (ordinal 0), 0.80 (ordinal 1), 0.70 (ordinal 2)
        // After sort by score descending, ordinals should remain: 0, 1, 2
        Assert.Multiple(() =>
        {
            Assert.That(results[0].Chunk.Index, Is.EqualTo(0));
            Assert.That(results[0].Score, Is.EqualTo(0.95).Within(0.001));
            Assert.That(results[1].Chunk.Index, Is.EqualTo(1));
            Assert.That(results[1].Score, Is.EqualTo(0.80).Within(0.001));
            Assert.That(results[2].Chunk.Index, Is.EqualTo(2));
            Assert.That(results[2].Score, Is.EqualTo(0.70).Within(0.001));
        });
    }

    [Test]
    public async Task Retrieve_SameFile_SurvivesRankFusion_AsDistinctEntries()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchSameFileMultiplePassagesResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        // Verify the precondition: all from same file
        Assert.That(results.All(r => r.Chunk.DocumentId == "file-same"), Is.True);

        // Feed through RankFusion as a single list — all three must survive dedup
        var fused = RankFusion.ReciprocalRank(new[] { results });

        Assert.That(fused, Has.Count.EqualTo(3),
            "Three passages from the same file_id must survive RankFusion as distinct entries because their per-file ordinals give them distinct (DocumentId, Index) identities.");
    }

    [Test]
    public async Task Retrieve_MultiCall_SortsByScoreDescending()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchMultiCallUnsortedResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        Assert.That(results, Has.Count.EqualTo(4));
        for (int i = 1; i < results.Count; i++)
        {
            Assert.That(results[i].Score, Is.LessThanOrEqualTo(results[i - 1].Score),
                $"Result at index {i} (score {results[i].Score}) should not rank above index {i - 1} (score {results[i - 1].Score}).");
        }
    }

    [Test]
    public async Task Retrieve_MultiCall_TopKTruncatesAfterSorting()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchMultiCallUnsortedResponse);
        var retriever = feature.ForStore("vs_test");

        // 4 results across two calls; topK=2 should keep the two highest-scoring
        var results = await retriever.RetrieveAsync("query", 2);

        Assert.That(results, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(results[0].Score, Is.EqualTo(0.99).Within(0.001), "Highest scoring result from second call should be first");
            Assert.That(results[1].Score, Is.EqualTo(0.85).Within(0.001), "Second highest scoring result from second call should be second");
        });
    }

    [Test]
    public async Task Retrieve_MultiCall_PerFileOrdinalsArePerFile_NotGlobal()
    {
        var (_, feature, _) = CreateClientWithFeature(FileSearchMultiCallUnsortedResponse);
        var retriever = feature.ForStore("vs_test");

        var results = await retriever.RetrieveAsync("query", 10);

        var fileAResults = results.Where(r => r.Chunk.DocumentId == "file-a").ToList();
        var fileBResults = results.Where(r => r.Chunk.DocumentId == "file-b").ToList();

        Assert.Multiple(() =>
        {
            Assert.That(fileAResults.Select(r => r.Chunk.Index), Is.EquivalentTo(new[] { 0, 1 }));
            Assert.That(fileBResults.Select(r => r.Chunk.Index), Is.EquivalentTo(new[] { 0, 1 }));
        });
    }

    #endregion
}
