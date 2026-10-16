using System.Text.Json;

namespace Winverter2;

internal sealed class AlwaysInvertStore
{
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winverter2", "always-invert.json");
    private readonly string filePath;
    internal AlwaysInvertStore(string? path = null) => filePath = path ?? DefaultPath;

    internal static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) || !string.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Always Invert entries must be absolute executable (.exe) paths.");
        return Path.GetFullPath(path);
    }

    internal List<string> Load()
    {
        if (!File.Exists(filePath)) return [];
        var paths = JsonSerializer.Deserialize<string[]>(File.ReadAllText(filePath))
            ?? throw new InvalidDataException("The Always Invert file contains no list.");
        return paths.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal void Save(IEnumerable<string> paths)
    {
        var normalized = paths.Select(Normalize).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToArray();
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, normalized, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            File.Move(temporary, filePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
