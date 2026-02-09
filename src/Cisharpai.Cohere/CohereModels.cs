namespace Cisharpai.Cohere;

/// <summary>
/// Well-known Cohere model identifiers.
/// </summary>
public static class CohereModels
{
    /// <summary>Chat / completion models.</summary>
    public static class Chat
    {
        public const string CommandA = "command-a-03-2025";
        public const string CommandRPlus = "command-r-plus-08-2024";
        public const string CommandR = "command-r-08-2024";
    }

    /// <summary>Embedding models.</summary>
    public static class Embedding
    {
        public const string EmbedV4 = "embed-v4.0";
        public const string EmbedEnglishV3 = "embed-english-v3.0";
        public const string EmbedMultilingualV3 = "embed-multilingual-v3.0";
        public const string EmbedEnglishLightV3 = "embed-english-light-v3.0";
        public const string EmbedMultilingualLightV3 = "embed-multilingual-light-v3.0";
    }
}
