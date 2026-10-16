using System.Text.Json;

namespace Winverter2;

internal sealed record Preferences
{
    public bool StartInNotificationArea { get; init; }
}

internal sealed class PreferencesStore
{
    internal static string DefaultPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Winverter2", "preferences.json");
    private readonly string filePath;
    internal PreferencesStore(string? path = null) => filePath = path ?? DefaultPath;

    internal Preferences Load() => !File.Exists(filePath) ? new()
        : JsonSerializer.Deserialize<Preferences>(File.ReadAllText(filePath))
            ?? throw new InvalidDataException("The preferences file contains no settings.");

    internal void Save(Preferences preferences)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        var temporary = filePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                JsonSerializer.Serialize(stream, preferences, new JsonSerializerOptions { WriteIndented = true });
                stream.Flush(true);
            }
            File.Move(temporary, filePath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
