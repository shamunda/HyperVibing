// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class SubagentServiceTests : TestFixture
{
    private readonly SubagentService _sut = new();

    [Fact]
    public void Spawn_JsonCommandSpec_RunsAndPersistsArtifact()
    {
        RegisterProject("proj");

        var job = _sut.Spawn("proj", """
            {"kind":"Command","command":"dotnet","args":["--version"],"timeoutSeconds":10}
            """);

        var completed = WaitForJob("proj", job.JobId);

        Assert.Equal(Watchdog.Server.Models.JobStatus.Completed, completed.Status);
        Assert.Equal(Watchdog.Server.Models.SubagentTaskKind.Command, completed.TaskKind);
        Assert.Equal(0, completed.ExitCode);
        Assert.NotNull(completed.StartedAt);
        Assert.NotNull(completed.CompletedAt);
        Assert.False(string.IsNullOrWhiteSpace(completed.ArtifactPath));
        Assert.True(File.Exists(completed.ArtifactPath));
        Assert.False(string.IsNullOrWhiteSpace(File.ReadAllText(completed.ArtifactPath!)));
    }

    [Fact]
    public void Spawn_DisallowedCommand_Throws()
    {
        RegisterProject("proj");

        var act = () => _sut.Spawn("proj", """
            {"kind":"Command","command":"pwsh","args":["-NoProfile","-Command","Write-Host hi"]}
            """);

        var ex = Assert.Throws<InvalidOperationException>(act);
        Assert.Contains("not allowed", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ReadArtifact_ReturnsCapturedOutput()
    {
        RegisterProject("proj");

        var job = _sut.Spawn("proj", """
            {"kind":"Command","command":"dotnet","args":["--version"],"timeoutSeconds":10}
            """);

        var completed = WaitForJob("proj", job.JobId);
        var content = _sut.ReadArtifact("proj", completed.JobId, 20);

        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Fact]
    public void PreviewPlan_UsesClaudeBackendFromProjectPolicy()
    {
        RegisterProject("proj");
        SetProjectPolicy("proj", new Watchdog.Server.Models.ProjectWorkflowPolicy
        {
            WorkerBackend = new Watchdog.Server.Models.WorkerBackendPolicy
            {
                DefaultBackend = Watchdog.Server.Models.WorkerBackendKind.Claude,
                ClaudeModel = "sonnet",
                ClaudeEffort = "high",
                PermissionMode = "plan",
                AllowedTools = ["Read", "Grep"]
            }
        });

        var preview = _sut.PreviewPlan("proj", "Review the current diff for regressions.");

        Assert.Equal(Watchdog.Server.Models.SubagentTaskKind.ClaudeWorker, preview.TaskKind);
        Assert.Equal("claude", preview.Command);
        Assert.Contains("--print", preview.Arguments);
        Assert.Contains("--model", preview.Arguments);
        Assert.Contains("sonnet", preview.Arguments);
        Assert.Contains("Review the current diff for regressions.", preview.Arguments);
    }

    private Watchdog.Server.Models.SubagentJob WaitForJob(string project, string jobId)
    {
        for (var i = 0; i < 100; i++)
        {
            var job = _sut.GetJob(project, jobId);
            if (job is { Status: not Watchdog.Server.Models.JobStatus.Pending and not Watchdog.Server.Models.JobStatus.Running })
                return job;

            Thread.Sleep(100);
        }

        throw new TimeoutException($"Job {jobId} did not complete in time.");
    }
}