using System.Text.Json.Serialization;

namespace Cisharpai.OpenAi.Models;

public sealed class OpenAiVectorStoreCreateRequest
{
    [JsonPropertyName("name")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Name { get; set; }

    [JsonPropertyName("file_ids")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? FileIds { get; set; }

    [JsonPropertyName("metadata")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("expires_after")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiExpiresAfter? ExpiresAfter { get; set; }
}

public sealed class OpenAiExpiresAfter
{
    [JsonPropertyName("anchor")]
    public string Anchor { get; set; } = "last_active_at";

    [JsonPropertyName("days")]
    public int Days { get; set; }
}

public sealed class OpenAiVectorStore
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("file_counts")]
    public OpenAiFileCounts FileCounts { get; set; } = new();

    [JsonPropertyName("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}

public sealed class OpenAiFileCounts
{
    [JsonPropertyName("in_progress")]
    public int InProgress { get; set; }

    [JsonPropertyName("completed")]
    public int Completed { get; set; }

    [JsonPropertyName("failed")]
    public int Failed { get; set; }

    [JsonPropertyName("cancelled")]
    public int Cancelled { get; set; }

    [JsonPropertyName("total")]
    public int Total { get; set; }
}

public sealed class OpenAiVectorStoreListResponse
{
    [JsonPropertyName("data")]
    public List<OpenAiVectorStore> Data { get; set; } = [];

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

public sealed class OpenAiVectorStoreFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("vector_store_id")]
    public string VectorStoreId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("last_error")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public OpenAiLastError? LastError { get; set; }

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}

public sealed class OpenAiLastError
{
    [JsonPropertyName("code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;
}

public sealed class OpenAiVectorStoreFileListResponse
{
    [JsonPropertyName("data")]
    public List<OpenAiVectorStoreFile> Data { get; set; } = [];

    [JsonPropertyName("has_more")]
    public bool HasMore { get; set; }
}

public sealed class OpenAiUploadedFile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("object")]
    public string Object { get; set; } = string.Empty;

    [JsonPropertyName("bytes")]
    public long Bytes { get; set; }

    [JsonPropertyName("filename")]
    public string Filename { get; set; } = string.Empty;

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = string.Empty;

    [JsonPropertyName("created_at")]
    public long CreatedAt { get; set; }
}

public sealed class OpenAiDeleteResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("deleted")]
    public bool Deleted { get; set; }
}
