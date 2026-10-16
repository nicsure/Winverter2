using Winverter2;
using System.Text.Json;

var directory = Path.Combine(Path.GetTempPath(), "WinverterStateTests-" + Guid.NewGuid().ToString("N"));
var file = Path.Combine(directory, "state.json");
var store = new AlwaysInvertStore(file);
var preferencesFile = Path.Combine(directory, "preferences.json");
void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
try
{
    Check(store.Load().Count == 0, "Missing state must load as empty.");
    store.Save([@"C:\Apps\Editor.exe", @"c:\apps\EDITOR.EXE", @"C:\Other\Editor.exe"]);
    var restarted = new AlwaysInvertStore(file).Load();
    Check(restarted.Count == 2, "Deduplicate by full path, case-insensitively; keep different folders.");
    Check(restarted.Contains(@"C:\Other\Editor.exe"), "Persist applications that are not installed or running.");
    var before = File.ReadAllText(file);
    try { store.Save(["relative.exe"]); throw new Exception("Relative path accepted."); }
    catch (InvalidDataException) { }
    Check(File.ReadAllText(file) == before, "Failed validation must preserve previous state.");
    store.Save([restarted[0]]);
    Check(new AlwaysInvertStore(file).Load().Count == 1, "Removal must persist across store instances.");
    store.Save([]);
    Check(new AlwaysInvertStore(file).Load().Count == 0, "Empty list must persist.");
    File.WriteAllText(file, "{broken");
    try { store.Load(); throw new Exception("Malformed JSON accepted."); }
    catch (JsonException) { }
    Check(File.ReadAllText(file) == "{broken", "Loading corrupt settings must not overwrite them.");
    Check(Directory.GetFiles(directory, "*.tmp").Length == 0, "Save must leave no temporary files.");
    var preferencesStore = new PreferencesStore(preferencesFile);
    Check(!preferencesStore.Load().StartInNotificationArea, "First use must open the main window.");
    preferencesStore.Save(new Preferences { StartInNotificationArea = true });
    Check(new PreferencesStore(preferencesFile).Load().StartInNotificationArea, "Tray-only preference must survive restart.");
    preferencesStore.Save(new Preferences { StartInNotificationArea = false });
    Check(!new PreferencesStore(preferencesFile).Load().StartInNotificationArea, "Turning off tray-only startup must persist.");
    File.WriteAllText(preferencesFile, "{}");
    Check(!preferencesStore.Load().StartInNotificationArea, "Missing preference flag must default to false.");
    File.WriteAllText(preferencesFile, "{broken");
    try { preferencesStore.Load(); throw new Exception("Malformed preferences accepted."); }
    catch (JsonException) { }
    Check(File.ReadAllText(preferencesFile) == "{broken", "Loading invalid preferences must preserve the file.");
    Console.WriteLine("All persistence checks passed.");
}
finally
{
    if (File.Exists(file)) File.Delete(file);
    if (File.Exists(preferencesFile)) File.Delete(preferencesFile);
    if (Directory.Exists(directory)) Directory.Delete(directory);
}
