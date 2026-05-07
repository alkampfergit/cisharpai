using Cisharpai.Azure.AzureOpenAi;

namespace Cisharpai.Tests.Azure.AzureOpenAi;

public sealed class AzureOpenAiClientOptionsTests
{
    [Test]
    public void Validate_ValidApiKeyConfig_DoesNotThrow()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = "my-api-key",
            DeploymentName = "gpt-4"
        };

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void Validate_MissingEndpoint_ThrowsInvalidOperationException()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "",
            ApiKey = "my-api-key",
            DeploymentName = "gpt-4"
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex.Message, Does.Contain("Endpoint"));
    }

    [Test]
    public void Validate_MissingDeploymentName_ThrowsInvalidOperationException()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = "my-api-key",
            DeploymentName = ""
        };

        var ex = Assert.Throws<InvalidOperationException>(() => options.Validate());
        Assert.That(ex.Message, Does.Contain("DeploymentName"));
    }

    [Test]
    public void Validate_MissingApiKey_DoesNotThrow_WhenOnlyValidatingStructure()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = null,
            DeploymentName = "gpt-4"
        };

        Assert.DoesNotThrow(() => options.Validate());
    }

    [Test]
    public void ValidateAuthentication_NoApiKeyNoCredential_Throws()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = null,
            DeploymentName = "gpt-4"
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => options.ValidateAuthentication(hasTokenCredential: false));
        Assert.That(ex.Message, Does.Contain("ApiKey").Or.Contain("TokenCredential"));
    }

    [Test]
    public void ValidateAuthentication_NoApiKeyWithCredential_DoesNotThrow()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = null,
            DeploymentName = "gpt-4"
        };

        Assert.DoesNotThrow(() => options.ValidateAuthentication(hasTokenCredential: true));
    }

    [Test]
    public void ValidateAuthentication_WithApiKeyNoCredential_DoesNotThrow()
    {
        var options = new AzureOpenAiClientOptions
        {
            Endpoint = "https://myresource.openai.azure.com",
            ApiKey = "my-api-key",
            DeploymentName = "gpt-4"
        };

        Assert.DoesNotThrow(() => options.ValidateAuthentication(hasTokenCredential: false));
    }

    [Test]
    public void DefaultValues_ApiVersionIsSet()
    {
        var options = new AzureOpenAiClientOptions();

        Assert.That(options.ApiVersion, Is.EqualTo("2024-10-21"));
    }

    [Test]
    public void ModelName_CanBeSet()
    {
        var options = new AzureOpenAiClientOptions
        {
            ModelName = "gpt-5"
        };

        Assert.That(options.ModelName, Is.EqualTo("gpt-5"));
    }
}
