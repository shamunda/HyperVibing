// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class ProjectPolicyTests : TestFixture
{
    [Fact]
    public void UpdatePolicy_PersistsBackendAndEvidenceRequirements()
    {
        RegisterProject("proj");
        var sut = new ProjectService();

        var updated = sut.UpdatePolicy("proj", new ProjectWorkflowPolicy
        {
            ReviewEvidence = new ReviewEvidencePolicy
            {
                RequireFreshTests = true,
                RequireFreshBuild = true,
                RequireNoWarnings = true,
                EvidenceFreshnessMinutes = 10
            },
            WorkerBackend = new WorkerBackendPolicy
            {
                DefaultBackend = WorkerBackendKind.Claude,
                ClaudeModel = "opus",
                ClaudeEffort = "high",
                PermissionMode = "dontAsk",
                AllowedTools = ["Read", "Grep"],
                AdditionalDirectories = ["D:/shared"]
            }
        });

        Assert.Equal(WorkerBackendKind.Claude, updated.EffectivePolicy.WorkerBackend.DefaultBackend);

        var reloaded = sut.GetPolicy("proj");
        Assert.True(reloaded.ReviewEvidence.RequireFreshBuild);
        Assert.True(reloaded.ReviewEvidence.RequireNoWarnings);
        Assert.Equal(10, reloaded.ReviewEvidence.EvidenceFreshnessMinutes);
        Assert.Equal("opus", reloaded.WorkerBackend.ClaudeModel);
        Assert.Equal("dontAsk", reloaded.WorkerBackend.PermissionMode);
        Assert.Equal(["Read", "Grep"], reloaded.WorkerBackend.AllowedTools);
    }
}