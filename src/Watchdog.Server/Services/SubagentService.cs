using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Interface for spawning bounded subagent processes.
/// Currently a stub — records the job and returns immediately with a placeholder result.
/// Full implementation will launch a bounded Claude CLI process,
/// capture output to a shared mailbox, and return a job ID.
///
/// The stub allows all downstream plumbing (tools, job tracking, cross-project alerts)
/// to work immediately. The actual process launch is security-sensitive and
/// will be implemented behind a human-gated approval mechanism.
/// </summary>
public class SubagentService
{
    /// <summary>
    /// Spawns a subagent task for the given project.
    /// Currently a stub — records the job and returns a placeholder result.
    /// </summary>
    public SubagentJob Spawn(string project, string taskDescription)
    {
        if (ProjectRegistry.Get(project) is null)
            throw new InvalidOperationException($"Project \"{project}\" is not registered.");

        var job = new SubagentJob(
            JobId:           Guid.NewGuid().ToString("N"),
            Project:         project,
            TaskDescription: taskDescription,
            Status:          JobStatus.Completed,
            CreatedAt:       DateTimeOffset.UtcNow,
            CompletedAt:     DateTimeOffset.UtcNow,
            Result:          "Subagent spawning is not yet implemented. " +
                             "This is a stub — the job was recorded for tracking purposes. " +
                             "Full implementation will launch a bounded Claude CLI process.");

        JobStore.Append(project, job);
        return job;
    }

    public SubagentJob? GetJob(string project, string jobId) =>
        JobStore.Get(project, jobId);

    public List<SubagentJob> ListJobs(string project) =>
        JobStore.ListRecent(project);
}
