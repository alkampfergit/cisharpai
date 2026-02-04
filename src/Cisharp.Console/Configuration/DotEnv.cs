namespace Cisharp.Console.Configuration;

public static class DotEnv
{
    public const string OpenAiApiKey = "OPENAI_API_KEY";
    public const string OpenAiOrganization = "OPENAI_ORG";
    public const string OpenAiBaseUrl = "OPENAI_BASE_URL";

    public const string AnthropicApiKey = "ANTHROPIC_API_KEY";
    public const string AnthropicBaseUrl = "ANTHROPIC_BASE_URL";
    public const string AnthropicApiVersion = "ANTHROPIC_API_VERSION";

    public const string AzureOpenAiEndpoint = "AZURE_OPENAI_ENDPOINT";
    public const string AzureOpenAiDeployment = "AZURE_OPENAI_DEPLOYMENT";
    public const string AzureOpenAiApiKey = "AZURE_OPENAI_API_KEY";
    public const string AzureOpenAiApiVersion = "AZURE_OPENAI_API_VERSION";

    public const string CohereApiKey = "COHERE_API_KEY";

    public static void Load()
    {
        var directory = new DirectoryInfo(Directory.GetCurrentDirectory());

        while (directory is not null)
        {
            var filePath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(filePath))
            {
                LoadFile(filePath);
                return;
            }

            directory = directory.Parent;
        }
    }

    private static void LoadFile(string path)
    {
        foreach (var line in File.ReadAllLines(path))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#'))
                continue;

            var separatorIndex = trimmed.IndexOf('=');
            if (separatorIndex < 0)
                continue;

            var key = trimmed[..separatorIndex].Trim();
            var value = trimmed[(separatorIndex + 1)..].Trim();

            if (value.Length >= 2 &&
                ((value.StartsWith('"') && value.EndsWith('"')) ||
                 (value.StartsWith('\'') && value.EndsWith('\''))))
            {
                value = value[1..^1];
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
