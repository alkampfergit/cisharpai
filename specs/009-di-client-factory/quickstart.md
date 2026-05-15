# Quickstart: DI Client Factory (v2 — strongly-typed configuration)

## 1. Register the factory and providers at startup

```csharp
var services = new ServiceCollection();

services.AddCisharpaiClientFactory()
    .AddOpenAiSupport()
    .AddAnthropicSupport()
    .AddCohereSupport()
    .AddAzureOpenAiSupport()
    .AddAzureAiInferenceSupport();

var provider = services.BuildServiceProvider();
```

## 2. Create clients at runtime (strongly-typed per-provider config)

```csharp
var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

// Each provider has its own configuration class with IntelliSense support
var config = new OpenAiClientConfiguration
{
    ApiKey = "sk-your-key",
    DefaultModel = "gpt-4o",
    Organization = "org-..."   // provider-specific — compile-time safe
};

var result = factory.CreateChatCompletionClient(config);
if (!result.IsSuccess)
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
    return;
}

var response = await result.Client!.GetChatCompletionAsync(
    new ChatCompletionRequest { Model = "gpt-4o", Messages = [...] });
```

## 3. Azure providers (required fields enforced at compile time)

```csharp
var azureConfig = new AzureOpenAiClientConfiguration
{
    ApiKey = "your-azure-key",
    Endpoint = "https://your-resource.openai.azure.com/",   // required
    DeploymentName = "my-gpt4o-deployment",                  // required
    ApiVersion = "2024-10-21"
};

var azureResult = factory.CreateChatCompletionClient(azureConfig);
```

## 4. Embedding clients

```csharp
var embeddingConfig = new OpenAiClientConfiguration
{
    ApiKey = "sk-your-key",
    DefaultModel = "text-embedding-3-small"
};

var embResult = factory.CreateEmbeddingClient(embeddingConfig);
if (embResult.IsSuccess)
{
    var embeddings = await embResult.Client!.GetEmbeddingsAsync(
        new EmbeddingRequest { Input = ["Hello world"], Model = "text-embedding-3-small" });
}
```

## 5. Check available providers

```csharp
var registered = factory.GetRegisteredProviders();
// e.g., [OpenAi, Anthropic, Cohere, AzureOpenAi, AzureAiInference]
```
