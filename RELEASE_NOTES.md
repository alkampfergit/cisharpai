# Release Notes

This file is included in the NuGet packages. Keep a single line for each user-facing feature added to the library.

## Unreleased

- Azure OpenAI reasoning requests now support first-class `ReasoningEffort` configuration while still allowing `ExtraParameters` overrides.
- Azure OpenAI chat completions now return `IsSuccess=false` with `IncompleteReason="length"` when the provider reports `finish_reason: "length"`.
- Initial release notes file added to the NuGet package contents.
