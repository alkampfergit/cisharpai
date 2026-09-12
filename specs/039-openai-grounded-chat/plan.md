# Implementation Plan: OpenAI / Azure OpenAI Grounded Chat

## Architecture

Documents are base64-encoded as `input_file` items in the Responses API `input` array (alongside chat messages). The model processes these files and returns `file_citation` annotations on `output_text` content blocks. Annotations are mapped to the existing `Citation`/`CitationSource` model using filename-to-DocumentChunk.Id mapping.

## Changes

### Model Classes (New)
- `OpenAiInputFile` — `type`, `filename`, `file_data` (base64 data URI)
- `AzureOpenAiInputFile` — Same structure for Azure

### Response Models (Modified)
- `OpenAiResponseContent` — Added `Annotations` list
- `OpenAiAnnotation` — `type`, `file_id`, `filename`, `start_index`, `end_index`
- `AzureOpenAiResponseContent` / `AzureOpenAiAnnotation` — Same

### Request Models (Modified)
- `OpenAiResponsesApiRequest.Input` — Changed from `List<OpenAiChatMessage>` to `List<object>` for heterogeneous input
- `AzureOpenAiResponsesApiRequest.Input` — Same

### Client Implementations
- `OpenAiChatCompletionClient` — Implements `IGroundedChatFeature`, model gate on `Gpt5`
- `AzureOpenAiChatCompletionClient` — Implements `IGroundedChatFeature`, route fallback returns error

### Tests
- `OpenAiGroundedChatTests` — 19 tests covering feature registration, model gate, request building, response mapping, validation
- `AzureOpenAiGroundedChatTests` — 10 tests covering feature registration, model gate, request building, response mapping, route fallback
- `FeatureDiscoveryTests` — Updated to assert OpenAI and Azure expose the feature
