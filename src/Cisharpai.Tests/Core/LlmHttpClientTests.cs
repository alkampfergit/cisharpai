using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Cisharpai.Tests.Core;

public sealed class LlmHttpClientTests
{
    [Test]
    public async Task PostAsync_SendsJsonBody_WithCorrectContentType()
    {
        var handler = new MockHttpMessageHandler((request, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":42}", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        await client.PostAsync<object, JsonElement>("api/test", new { Name = "test" });

        Assert.Multiple(() =>
        {
            Assert.That(handler.LastRequest, Is.Not.Null);
            Assert.That(handler.LastRequest!.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
        });
    }

    [Test]
    public async Task PostAsync_SerializesPayload_AsCamelCase()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        await client.PostAsync<object, JsonElement>("api/test", new { MyProperty = "hello" });

        Assert.Multiple(() =>
        {
            Assert.That(capturedBody, Does.Contain("\"myProperty\""));
            Assert.That(capturedBody, Does.Not.Contain("\"MyProperty\""));
        });
    }

    [Test]
    public async Task PostAsync_DeserializesResponse_Correctly()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"name\":\"result\",\"count\":5}",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var result = await client.PostAsync<object, TestResponse>("api/test", new { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("result"));
            Assert.That(result.Count, Is.EqualTo(5));
        });
    }

    [Test]
    public void PostAsync_ThrowsLlmHttpRequestExceptionOnNonSuccessStatusCode()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var ex = Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { }));

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
    }

    [Test]
    public void PostAsync_ThrowsLlmHttpRequestException_WithResponseBody()
    {
        const string errorBody = "{\"error\":{\"message\":\"Invalid API key\",\"type\":\"invalid_request_error\"}}";
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(errorBody, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var ex = Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(ex.ResponseBody, Is.EqualTo(errorBody));
            Assert.That(ex.Message, Does.Contain("401"));
            Assert.That(ex.Message, Does.Contain(errorBody));
        });
    }

    [Test]
    public void PostAsync_ThrowsLlmHttpRequestException_WithEmptyBody()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("", System.Text.Encoding.UTF8, "text/plain")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var ex = Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
            Assert.That(ex.ResponseBody, Is.EqualTo(""));
            Assert.That(ex.Message, Does.Contain("502"));
        });
    }

    [Test]
    public async Task PostAsync_LlmHttpRequestException_IsCatchableAsHttpRequestException()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        HttpRequestException? caughtException = null;
        try
        {
            await client.PostAsync<object, JsonElement>("api/test", new { });
        }
        catch (HttpRequestException ex)
        {
            caughtException = ex;
        }

        Assert.Multiple(() =>
        {
            Assert.That(caughtException, Is.Not.Null);
            Assert.That(caughtException, Is.InstanceOf<LlmHttpRequestException>());
        });
    }

    [Test]
    public void PostAsync_ThrowsOnNullResponseBody()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.PostAsync<object, TestResponse>("api/test", new { }));
    }

    [Test]
    public async Task PostWithRawAsync_ReturnsDeserializedResultAndRawJson()
    {
        const string json = "{\"name\":\"result\",\"count\":5}";
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var (result, rawResponseJson, rawRequestJson) = await client.PostWithRawAsync<object, TestResponse>("api/test", new { });

        Assert.Multiple(() =>
        {
            Assert.That(result.Name, Is.EqualTo("result"));
            Assert.That(result.Count, Is.EqualTo(5));
            Assert.That(rawResponseJson, Is.EqualTo(json));
            Assert.That(rawRequestJson, Is.Not.Null);
        });
    }

    [Test]
    public void PostWithRawAsync_ThrowsLlmHttpRequestExceptionOnNonSuccessStatusCode()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostWithRawAsync<object, JsonElement>("api/test", new { }));
    }

    [Test]
    public void PostAsync_PreservesExceptionInfo_WhenResponseBodyReadFails()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new ThrowingHttpContent("Simulated read failure")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var ex = Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.InternalServerError));
            Assert.That(ex.ResponseBody, Does.Contain("Failed to read response body"));
            Assert.That(ex.ResponseBody, Does.Contain("Simulated read failure"));
        });
    }

    [Test]
    public void PostWithRawAsync_PreservesExceptionInfo_WhenResponseBodyReadFails()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new ThrowingHttpContent("Connection reset")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var ex = Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostWithRawAsync<object, JsonElement>("api/test", new { }));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
            Assert.That(ex.ResponseBody, Does.Contain("Failed to read response body"));
            Assert.That(ex.ResponseBody, Does.Contain("Connection reset"));
        });
    }

    [Test]
    public void Constructor_WithoutCustomOptions_SharesStaticDefaultOptions()
    {
        using var httpClient1 = new HttpClient { BaseAddress = new Uri("https://test.com") };
        using var httpClient2 = new HttpClient { BaseAddress = new Uri("https://test.com") };

        var client1 = new LlmHttpClient(httpClient1);
        var client2 = new LlmHttpClient(httpClient2);

        var field = typeof(LlmHttpClient).GetField(
            "_serializerOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var options1 = field.GetValue(client1);
        var options2 = field.GetValue(client2);

        Assert.That(options1, Is.SameAs(options2),
            "Multiple LlmHttpClient instances without custom options should share the same static JsonSerializerOptions");
    }

    [Test]
    public void Constructor_WithCustomOptions_DoesNotUseStaticDefault()
    {
        using var httpClient = new HttpClient { BaseAddress = new Uri("https://test.com") };

        var customOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var client = new LlmHttpClient(httpClient, customOptions);

        var field = typeof(LlmHttpClient).GetField(
            "_serializerOptions", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var actual = field.GetValue(client);

        Assert.That(actual, Is.SameAs(customOptions),
            "LlmHttpClient with custom options should use the provided instance");
    }

    [Test]
    public async Task PostAsync_MergesExtraParameters_IntoRequestBody()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        var extra = JsonDocument.Parse("{\"customParam\":\"value\",\"nested\":{\"key\":123}}").RootElement;

        await client.PostAsync<object, JsonElement>("api/test", new { Name = "test" }, extraParameters: extra);

        Assert.Multiple(() =>
        {
            Assert.That(capturedBody, Does.Contain("\"name\""));
            Assert.That(capturedBody, Does.Contain("\"customParam\":\"value\""));
            Assert.That(capturedBody, Does.Contain("\"nested\""));
            Assert.That(capturedBody, Does.Contain("\"key\":123"));
        });
    }

    [Test]
    public async Task PostAsync_ExtraParametersNull_SendsOriginalPayloadOnly()
    {
        string? capturedBody = null;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        await client.PostAsync<object, JsonElement>("api/test", new { Name = "test" }, extraParameters: null);

        Assert.That(capturedBody, Is.EqualTo("{\"name\":\"test\"}"));
    }

    [Test]
    public void PostWithRawAsync_ThrowsOnNullResponseBody()
    {
        var handler = new MockHttpMessageHandler((_, _) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("null", System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(response);
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await client.PostWithRawAsync<object, TestResponse>("api/test", new { }));
    }

    [Test]
    public void PostAsync_ThrowsTaskCanceledException_WhenCancelled()
    {
        var handler = new MockHttpMessageHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { }, cancellationToken: cts.Token));
    }

    [Test]
    public void PostWithRawAsync_ThrowsTaskCanceledException_WhenCancelled()
    {
        var handler = new MockHttpMessageHandler((_, ct) =>
        {
            ct.ThrowIfCancellationRequested();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        });

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com") };
        var client = new LlmHttpClient(httpClient);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await client.PostWithRawAsync<object, JsonElement>("api/test", new { }, cancellationToken: cts.Token));
    }

    [Test]
    public async Task PostAsync_LogsStructuredRequestAndResponse()
    {
        var logger = new TestLogger<LlmHttpClient>();
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":42}", System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient, logger: logger);

        await client.PostAsync<object, JsonElement>("api/test", new { Name = "test" });

        Assert.That(logger.Entries, Has.Count.EqualTo(2));

        var requestLog = logger.Entries[0];
        var responseLog = logger.Entries[1];

        Assert.Multiple(() =>
        {
            Assert.That(requestLog.LogLevel, Is.EqualTo(LogLevel.Information));
            Assert.That(requestLog.Properties["HttpMethod"], Is.EqualTo("POST"));
            Assert.That(requestLog.Properties["RequestUri"], Is.EqualTo("https://test.com/api/test"));
            Assert.That(requestLog.Properties["RequestBody"], Is.EqualTo("{\"name\":\"test\"}"));
            Assert.That(responseLog.LogLevel, Is.EqualTo(LogLevel.Information));
            Assert.That(responseLog.Properties["StatusCode"], Is.EqualTo(200));
            Assert.That(responseLog.Properties["ResponseBody"], Is.EqualTo("{\"value\":42}"));
            Assert.That(responseLog.Properties["RequestUri"], Is.EqualTo("https://test.com/api/test"));
        });
    }

    [Test]
    public void PostAsync_LogsStructuredFailure()
    {
        var logger = new TestLogger<LlmHttpClient>();
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad request\"}", System.Text.Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient, logger: logger);

        Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { Name = "test" }));

        Assert.That(logger.Entries, Has.Count.EqualTo(2));

        var failureLog = logger.Entries[1];
        Assert.Multiple(() =>
        {
            Assert.That(failureLog.LogLevel, Is.EqualTo(LogLevel.Warning));
            Assert.That(failureLog.Properties["StatusCode"], Is.EqualTo(400));
            Assert.That(failureLog.Properties["ResponseBody"], Is.EqualTo("{\"error\":\"bad request\"}"));
            Assert.That(failureLog.Properties["RequestUri"], Is.EqualTo("https://test.com/api/test"));
        });
    }

    private sealed class TestResponse
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    private sealed class ThrowingHttpContent : HttpContent
    {
        private readonly string _errorMessage;

        public ThrowingHttpContent(string errorMessage) => _errorMessage = errorMessage;

        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => throw new IOException(_errorMessage);

        protected override bool TryComputeLength(out long length)
        {
            length = 0;
            return false;
        }
    }
}
