using Cisharpai.Azure.AzureAiInference;

namespace Cisharpai.Tests.Azure.AzureAiInference;

public sealed class AzureAiInferenceClientOptionsTests
{
    [Test]
    public void Validate_ValidApiKeyConfig_DoesNotThrow()
    {
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/models",
            ApiKey = "my-api-key",
            ModelId = "Phi-3-mini-4k-instruct"
        };

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_MissingEndpoint_ThrowsInvalidOperationException()
    {
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "",
            ApiKey = "my-api-key",
            ModelId = "Phi-3-mini-4k-instruct"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex.Message, Does.Contain("Endpoint"));
    }

    [Test]
    public void Validate_InvalidEndpointUri_ThrowsInvalidOperationException()
    {
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "not-a-valid-url",
            ApiKey = "my-api-key",
            ModelId = "Phi-3-mini-4k-instruct"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex.Message, Does.Contain("valid HTTP or HTTPS URL"));
    }

    [Test]
    public void Validate_MissingModelId_ThrowsInvalidOperationException()
    {
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/models",
            ApiKey = "my-api-key",
            ModelId = ""
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex.Message, Does.Contain("ModelId"));
    }

    [Test]
    public void Validate_MissingApiKey_ThrowsInvalidOperationException()
    {
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://my-resource.services.ai.azure.com/models",
            ApiKey = null,
            ModelId = "Phi-3-mini-4k-instruct"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex.Message, Does.Contain("ApiKey"));
    }

    [Test]
    public void DefaultValues_ApiVersionIsSet()
    {
        var options = new AzureAiInferenceClientOptions
        {
            Endpoint = "https://example.com",
            ModelId = "test",
            ApiKey = "key"
        };

        Assert.That(options.ApiVersion, Is.EqualTo("2024-05-01-preview"));
    }
}
