using System.Net;
using System.Text.Json;
using Cisharpai.Cohere;

namespace Cisharpai.Tests.Cohere;

public sealed class CohereTokenCounterTests
{
    private const string TokenizeResponseJson = """
    {
        "tokens": [1, 2, 3, 4, 5],
        "token_strings": ["Hello", ",", " world", "!", ""]
    }
    """;

    private static MockHttpMessageHandler OkHandler(Action<string>? captureBody = null, string? responseJson = null) =>
        new(async (request, _) =>
        {
            if (captureBody is not null)
                captureBody(await request.Content!.ReadAsStringAsync(CancellationToken.None));

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson ?? TokenizeResponseJson, System.Text.Encoding.UTF8, "application/json")
            };
        });

    private static CohereTokenCounter CreateCounter(
        HttpMessageHandler handler,
        out HttpClient httpClient,
        string model = "embed-english-v3.0",
        string baseUrl = "https://api.cohere.com/v2/")
    {
        httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        return new CohereTokenCounter(httpClient, new CohereClientOptions { BaseUrl = baseUrl }, model);
    }

    [Test]
    public async Task CountAsync_ReturnsTokenCount()
    {
        var handler = OkHandler();
        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        var count = await counter.CountAsync("Hello, world!");

        Assert.That(count, Is.EqualTo(5));
    }

    [Test]
    public async Task CountAsync_SendsModelInRequest()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var counter = CreateCounter(handler, out var httpClient, model: "embed-english-v3.0");
        using var _ = httpClient;

        await counter.CountAsync("test");

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.That(root.GetProperty("model").GetString(), Is.EqualTo("embed-english-v3.0"));
    }

    [Test]
    public async Task CountAsync_SendsTextInRequest()
    {
        string? capturedBody = null;
        var handler = OkHandler(body => capturedBody = body);
        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        await counter.CountAsync("Hello, world!");

        var root = JsonDocument.Parse(capturedBody!).RootElement;
        Assert.That(root.GetProperty("text").GetString(), Is.EqualTo("Hello, world!"));
    }

    [Test]
    public async Task CountAsync_PostsToV1TokenizeEndpoint()
    {
        var handler = OkHandler();
        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        await counter.CountAsync("test");

        Assert.That(handler.LastRequest!.RequestUri!.ToString(),
            Does.Contain("/v1/tokenize"));
    }

    [Test]
    public async Task CountAsync_EmptyStringReturnsZero()
    {
        var handler = OkHandler();
        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        var count = await counter.CountAsync("");

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task CountAsync_ChunksTextOverCharacterLimit()
    {
        var callCount = 0;
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            var root = JsonDocument.Parse(body).RootElement;
            var text = root.GetProperty("text").GetString()!;

            Assert.That(text.Length, Is.LessThanOrEqualTo(CohereTokenCounter.MaxCharactersPerRequest),
                $"Chunk {callCount} exceeds max character limit");

            Interlocked.Increment(ref callCount);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tokens": [1, 2, 3], "token_strings": ["a", "b", "c"]}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        var longText = new string('a', CohereTokenCounter.MaxCharactersPerRequest + 100);
        var count = await counter.CountAsync(longText);

        Assert.Multiple(() =>
        {
            Assert.That(callCount, Is.GreaterThan(1));
            Assert.That(count, Is.EqualTo(callCount * 3));
        });
    }

    [Test]
    public async Task CountAsync_ChunksOnWhitespaceBoundary()
    {
        var receivedTexts = new List<string>();
        var handler = new MockHttpMessageHandler(async (request, _) =>
        {
            var body = await request.Content!.ReadAsStringAsync(CancellationToken.None);
            var root = JsonDocument.Parse(body).RootElement;
            receivedTexts.Add(root.GetProperty("text").GetString()!);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"tokens": [1], "token_strings": ["a"]}""",
                    System.Text.Encoding.UTF8, "application/json")
            };
        });

        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        var nearLimit = new string('a', CohereTokenCounter.MaxCharactersPerRequest - 10);
        var text = nearLimit + " " + new string('b', 20);

        await counter.CountAsync(text);

        Assert.That(receivedTexts, Has.Count.EqualTo(2));
        Assert.That(receivedTexts[0], Does.EndWith(" "));
    }

    [Test]
    public void CountAsync_ThrowsOnNullText()
    {
        var handler = OkHandler();
        var counter = CreateCounter(handler, out var httpClient);
        using var _ = httpClient;

        Assert.That(async () => await counter.CountAsync(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Constructor_ThrowsOnNullModel()
    {
        using var httpClient = new HttpClient();
        Assert.That(() => new CohereTokenCounter(httpClient, new CohereClientOptions(), null!),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Constructor_ThrowsOnEmptyModel()
    {
        using var httpClient = new HttpClient();
        Assert.That(() => new CohereTokenCounter(httpClient, new CohereClientOptions(), ""),
            Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void FindWhitespaceSplitPoint_SplitsAtLastWhitespace()
    {
        var text = "Hello world foo bar";
        var splitPoint = CohereTokenCounter.FindWhitespaceSplitPoint(text, 0, 12);

        Assert.That(splitPoint, Is.EqualTo(12));
        Assert.That(text[..splitPoint].TrimEnd(), Does.Not.EndWith(" "));
    }

    [Test]
    public void FindWhitespaceSplitPoint_FallsBackToMaxLengthWhenNoWhitespace()
    {
        var text = new string('a', 100);
        var splitPoint = CohereTokenCounter.FindWhitespaceSplitPoint(text, 0, 50);

        Assert.That(splitPoint, Is.EqualTo(50));
    }

    [Test]
    public async Task CountAsync_CustomBaseUrlDeriveV1Correctly()
    {
        var handler = OkHandler();
        var counter = CreateCounter(handler, out var httpClient,
            baseUrl: "https://my-cohere.inference.ai.azure.com/v2/");
        using var _ = httpClient;

        await counter.CountAsync("test");

        Assert.That(handler.LastRequest!.RequestUri!.ToString(),
            Does.Contain("my-cohere.inference.ai.azure.com/v1/tokenize"));
    }
}
