namespace Cisharpai.Tests.Common;

/// <summary>
/// Utility class to load environment variables from a .env file.
/// Searches for .env in the current directory and parent directories.
/// </summary>
public static class DotEnvLoader
{
    /// <summary>
    /// Loads environment variables from a .env file found in the current
    /// directory or any parent directory.
    /// </summary>
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

    /// <summary>
    /// Loads environment variables from the specified .env file path.
    /// </summary>
    /// <param name="path">Path to the .env file.</param>
    public static void LoadFile(string path)
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

            // Strip surrounding quotes
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
