using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class PatternStore
{
    public static List<CrystallizedPattern> Load()
    {
        if (!File.Exists(Paths.Patterns)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<CrystallizedPattern>>(
                File.ReadAllText(Paths.Patterns), JsonOptions.Default) ?? [];
        }
        catch { return []; }
    }

    public static void Save(List<CrystallizedPattern> patterns)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.Patterns)!);
        File.WriteAllText(Paths.Patterns,
            JsonSerializer.Serialize(patterns, JsonOptions.Indented));
    }

    public static List<CrystallizedPattern> ReadForProject(string project) =>
        Load().Where(p => p.Project == project).ToList();

    public static void Append(CrystallizedPattern pattern)
    {
        var all = Load();
        all.Add(pattern);
        Save(all);
    }
}
