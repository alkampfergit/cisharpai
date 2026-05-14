# Quickstart: Grounded Chat (RAG)

## Prerequisites

- .NET 8.0 or .NET 10 SDK
- A Cohere API key (grounded chat is Cohere-only)

## Install

```bash
dotnet add package Cisharpai
dotnet add package Cisharpai.Cohere
```

## 1. Basic Grounded Chat with Structured Documents

```csharp
using Cisharpai;
using Cisharpai.Features.Chat;
using Cisharpai.Models;
using Cisharpai.Cohere;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddLogging();
services.AddCohereChatCompletionClient(options =>
{
    options.ApiKey = "YOUR_COHERE_API_KEY";
});

var provider = services.BuildServiceProvider();
var client = provider.GetRequiredService<IChatCompletionClient>();

if (client.Features.Get<IGroundedChatFeature>() is { } groundedFeature)
{
    var documents = new List<DocumentChunk>
    {
        new(Id: "doc-1", Data: new Dictionary<string, string>
        {
            ["title"] = "France",
            ["snippet"] = "Paris is the capital city of France."
        }),
        new(Id: "doc-2", Data: new Dictionary<string, string>
        {
            ["title"] = "Germany",
            ["snippet"] = "Berlin is the capital city of Germany."
        })
    };

    var request = new ChatCompletionRequest(
        Messages: [new LlmMessage(LlmRole.User, "What is the capital of France?")],
        Model: "command-a-03-2025");

    var response = await groundedFeature.GetGroundedChatCompletionAsync(
        request, new GroundedChatOptions(Documents: documents));

    Console.WriteLine(response.Content);
    foreach (var citation in response.Citations)
        Console.WriteLine($"  [{citation.Start}:{citation.End}] \"{citation.Text}\" from {citation.Sources[0].Id}");
}
```

## 2. Plain Text Documents

```csharp
var documents = new List<DocumentChunk>
{
    new(Id: "doc-1", Text: "Paris is the capital city of France."),
    new(Id: "doc-2", Text: "Berlin is the capital city of Germany.")
};

var options = new GroundedChatOptions(Documents: documents);
```

## 3. Citation Mode Control

```csharp
// Fast (default) — works on all Cohere models
var fast = new GroundedChatOptions(Documents: documents, CitationMode: CitationMode.Fast);

// Accurate — only command-r models (auto-downgrades on command-a)
var accurate = new GroundedChatOptions(Documents: documents, CitationMode: CitationMode.Accurate);
```

## 4. Error Handling

```csharp
var response = await groundedFeature.GetGroundedChatCompletionAsync(request, options);
if (!response.IsSuccess)
{
    Console.WriteLine($"Error: {response.ErrorMessage}");
}
```

## 5. Working with Citations

```csharp
foreach (var citation in response.Citations)
{
    var textAtOffset = response.Content[citation.Start..citation.End];
    Console.WriteLine($"Cited: \"{textAtOffset}\"");
    foreach (var source in citation.Sources)
        Console.WriteLine($"  From document: {source.Id}");
}
```

## Verification

```bash
dotnet test src/Cisharpai.Tests/ --filter "FullyQualifiedName~GroundedChat or FullyQualifiedName~DocumentChunk"
```
