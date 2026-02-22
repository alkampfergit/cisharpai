using System.Net;
using System.Text;
using System.Text.Json;

namespace Cisharpai.Tests.Core;

public sealed class LlmHttpClientStreamTests
{
    private static HttpResponseMessage CreateSseResponse(string sseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var bytes = Encoding.UTF8.GetBytes(sseContent);
        var stream = new MemoryStream(bytes);
        return new HttpResponseMessage(statusCode)
        {
            Content = new StreamContent(stream)
        };
    }

    [Test]
    public async Task Yields_Content_From_Data_Lines()
    {
        const string sseContent = """
            data: {"content":"Hello"}

            data: {"content":" world"}

            data: [DONE]
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateSseResponse(sseContent)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var results = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { }))
        {
            results.Add(chunk);
        }

        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results[0], Is.EqualTo("{\"content\":\"Hello\"}"));
        Assert.That(results[1], Is.EqualTo("{\"content\":\" world\"}"));
    }

    [Test]
    public async Task Stops_On_Done_Signal()
    {
        const string sseContent = """
            data: {"content":"first"}

            data: [DONE]

            data: {"content":"should not appear"}
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateSseResponse(sseContent)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var results = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { }))
        {
            results.Add(chunk);
        }

        Assert.That(results.Count, Is.EqualTo(1));
        Assert.That(results[0], Is.EqualTo("{\"content\":\"first\"}"));
    }

    [Test]
    public async Task Ignores_Empty_Lines()
    {
        const string sseContent = """
            data: {"a":1}



            data: {"b":2}


            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateSseResponse(sseContent)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var results = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { }))
        {
            results.Add(chunk);
        }

        Assert.That(results.Count, Is.EqualTo(2));
    }

    [Test]
    public async Task Ignores_Event_Lines_Anthropic_Style()
    {
        const string sseContent = """
            event: content_block_delta
            data: {"type":"content_block_delta","delta":{"text":"Hi"}}

            event: message_stop
            data: {"type":"message_stop"}
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateSseResponse(sseContent)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var results = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { }))
        {
            results.Add(chunk);
        }

        Assert.That(results.Count, Is.EqualTo(2));
        Assert.That(results[0], Does.Contain("content_block_delta"));
        Assert.That(results[1], Does.Contain("message_stop"));
    }

    [Test]
    public async Task Handles_Stream_End_Without_Done()
    {
        // Anthropic/Cohere pattern: stream ends naturally, no [DONE]
        const string sseContent = """
            data: {"type":"message_start"}

            data: {"type":"content_block_delta","delta":{"text":"Hello"}}

            data: {"type":"message_stop"}
            """;

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(CreateSseResponse(sseContent)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var results = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { }))
        {
            results.Add(chunk);
        }

        Assert.That(results.Count, Is.EqualTo(3));
        // No exception should be thrown — enumeration completes normally
    }

    [Test]
    public void Throws_On_Non_Success_Status()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"invalid request\"}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
        {
            await foreach (var _ in client.PostStreamAsync<object>("api/test", new { }))
            {
                // Should throw before yielding anything
            }
        });
    }

    [Test]
    public async Task Extra_Parameters_Merged_Into_Request()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            const string sseContent = "data: {\"result\":true}\n\ndata: [DONE]\n";
            return CreateSseResponse(sseContent);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var extra = JsonDocument.Parse("{\"stream\":true,\"customParam\":\"value\"}").RootElement;

        var results = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { Name = "test" }, extraParameters: extra))
        {
            results.Add(chunk);
        }

        Assert.That(capturedBody, Does.Contain("\"name\""));
        Assert.That(capturedBody, Does.Contain("\"stream\":true"));
        Assert.That(capturedBody, Does.Contain("\"customParam\":\"value\""));
        Assert.That(results.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Cancellation_Token_Stops_Enumeration()
    {
        // Create a slow streaming response with many chunks
        var sseLines = string.Join("\n", Enumerable.Range(1, 100).Select(i => $"data: {{\"n\":{i}}}\n"));
        var bytes = Encoding.UTF8.GetBytes(sseLines);

        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var stream = new MemoryStream(bytes);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(stream)
            });
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        using var cts = new CancellationTokenSource();
        var results = new List<string>();

        var ex = Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var chunk in client.PostStreamAsync<object>("api/test", new { }, cts.Token))
            {
                results.Add(chunk);
                if (results.Count == 1)
                {
                    cts.Cancel(); // Cancel after first chunk
                }
            }
        });

        Assert.That(results.Count, Is.GreaterThanOrEqualTo(1));
        Assert.That(ex, Is.Not.Null);
    }
}
