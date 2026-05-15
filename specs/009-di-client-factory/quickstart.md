# Quickstart: DI Client Factory

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

## 2. Create clients at runtime

```csharp
var factory = provider.GetRequiredService<ICisharpaiClientFactory>();

// Switch provider by changing only the configuration
var config = new RuntimeClientConfiguration(
    Provider: CisharpaiProvider.OpenAi,
    ApiKey: "sk-your-key",
    Model: "gpt-4o"
);

var result = factory.CreateChatCompletionClient(config);
if (!result.IsSuccess)
{
    Console.WriteLine($"Error: {result.ErrorMessage}");
    return;
}

var response = await result.Client!.GetChatCompletionAsync(
    new ChatCompletionRequest { Model = "gpt-4o", Messages = [...] });
```

## 3. Azure providers with extra settings

```csharp
var azureConfig = new RuntimeClientConfiguration(
    Provider: CisharpaiProvider.AzureOpenAi,
    ApiKey: "your-azure-key",
    Model: "gpt-4o",
    Endpoint: "https://your-resource.openai.azure.com/",
    ExtraSettings: new Dictionary<string, string>
    {
        ["DeploymentName"] = "my-gpt4o-deployment",
        ["ApiVersion"] = "2024-10-21"
    }
);

var azureResult = factory.CreateChatCompletionClient(azureConfig);
```

## 4. Embedding clients

```csharp
var embeddingConfig = new RuntimeClientConfiguration(
    Provider: CisharpaiProvider.OpenAi,
    ApiKey: "sk-your-key",
    Model: "text-embedding-3-small"
);

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
