// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class EpisodeStore
{
    public static void Append(string project, Episode episode)
    {
        var dir = Paths.Episodes(project);
        Directory.CreateDirectory(dir);
        var file = Paths.EpisodeFile(project, DateOnly.FromDateTime(DateTime.UtcNow));
        File.AppendAllText(file, JsonSerializer.Serialize(episode, JsonOptions.Default) + "\n");
    }

    /// <summary>Reads episodes from the last <paramref name="maxDays"/> day files, newest first.</summary>
    public static List<Episode> ReadRecent(string project, int maxDays = 7)
    {
        var dir = Paths.Episodes(project);
        if (!Directory.Exists(dir)) return [];

        var episodes = new List<Episode>();
        for (var i = 0; i < maxDays; i++)
        {
            var file = Paths.EpisodeFile(project, DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-i)));
            if (!File.Exists(file)) continue;
            foreach (var line in File.ReadAllLines(file))
            {
                try
                {
                    var ep = JsonSerializer.Deserialize<Episode>(line, JsonOptions.Default);
                    if (ep is not null) episodes.Add(ep);
                }
                catch { /* skip malformed lines */ }
            }
        }
        return episodes;
    }

    /// <summary>
    /// Finds the episode by ID in any day file and rewrites that line with the
    /// updated outcome. JSONL files are small (bounded by session budget) so
    /// full rewrite is safe.
    /// </summary>
    public static bool PatchOutcome(string project, string episodeId,
        EpisodeOutcome outcome, DateTimeOffset reflectedAt)
    {
        var dir = Paths.Episodes(project);
        if (!Directory.Exists(dir)) return false;

        foreach (var file in Directory.GetFiles(dir, "*.jsonl"))
        {
            var lines   = File.ReadAllLines(file);
            var patched = false;

            for (var i = 0; i < lines.Length; i++)
            {
                try
                {
                    var ep = JsonSerializer.Deserialize<Episode>(lines[i], JsonOptions.Default);
                    if (ep?.EpisodeId != episodeId) continue;
                    lines[i] = JsonSerializer.Serialize(
                        ep with { Outcome = outcome, ReflectedAt = reflectedAt },
                        JsonOptions.Default);
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
