// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class JobStore
{
    public static void Append(string project, SubagentJob job) => WatchdogDataStore.Current.AppendJob(project, job);

    public static bool Replace(string project, SubagentJob updatedJob) =>
        WatchdogDataStore.Current.ReplaceJob(project, updatedJob);

    public static SubagentJob? Get(string project, string jobId) => WatchdogDataStore.Current.GetJob(project, jobId);

    public static bool Update(string project, string jobId, JobStatus status, string? result)
    {
        var existing = Get(project, jobId);
        return existing is not null && Replace(project, existing with
        {
            Status = status,
            CompletedAt = DateTimeOffset.UtcNow,
            Result = result
        });
    }

    public static List<SubagentJob> ListRecent(string project, int max = 50) =>
        WatchdogDataStore.Current.ListRecentJobs(project, max);

    public static List<SubagentJob> ListAll(string project) => WatchdogDataStore.Current.ListAllJobs(project);
}
