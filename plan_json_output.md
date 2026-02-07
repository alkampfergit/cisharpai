# Implementation Plan: JSON Output Feature for Chat Completion

## Goal
Implement a feature that allows enforcing JSON output for Chat Completion requests. This includes both "JSON Mode" (forcing valid JSON output) and "Structured Outputs" (forcing output to match a specific JSON Schema). This feature will be implemented using the `IFeatureCollection` pattern, similar to image embeddings.

## Architecture

### 1. Core Feature Definition
*   **Location**: `src/Cisharpai/Features/Chat/`
*   **Interface**: `IJsonOutputFeature`
*   **Models**: 
    *   `JsonOutputOptions`: A configuration object to specify the mode (Any JSON vs Schema-based) and the schema itself if applicable.
    *   The schema is provided as a string by the caller; construction and validation of the JSON schema content is caller-owned.

### 2. Feature Capability
The `IJsonOutputFeature` will expose a method to perform chat completion with JSON constraints.
```csharp
public interface IJsonOutputFeature
{
    Task<ChatCompletionResponse> GetChatCompletionAsync(
        ChatCompletionRequest request,
        JsonOutputOptions options,
        CancellationToken cancellationToken = default);
}
```

### 3. Provider Implementation
We will implement this feature in the following providers:
*   **Cisharpai.OpenAi**: Support for `response_format` with `json_object` and `json_schema`.
    *   **Note**: Newer models (starting from `gpt-4o` and including future `gpt-5` class models) use **Structured Outputs** via `json_schema` with `strict: true`.
    *   Ref: [OpenAI Structured Outputs Guide](https://platform.openai.com/docs/guides/structured-outputs)
*   **Cisharpai.Azure (AzureOpenAi)**: Support for `response_format` with `json_object` and `json_schema`.

## Tasks

### Phase 1: Core Definitions
1.  [ ] Create `src/Cisharpai/Features/Chat/` directory.
2.  [ ] Define `JsonOutputOptions` record in `Cisharpai.Models` (or `Cisharpai.Features.Chat`).
    *   Properties:
        *   `bool Strict`: Required for Structured Outputs (`json_schema`).
        *   `string? SchemaName`: Required for Structured Outputs.
        *   `string? SchemaDescription`: Optional.
        *   `string? JsonSchema`: The JSON schema string supplied by the caller (schema construction is caller-owned).
    *   **Documentation Note**: Ensure these map correctly to OpenAI's `response_format` > `json_schema` structure.
3.  [ ] Define `IJsonOutputFeature` interface in `Cisharpai.Features.Chat`.

### Phase 2: OpenAI Implementation (`Cisharpai.OpenAi`)
4.  [ ] Update `OpenAiChatCompletionClient` to implement `IJsonOutputFeature`.
5.  [ ] In `GetChatCompletionAsync`:
    *   Determine mode based on `JsonOutputOptions`.
    *   **Mode A (Legacy/Loose)**: If `JsonSchema` is null, use `type: "json_object"`.
    *   **Mode B (Structured/Strict)**: If `JsonSchema` is present, use `type: "json_schema"`.
        *   Construct payload: `{ type: "json_schema", json_schema: { name, strict, schema } }`.
        *   Ensure `strict: true` is set as per "Structured Outputs" documentation for modern models.
    *   Register the feature in the client's feature collection.

### Phase 3: Azure OpenAI Implementation (`Cisharpai.Azure`)
6.  [ ] Update `AzureOpenAiChatCompletionClient` in `src/Cisharpai.Azure/AzureOpenAi/`.
7.  [ ] Implement `IJsonOutputFeature`.
    *   Map options to Azure OpenAI's REST API `response_format`.
    *   Register the feature.

### Phase 4: Testing & Verification
8.  [ ] Add integration tests in `Cisharpai.Integration.Tests`.
    *   Test JSON Mode (basic valid JSON).
    *   Test JSON Schema Mode (validation against a schema).
    *   Test both modes against model families (legacy models, gpt-4o family, gpt-5 family) and Azure Foundry / Azure OpenAI endpoints.
9.  [ ] Verify support for both providers.

---

## Findings from Official Docs (OpenAI & Azure)
Key points that affect implementation and must be handled by the clients:

- Structured Outputs vs JSON mode
  - Structured Outputs (json_schema + strict:true) is the recommended approach for modern models (gpt-4o and later, and gpt-5 family). It enforces schema adherence and returns a `refusal` object when the model refuses for safety reasons.
  - JSON mode (`json_object`) ensures valid JSON but does not guarantee schema adherence and **requires** the phrase "JSON" somewhere in the conversation (the API will error otherwise).

- Model compatibility
    - Structured Outputs are supported on latest models only (examples: `gpt-4o-mini`, `gpt-4o-2024-08-06` and later). Older models should fall back to JSON mode.
    - `gpt-5` and o-series models still use the same chat completion client, but with an extended request type that adds JSON output settings (json without schema vs json with schema) to the payload. No separate Responses client is required.

- Schema requirements & limits
  - Root must be an `object`, `additionalProperties` must be `false`, and all fields should be marked `required` (use `null` unions to emulate optional fields).
  - Limits exist on schema size, nesting depth and enum sizes; validate early to avoid API errors.

- Azure differences
  - Azure (Foundry/Agent/Responses) exposes a `response_format` / `text.format` configuration object analogous to OpenAI's `text.format` / `response_format`. Prefer `json_schema` when available; `json_object` is still supported but not recommended for gpt-4o+.
  - In some Azure integrations the schema may be appended as a prefix to the system message; verify specific endpoint behavior and provide both mapping strategies if needed.

- Parsing & failures
  - Structured Outputs are returned in structured response objects (or via SDK parsing helpers) but may include `refusal` entries. JSON mode may deliver raw text — implement robust extraction and parsing and validate against the schema.
  - Streaming structured outputs is supported by the APIs; plan for a streaming-compatible parse path in future iterations.

---

## Updated Implementation Tasks

### Phase 1.5: Schema Validation & Tools
10. [ ] Add a small JSON Schema validation utility in `src/Cisharpai/Utilities/JsonSchemaValidator.cs`.
    *   Consider a dependency like `Json.Schema` (https://www.nuget.org/packages/Json.Schema/) or `NJsonSchema` and add to `Directory.Packages.props`.
    *   Implement checks: root object, ensure `additionalProperties:false`, ensure `required` list exists, and reject obviously oversized schemas with a helpful exception.

### Phase 2 (OpenAI) — Details
11. [ ] Implement model-detection logic in `OpenAiChatCompletionClient`:
    *   If model name indicates `gpt-5`/o-series or explicitly lists Structured Outputs support, include the JSON schema settings on the extended request type so the chat completion payload uses `{ type: "json_schema", json_schema: { name, strict, schema } }`.
    *   If the model does not support Structured Outputs and `JsonSchema` is provided, either: convert the request to JSON mode (`json_object`) and add a system message that mentions "JSON", or return a clear error to the caller recommending changing to a supported model.
12. [ ] Add a helper that ensures when `json_object` is used, there's a system message that explicitly instructs the model to output JSON (and includes the word "JSON"), to satisfy the API check.
13. [ ] Implement parsing/validation logic:
    *   Structured Outputs: parse SDK response objects or `output` items and return parsed JSON validated against the schema; detect `refusal` and expose it on `ChatCompletionResponse`.
    *   JSON mode: parse raw text, attempt to find the first valid JSON object in the output (robustly), then validate it with the schema if provided.

### Phase 3 (Azure) — Details
14. [ ] Mirror the OpenAI behaviour in `AzureOpenAiChatCompletionClient` and ensure mapping uses Azure shapes:
    *   Use `response_format`/`text.format` where supported by the endpoint.
    *   For endpoints that require schema-as-system-prefix, provide a helper to prepend the schema if the provider indicates so.

### Phase 3.5: Feature Registration & API Design
15. [ ] Ensure `IJsonOutputFeature` is registered in both provider feature collections and is discoverable via `IHasFeatures`.
16. [ ] Decide and document fallback behaviour (e.g., if Structured Outputs unsupported and schema provided, either fallback to `json_object` with schema validation client-side or throw an informative exception).

### Phase 4: Testing & Verification (expanded)
17. [ ] Add unit tests for:
    *   Schema validator (root object, additionalProperties, required enforcement).
    *   System-message injection when `json_object` is selected.
    *   Parsing logic for JSON mode and Structured Outputs, including `refusal` handling.
18. [ ] Add integration tests covering:
    *   OpenAI chat completion requests for `gpt-5`/o-series using the extended request type with JSON output settings (if available in integration environment).
    *   OpenAI chat completion requests for older models (`gpt-3.5`, `gpt-4-*`).
    *   Azure Foundry / Azure OpenAI endpoints exercising both `json_schema` and `json_object` paths.

### Phase 5: Docs & Examples
19. [ ] Add documentation to `wiki/` describing the feature, provider differences, and sample usage for both JSON Mode and Structured Outputs (include sample JSON Schemas and recommended model families).
20. [ ] Add example code snippets and an integration test template to `Cisharpai.Integration.Tests` that can be enabled manually (since running real provider tests requires credentials and may be flaky).

---

## Notes & Risks
- Forcing Structured Outputs for models that do not support it can produce errors; the client should detect this and provide a clear, actionable message.
- Schema validation on the client should be conservative — do not try to fully reimplement the API's validation rules, but check the most common pitfalls (root object, additionalProperties, required) to provide faster feedback to developers.
- Consider adding a feature-flag or opt-in behavior for automatic schema injection and strict validation for users who prefer strict enforcement.


---

References:
- OpenAI Structured Outputs Guide: https://platform.openai.com/docs/guides/structured-outputs
- OpenAI Responses API: https://platform.openai.com/docs/api-reference/responses
- Azure OpenAI (Foundry) Agent / Responses docs: https://learn.microsoft.com/en-us/azure/ai-foundry/openai/

