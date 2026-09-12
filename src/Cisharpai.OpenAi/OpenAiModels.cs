namespace Cisharpai.OpenAi;

/// <summary>
/// Well-known OpenAI model identifiers.
/// </summary>
public static class OpenAiModels
{
    /// <summary>Chat / completion models.</summary>
    public static class Chat
    {
        public const string Gpt4_1 = "gpt-4.1";
        public const string Gpt4_1Mini = "gpt-4.1-mini";
        public const string Gpt4_1Nano = "gpt-4.1-nano";
        public const string Gpt4o = "gpt-4o";
        public const string Gpt4oMini = "gpt-4o-mini";
        public const string Gpt4_5 = "gpt-4.5";
        public const string O3 = "o3";
        public const string O3Mini = "o3-mini";
        public const string O3Pro = "o3-pro";
        public const string O4Mini = "o4-mini";
        public const string O1 = "o1";
        public const string O1Mini = "o1-mini";
    }

    /// <summary>Embedding models.</summary>
    public static class Embedding
    {
        public const string TextEmbedding3Small = "text-embedding-3-small";
        public const string TextEmbedding3Large = "text-embedding-3-large";
        public const string TextEmbeddingAda002 = "text-embedding-ada-002";
    }
}
