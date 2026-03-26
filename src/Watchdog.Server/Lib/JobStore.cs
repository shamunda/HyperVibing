// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class JobStore
{
    public static void Append(string project, SubagentJob job)
    {
        var dir = Paths.JobDir(project);
        Directory.CreateDirectory(dir);
        File.AppendAllText(Paths.JobFile(project),
            JsonSerializer.Serialize(job, JsonOptions.Default) + "\n");
    }

    public static SubagentJob? Get(string project, string jobId) =>
        ListRecent(project).FirstOrDefault(j => j.JobId == jobId);

    public static bool Update(string project, string jobId, JobStatus status, string? result)
    {
        var file = Paths.JobFile(project);
        if (!File.Exists(file)) return false;

        var lines   = File.ReadAllLines(file);
        var patched = false;

        for (var i = 0; i < lines.Length; i++)
        {
            try
            {
                var job = JsonSerializer.Deserialize<SubagentJob>(lines[i], JsonOptions.Default);
                if (job?.JobId != jobId) continue;
                lines[i] = JsonSerializer.Serialize(
                    job with { Status = status, CompletedAt = DateTimeOffset.UtcNow, Result = result },
                    JsonOptions.Default);
                patched = true;
                break;
            }
            catch { /* skip */ }
        }

        if (!patched) return false;
        File.WriteAllLines(file, lines);
        return true;
    }

    public static List<SubagentJob> ListRecent(string project, int max = 50)
    {
        var file = Paths.JobFile(project);
        if (!File.Exists(file)) return [];

        return File.ReadAllLines(file)
            .Reverse()
            .Take(max)
            .Select(line =>
            {
                try { return JsonSerializer.Deserialize<SubagentJob>(line, JsonOptions.Default); }
                catch { return null; }
            })
            .Where(j => j is not null)
            .Cast<SubagentJob>()
            .ToList();
    }
}
