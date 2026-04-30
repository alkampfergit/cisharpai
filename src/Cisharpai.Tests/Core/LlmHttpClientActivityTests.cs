using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;

namespace Cisharpai.Tests.Core;

public sealed class LlmHttpClientActivityTests
{
    private static ActivityListener CreateListener(List<Activity> sink)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == CisharpaiTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = activity => sink.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Test]
    public void ActivitySourceName_Constant_Is_Cisharpai()
    {
        Assert.That(CisharpaiTelemetry.ActivitySourceName, Is.EqualTo("Cisharpai"));
    }

    [Test]
    public async Task PostAsync_Without_Listener_Creates_No_Activity()
    {
        // No listener registered: StartActivity returns null and we shouldn't blow up.
        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":1}", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        var result = await client.PostAsync<object, JsonElement>("api/test", new { Name = "x" });
        Assert.That(result.GetProperty("value").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task PostAsync_With_Listener_Emits_Span_With_Tags_And_Ok_Status()
    {
        var spans = new List<Activity>();
        using var listener = CreateListener(spans);

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"value\":42}", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        await client.PostAsync<object, JsonElement>("api/test", new { Name = "x" });

        Assert.That(spans, Has.Count.EqualTo(1));
        var span = spans[0];
        Assert.Multiple(() =>
        {
            Assert.That(span.OperationName, Is.EqualTo("POST api/test"));
            Assert.That(span.Kind, Is.EqualTo(ActivityKind.Client));
            Assert.That(span.GetTagItem("http.request.method"), Is.EqualTo("POST"));
            Assert.That(span.GetTagItem("url.full"), Is.EqualTo("https://test.com/api/test"));
            Assert.That(span.GetTagItem("server.address"), Is.EqualTo("test.com"));
            Assert.That(span.GetTagItem("http.response.status_code"), Is.EqualTo(200));
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Ok));
        });
    }

    [Test]
    public void PostAsync_Failure_Emits_Span_With_Error_Status()
    {
        var spans = new List<Activity>();
        using var listener = CreateListener(spans);

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("{\"error\":\"bad\"}", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
            await client.PostAsync<object, JsonElement>("api/test", new { Name = "x" }));

        Assert.That(spans, Has.Count.EqualTo(1));
        var span = spans[0];
        Assert.Multiple(() =>
        {
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(span.GetTagItem("http.response.status_code"), Is.EqualTo(400));
        });
    }

    [Test]
    public async Task PostStreamAsync_Emits_Span_With_Stream_Tags_And_Done_Completion()
    {
        var spans = new List<Activity>();
        using var listener = CreateListener(spans);

        const string sseContent = """
            data: {"content":"Hello"}

            data: {"content":" world"}

            data: [DONE]
            """;
        var bytes = Encoding.UTF8.GetBytes(sseContent);

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(bytes))
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        var collected = new List<string>();
        await foreach (var chunk in client.PostStreamAsync<object>("api/stream", new { Name = "x" }))
        {
            collected.Add(chunk);
        }

        Assert.That(collected, Has.Count.EqualTo(2));
        Assert.That(spans, Has.Count.EqualTo(1));
        var span = spans[0];
        Assert.Multiple(() =>
        {
            Assert.That(span.OperationName, Is.EqualTo("POST api/stream"));
            Assert.That(span.GetTagItem("cisharpai.stream"), Is.EqualTo(true));
            Assert.That(span.GetTagItem("cisharpai.stream.chunks"), Is.EqualTo(2));
            Assert.That(span.GetTagItem("cisharpai.stream.completion_kind"), Is.EqualTo("done"));
            Assert.That(span.Status, Is.EqualTo(ActivityStatusCode.Ok));
        });
    }

    [Test]
    public async Task PostStreamAsync_End_Of_Stream_Without_Done_Sets_End_Of_Stream_Kind()
    {
        var spans = new List<Activity>();
        using var listener = CreateListener(spans);

        // No "[DONE]" sentinel — stream ends naturally (Anthropic / Cohere style).
        const string sseContent = """
            data: {"content":"Hi"}


            """;
        var bytes = Encoding.UTF8.GetBytes(sseContent);

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(bytes))
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        await foreach (var _ in client.PostStreamAsync<object>("api/stream", new { Name = "x" }))
        {
        }

        Assert.That(spans, Has.Count.EqualTo(1));
        Assert.That(spans[0].GetTagItem("cisharpai.stream.completion_kind"), Is.EqualTo("end_of_stream"));
        Assert.That(spans[0].GetTagItem("cisharpai.stream.chunks"), Is.EqualTo(1));
        Assert.That(spans[0].Status, Is.EqualTo(ActivityStatusCode.Ok));
    }

    [Test]
    public async Task PostStreamAsync_Caller_Breaks_Early_Sets_Error_Status_With_Incomplete_Kind()
    {
        var spans = new List<Activity>();
        using var listener = CreateListener(spans);

        // Three chunks plus [DONE]; the consumer will break after the first.
        const string sseContent = """
            data: {"content":"a"}

            data: {"content":"b"}

            data: {"content":"c"}

            data: [DONE]
            """;
        var bytes = Encoding.UTF8.GetBytes(sseContent);

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new MemoryStream(bytes))
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        await foreach (var _ in client.PostStreamAsync<object>("api/stream", new { Name = "x" }))
        {
            break; // consumer abandons the stream
        }

        Assert.That(spans, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(spans[0].GetTagItem("cisharpai.stream.completion_kind"), Is.EqualTo("incomplete"));
            Assert.That(spans[0].Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(spans[0].GetTagItem("cisharpai.stream.chunks"), Is.EqualTo(1));
        });
    }

    [Test]
    public void PostStreamAsync_Failure_Response_Emits_Error_Span()
    {
        var spans = new List<Activity>();
        using var listener = CreateListener(spans);

        var handler = new MockHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\":\"unauthorized\"}", Encoding.UTF8, "application/json")
            }));

        using var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://test.com/") };
        var client = new LlmHttpClient(httpClient);

        Assert.ThrowsAsync<LlmHttpRequestException>(async () =>
        {
            await foreach (var _ in client.PostStreamAsync<object>("api/stream", new { Name = "x" }))
            {
            }
        });

        Assert.That(spans, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(spans[0].Status, Is.EqualTo(ActivityStatusCode.Error));
            Assert.That(spans[0].GetTagItem("http.response.status_code"), Is.EqualTo(401));
            Assert.That(spans[0].GetTagItem("cisharpai.stream.completion_kind"), Is.EqualTo("incomplete"));
        });
    }
}
