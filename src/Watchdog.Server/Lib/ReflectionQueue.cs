using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

/// <summary>
/// Persists the queue of nudges awaiting outcome evaluation.
/// Uses a single JSON array (not JSONL) — the full list is always read/written
/// atomically since partial reads would leave the queue inconsistent.
/// </summary>
public static class ReflectionQueue
{
    public static void Enqueue(PendingReflection reflection)
    {
        var queue = LoadAll();
        queue.Add(reflection);
        Save(queue);
    }

    public static List<PendingReflection> GetDue(DateTimeOffset? asOf = null)
    {
        var cutoff = asOf ?? DateTimeOffset.UtcNow;
        return LoadAll().Where(r => r.ReflectAfter <= cutoff).ToList();
    }

    public static void Remove(string episodeId)
    {
        var queue = LoadAll().Where(r => r.EpisodeId != episodeId).ToList();
        Save(queue);
    }

    public static int Count() => LoadAll().Count;

    public static List<PendingReflection> LoadAll()
    {
        if (!File.Exists(Paths.PendingReflections)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<PendingReflection>>(
                File.ReadAllText(Paths.PendingReflections), JsonOptions.Default) ?? [];
        }
        catch { return []; }
    }

    private static void Save(List<PendingReflection> queue)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.PendingReflections)!);
        File.WriteAllText(Paths.PendingReflections,
            JsonSerializer.Serialize(queue, JsonOptions.Default));
    }
}
