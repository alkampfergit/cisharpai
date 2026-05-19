using Cisharpai.OpenAi;

namespace Cisharpai.Tests.Factory;

public sealed class ClientConfigurationTests
{
    [Test]
    public void OpenAiConfiguration_HasCorrectProvider()
    {
        var config = new OpenAiClientConfiguration { ApiKey = "test-key" };

        Assert.Multiple(() =>
        {
            Assert.That(config.Provider, Is.EqualTo(CisharpaiProvider.OpenAi));
            Assert.That(config.ApiKey, Is.EqualTo("test-key"));
        });
    }

    [Test]
    public void OpenAiConfiguration_DefaultValues()
    {
        var config = new OpenAiClientConfiguration { ApiKey = "k" };

        Assert.Multiple(() =>
        {
            Assert.That(config.BaseUrl, Is.EqualTo("https://api.openai.com/v1/"));
            Assert.That(config.DefaultModel, Is.Null);
            Assert.That(config.Organization, Is.Null);
            Assert.That(config.ReasoningEffort, Is.Null);
            Assert.That(config.TextVerbosity, Is.Null);
        });
    }

    [Test]
    public void FactoryResult_Success_HasClient()
    {
        var result = CisharpaiClientFactoryResult<IChatCompletionClient>.Success(
            NSubstitute.Substitute.For<IChatCompletionClient>());

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Client, Is.Not.Null);
            Assert.That(result.ErrorMessage, Is.Null);
        });
    }

    [Test]
    public void FactoryResult_Failure_HasErrorMessage()
    {
        var result = CisharpaiClientFactoryResult<IChatCompletionClient>.Failure("something went wrong");

        Assert.Multiple(() =>
        {
            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Client, Is.Null);
            Assert.That(result.ErrorMessage, Is.EqualTo("something went wrong"));
        });
    }

    [Test]
    public void Configuration_IsImmutableRecord()
    {
        var config = new OpenAiClientConfiguration
        {
            ApiKey = "key1",
            DefaultModel = "gpt-4o"
        };

        var copy = config with { ApiKey = "key2" };

        Assert.Multiple(() =>
        {
            Assert.That(config.ApiKey, Is.EqualTo("key1"));
            Assert.That(copy.ApiKey, Is.EqualTo("key2"));
            Assert.That(copy.DefaultModel, Is.EqualTo("gpt-4o"));
        });
    }
}
