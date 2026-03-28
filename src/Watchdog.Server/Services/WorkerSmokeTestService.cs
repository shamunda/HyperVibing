// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.Json;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Launches a bounded Claude worker smoke test against a monitored project.
/// This validates the real Claude CLI worker path rather than only the plan/preview path.
/// </summary>
public class WorkerSmokeTestService(ProjectService projects, SubagentService subagents)
{
    public WorkerSmokeTestResult Start(string project, int waitForCompletionSeconds = 10)
    {
        var registeredProject = projects.Get(project)
            ?? throw new InvalidOperationException($"Project \"{project}\" is not registered.");

        var taskSpec = BuildTaskSpec(registeredProject);
        var job = subagents.Spawn(project, taskSpec);

        var deadline = DateTimeOffset.UtcNow.AddSeconds(Math.Max(0, waitForCompletionSeconds));
        while (waitForCompletionSeconds > 0 && DateTimeOffset.UtcNow < deadline)
        {
            var current = subagents.GetJob(project, job.JobId);
            if (current is { Status: JobStatus.Completed or JobStatus.Failed })
            {
                return new WorkerSmokeTestResult(
                    Project:                    project,
                    JobId:                      current.JobId,
                    Status:                     current.Status,
                    CompletedWithinWaitWindow:  true,
                    TaskSpec:                   taskSpec,
                    Result:                     current.Result,
                    ArtifactPath:               current.ArtifactPath,
                    ArtifactPreview:            ReadPreview(project, current.JobId));
            }

            Thread.Sleep(250);
        }

        var pending = subagents.GetJob(project, job.JobId) ?? job;
        return new WorkerSmokeTestResult(
            Project:                    project,
            JobId:                      pending.JobId,
            Status:                     pending.Status,
            CompletedWithinWaitWindow:  false,
            TaskSpec:                   taskSpec,
            Result:                     pending.Result,
            ArtifactPath:               pending.ArtifactPath,
            ArtifactPreview:            null);
    }

    internal string BuildTaskSpec(Project project)
    {
        var policy = project.EffectivePolicy;

        var spec = new
        {
            kind = SubagentTaskKind.ClaudeWorker,
            prompt =
                "Watchdog smoke test. Operate read-only. Do not modify files. " +
                "Inspect the current project and return a compact JSON object with these keys: " +
                "smoke_test, cwd_basename, sample_entries, repo_signals, verdict. " +
                "Use only evidence you can gather from file inspection.",
            timeoutSeconds = 90,
            model = policy.WorkerBackend.ClaudeModel,
            effort = policy.WorkerBackend.ClaudeEffort,
            agent = policy.WorkerBackend.ClaudeAgent,
            allowedTools = policy.WorkerBackend.AllowedTools,
            addDirs = policy.WorkerBackend.AdditionalDirectories,
            maxBudgetUsd = policy.WorkerBackend.MaxBudgetUsd,
            appendSystemPrompt =
                "This is a smoke test. Stay read-only, avoid side effects, and produce concise machine-readable output."
        };

        return JsonSerializer.Serialize(spec, JsonOptions.Default);
    }

    private string? ReadPreview(string project, string jobId)
    {
        try
        {
            return subagents.ReadArtifact(project, jobId, maxLines: 40);
        }
        catch
        {
            return null;
        }
    }
}