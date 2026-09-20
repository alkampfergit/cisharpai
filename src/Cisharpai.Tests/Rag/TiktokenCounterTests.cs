using Cisharpai.Rag.Tokenization;

namespace Cisharpai.Tests.Rag;

public sealed class TiktokenCounterTests
{
    [Test]
    public async Task CountAsync_ReturnsCorrectCountForSimpleText()
    {
        var counter = new TiktokenCounter("gpt-4o");

        var count = await counter.CountAsync("Hello, world!");

        Assert.That(count, Is.EqualTo(4));
    }

    [Test]
    public async Task CountAsync_EmptyStringReturnsZero()
    {
        var counter = new TiktokenCounter("gpt-4o");

        var count = await counter.CountAsync("");

        Assert.That(count, Is.EqualTo(0));
    }

    [Test]
    public async Task CountAsync_LongerTextReturnsMoreTokens()
    {
        var counter = new TiktokenCounter("gpt-4o");

        var short_ = await counter.CountAsync("Hello");
        var long_ = await counter.CountAsync("Hello, this is a much longer sentence with many more words in it.");

        Assert.That(long_, Is.GreaterThan(short_));
    }

    [Test]
    public async Task CountTokens_MatchesCountAsync()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "The quick brown fox jumps over the lazy dog.";

        var sync = counter.CountTokens(text);
        var async_ = await counter.CountAsync(text);

        Assert.That(sync, Is.EqualTo(async_));
    }

    [Test]
    public async Task CountAsync_IsThreadSafe()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var text = "Thread safety test with some tokens.";
        var barrier = new Barrier(100);

        var tasks = Enumerable.Range(0, 100)
            .Select(_ => Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await counter.CountAsync(text);
            }))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.That(results, Is.All.EqualTo(results[0]));
    }

    [Test]
    public void Constructor_ThrowsOnNullModelName()
    {
        Assert.That(() => new TiktokenCounter(null!), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public void Constructor_ThrowsOnEmptyModelName()
    {
        Assert.That(() => new TiktokenCounter(""), Throws.InstanceOf<ArgumentException>());
    }

    [Test]
    public async Task CountAsync_ThrowsOnNullText()
    {
        var counter = new TiktokenCounter("gpt-4o");

        Assert.That(async () => await counter.CountAsync(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void CountTokens_ThrowsOnNullText()
    {
        var counter = new TiktokenCounter("gpt-4o");

        Assert.That(() => counter.CountTokens(null!), Throws.ArgumentNullException);
    }

    [Test]
    public void Constructor_SupportsGpt4Model()
    {
        Assert.DoesNotThrow(() => new TiktokenCounter("gpt-4"));
    }

    [Test]
    public void Constructor_SupportsGpt35TurboModel()
    {
        Assert.DoesNotThrow(() => new TiktokenCounter("gpt-3.5-turbo"));
    }

    [Test]
    public async Task CountAsync_DifferentModelsProduceDifferentCounts()
    {
        var o200k = new TiktokenCounter("gpt-4o");
        var cl100k = new TiktokenCounter("gpt-4");

        // "tiktoken" is a single token in cl100k_base but two in o200k_base,
        // making it a reliable discriminator between the two encodings.
        var text = "tiktoken tiktoken tiktoken tiktoken tiktoken";

        var countO200k = await o200k.CountAsync(text);
        var countCl100k = await cl100k.CountAsync(text);

        Assert.Multiple(() =>
        {
            Assert.That(countO200k, Is.Not.EqualTo(countCl100k),
                "The two encodings must produce different counts for this input");
            Assert.That(countO200k, Is.GreaterThan(0));
            Assert.That(countCl100k, Is.GreaterThan(0));
        });
    }

    [Test]
    public void CountAsync_ThrowsOnPreCancelledToken()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.That(() => counter.CountAsync("Hello, world!", cts.Token),
            Throws.InstanceOf<OperationCanceledException>());
    }

    [Test]
    public void ToTokenEstimator_ReturnsFuncThatDelegatesToCountTokens()
    {
        var counter = new TiktokenCounter("gpt-4o");
        var estimator = counter.ToTokenEstimator();

        var text = "Hello, world!";
        var expected = counter.CountTokens(text);
        var actual = estimator(text);

        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void ToTokenEstimator_ThrowsOnNullCounter()
    {
        TiktokenCounter? counter = null;

        Assert.That(() => counter!.ToTokenEstimator(), Throws.ArgumentNullException);
    }
}
