using Cisharpai.Testing;

namespace Cisharpai.Tests.Testing;

public sealed class FakeTokenCounterTests
{
    [Test]
    public async Task CountAsync_ReturnsQueuedCountsInOrder()
    {
        var fake = new FakeTokenCounter();
        fake.EnqueueCount(10);
        fake.EnqueueCount(20);

        var first = await fake.CountAsync("hello");
        var second = await fake.CountAsync("world");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(10));
            Assert.That(second, Is.EqualTo(20));
        });
    }

    [Test]
    public async Task CountAsync_FallsBackToDefaultCountWhenQueueIsEmpty()
    {
        var fake = new FakeTokenCounter { DefaultCount = 42 };

        var count = await fake.CountAsync("any text");

        Assert.That(count, Is.EqualTo(42));
    }

    [Test]
    public async Task CountAsync_CapturesReceivedTexts()
    {
        var fake = new FakeTokenCounter { DefaultCount = 5 };

        await fake.CountAsync("hello");
        await fake.CountAsync("world");

        Assert.Multiple(() =>
        {
            Assert.That(fake.ReceivedTexts, Has.Count.EqualTo(2));
            Assert.That(fake.ReceivedTexts[0], Is.EqualTo("hello"));
            Assert.That(fake.ReceivedTexts[1], Is.EqualTo("world"));
            Assert.That(fake.CallCount, Is.EqualTo(2));
        });
    }

    [Test]
    public void CountAsync_ThrowsWhenNoCountIsConfigured()
    {
        var fake = new FakeTokenCounter();

        Assert.That(
            async () => await fake.CountAsync("text"),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task Reset_ClearsQueueAndCapturedTexts()
    {
        var fake = new FakeTokenCounter();
        fake.EnqueueCount(10);
        await fake.CountAsync("text");

        fake.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(fake.ReceivedTexts, Is.Empty);
            Assert.That(fake.CallCount, Is.Zero);
        });

        Assert.That(
            async () => await fake.CountAsync("text"),
            Throws.InstanceOf<InvalidOperationException>());
    }

    [Test]
    public async Task QueuedCountTakesPriorityOverDefault()
    {
        var fake = new FakeTokenCounter { DefaultCount = 100 };
        fake.EnqueueCount(5);

        var first = await fake.CountAsync("queued");
        var second = await fake.CountAsync("default");

        Assert.Multiple(() =>
        {
            Assert.That(first, Is.EqualTo(5));
            Assert.That(second, Is.EqualTo(100));
        });
    }

    [Test]
    public void FakeResponses_TokenCounter_CreatesPreConfiguredFake()
    {
        var fake = FakeResponses.TokenCounter(25);

        Assert.That(fake.DefaultCount, Is.EqualTo(25));
    }
}
