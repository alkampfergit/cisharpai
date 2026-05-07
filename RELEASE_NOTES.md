# Release Notes

This file is included in the NuGet packages. Keep a single line for each user-facing feature added to the library.

## Unreleased

- Cohere grounded chat: default `CitationMode` is now `Fast` (works on both `command-r` and `command-a` families); when `Accurate` is requested against a `command-a` model the provider logs a warning and silently downgrades to `Fast` instead of letting the API return HTTP 400.
- Azure OpenAI GPT-5 models now automatically route to the Responses API (same as OpenAI), with first-class `TextVerbosity` option on `AzureOpenAiClientOptions`.
- Azure OpenAI gains optional `ModelFamily` on `AzureOpenAiClientOptions` to drive routing when the deployment name is opaque (e.g. set `ModelFamily="gpt-5"` for a deployment named "foo"); when unset, routing falls back to the deployment/request model name and finally to standard Chat Completions.
- Azure OpenAI reasoning requests now support first-class `ReasoningEffort` configuration while still allowing `ExtraParameters` overrides.
- Azure OpenAI chat completions now return `IsSuccess=false` with `IncompleteReason="length"` when the provider reports `finish_reason: "length"`.
- Initial release notes file added to the NuGet package contents.
