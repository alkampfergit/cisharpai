# Contract: JsonDeepMerge

**File**: `src/Cisharpai/JsonDeepMerge.cs`

## Class Definition

```csharp
public static class JsonDeepMerge
```

## Method

### `Merge(string baseJson, JsonElement overrides) → string`

Deeply merges an override JSON object into a base JSON string. Returns the merged JSON string.

**Preconditions**:
- `baseJson` must parse as a JSON object (`ValueKind == Object`). Throws `ArgumentException` otherwise.
- `overrides` must be a JSON object (`ValueKind == Object`). Throws `ArgumentException` otherwise.

**Merge semantics**:

1. Iterate all properties in `baseJson`:
   - If the property key exists in `overrides`:
     - If both values are objects → recursively merge
     - Otherwise → override value replaces base value
   - If the property key does NOT exist in `overrides` → preserve base value
2. Iterate all properties in `overrides`:
   - If the property key does NOT exist in `baseJson` → add to output
3. Return the merged JSON string

**Property ordering**: Base properties appear first (original order), followed by override-only properties.

**Type coercion**: None. Override values are written as-is regardless of the base value's type. An object can replace a scalar, a scalar can replace an object, an array replaces an array.

**Array handling**: Arrays are replaced entirely. No element-level merging.

**Null handling**: `"temperature": null` in overrides sets the property to JSON null.

**Empty inputs**:
- Empty base `{}` + non-empty overrides → returns overrides
- Non-empty base + empty overrides `{}` → returns base unchanged

## Usage

```csharp
var baseJson = """{"model":"gpt-4","reasoning":{"effort":"high"}}""";
var overrides = JsonDocument.Parse("""{"reasoning":{"summary":"auto"},"stream":true}""").RootElement;

var result = JsonDeepMerge.Merge(baseJson, overrides);
// {"model":"gpt-4","reasoning":{"effort":"high","summary":"auto"},"stream":true}
```

## Error Cases

```csharp
// Non-object override → ArgumentException
JsonDeepMerge.Merge("{}", JsonDocument.Parse("[1,2]").RootElement);

// Non-object base → ArgumentException
JsonDeepMerge.Merge("[1,2]", JsonDocument.Parse("{}").RootElement);
```
