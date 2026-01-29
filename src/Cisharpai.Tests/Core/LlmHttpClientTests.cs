using System.Net;
using System.Text.Json;

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

        Assert.That(handler.LastRequest, Is.Not.Null);
        Assert.That(handler.LastRequest!.Content!.Headers.ContentType!.MediaType, Is.EqualTo("application/json"));
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

        Assert.That(capturedBody, Does.Contain("\"myProperty\""));
        Assert.That(capturedBody, Does.Not.Contain("\"MyProperty\""));
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

        Assert.That(result.Name, Is.EqualTo("result"));
        Assert.That(result.Count, Is.EqualTo(5));
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

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(ex.ResponseBody, Is.EqualTo(errorBody));
        Assert.That(ex.Message, Does.Contain("401"));
        Assert.That(ex.Message, Does.Contain(errorBody));
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

        Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway));
        Assert.That(ex.ResponseBody, Is.EqualTo(""));
        Assert.That(ex.Message, Does.Contain("502"));
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

        Assert.That(caughtException, Is.Not.Null);
        Assert.That(caughtException, Is.InstanceOf<LlmHttpRequestException>());
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

        var (result, rawJson) = await client.PostWithRawAsync<object, TestResponse>("api/test", new { });

        Assert.That(result.Name, Is.EqualTo("result"));
        Assert.That(result.Count, Is.EqualTo(5));
        Assert.That(rawJson, Is.EqualTo(json));
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

    private sealed class TestResponse
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}
