using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class AlertStore
{
    public static void Append(string project, SafetyAlert alert)
    {
        var dir = Paths.AlertDir(project);
        Directory.CreateDirectory(dir);
        var file = Paths.AlertFile(project, DateOnly.FromDateTime(DateTime.UtcNow));
        File.AppendAllText(file, JsonSerializer.Serialize(alert, JsonOptions.Default) + "\n");
    }

    public static List<SafetyAlert> ReadRecent(string project, int maxDays = 7)
    {
        var dir = Paths.AlertDir(project);
        if (!Directory.Exists(dir)) return [];

        var alerts = new List<SafetyAlert>();
        for (var i = 0; i < maxDays; i++)
        {
            var file = Paths.AlertFile(project, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)));
            if (!File.Exists(file)) continue;
            foreach (var line in File.ReadAllLines(file))
            {
                try
                {
                    var a = JsonSerializer.Deserialize<SafetyAlert>(line, JsonOptions.Default);
                    if (a is not null) alerts.Add(a);
                }
                catch { /* skip malformed lines */ }
            }
        }
        return alerts;
    }

    public static List<SafetyAlert> ReadUnacknowledged(string project, int maxDays = 7) =>
        ReadRecent(project, maxDays).Where(a => !a.Acknowledged).ToList();

    public static bool Acknowledge(string project, string alertId)
    {
        var dir = Paths.AlertDir(project);
        if (!Directory.Exists(dir)) return false;

        foreach (var file in Directory.GetFiles(dir, "*.jsonl"))
        {
            var lines = File.ReadAllLines(file);
            var patched = false;

            for (var i = 0; i < lines.Length; i++)
            {
                try
                {
                    var alert = JsonSerializer.Deserialize<SafetyAlert>(lines[i], JsonOptions.Default);
                    if (alert?.AlertId != alertId) continue;
                    lines[i] = JsonSerializer.Serialize(alert with { Acknowledged = true }, JsonOptions.Default);
                    patched = true;
                    break;
                }
                catch { /* skip */ }
            }

            if (!patched) continue;
            File.WriteAllLines(file, lines);
            return true;
        }
        return false;
    }
}
