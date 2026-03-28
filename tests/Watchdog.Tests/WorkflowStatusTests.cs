// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;
using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class WorkflowStatusTests : TestFixture
{
    [Fact]
    public void GetOne_WithFreshVerification_ElevatesProjectToReview()
    {
        RegisterProject("proj");

        JobStore.Append("proj", new SubagentJob(
            JobId:           Guid.NewGuid().ToString("N"),
            Project:         "proj",
            TaskDescription: "run tests",
            TaskKind:        SubagentTaskKind.RunTests,
            Status:          JobStatus.Completed,
            CreatedAt:       DateTimeOffset.UtcNow.AddMinutes(-2),
            StartedAt:       DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAt:     DateTimeOffset.UtcNow.AddMinutes(-1),
            Command:         "dotnet",
            Arguments:       ["test"],
            ExitCode:        0,
            Result:          "Tests passed.",
            ArtifactPath:    Path.Combine(TestRoot, "artifact.log")));

        var statusService = new StatusService(new EvidenceService(), new WorkflowService());
        var status = statusService.GetOne("proj");

        Assert.NotNull(status);
        Assert.NotNull(status!.Evidence);
        Assert.True(status.Evidence!.HasFreshVerification);
        Assert.NotNull(status.Workflow);
        Assert.Equal(WorkflowStage.Review, status.Workflow!.Stage);
        Assert.False(status.Workflow.NeedsEvidence);
    }

    [Fact]
    public void GetOne_WithPolicyRequiringBuild_KeepsNeedsEvidenceTrue()
    {
        RegisterProject("proj");
        SetProjectPolicy("proj", new ProjectWorkflowPolicy
        {
            ReviewEvidence = new ReviewEvidencePolicy
            {
                RequireFreshTests = true,
                RequireFreshBuild = true
            }
        });

        JobStore.Append("proj", new SubagentJob(
            JobId:           Guid.NewGuid().ToString("N"),
            Project:         "proj",
            TaskDescription: "run tests",
            TaskKind:        SubagentTaskKind.RunTests,
            Status:          JobStatus.Completed,
            CreatedAt:       DateTimeOffset.UtcNow.AddMinutes(-2),
            StartedAt:       DateTimeOffset.UtcNow.AddMinutes(-2),
            CompletedAt:     DateTimeOffset.UtcNow.AddMinutes(-1),
            Command:         "dotnet",
            Arguments:       ["test"],
            ExitCode:        0,
            Result:          "Tests passed.",
            ArtifactPath:    Path.Combine(TestRoot, "artifact-2.log")));

        var statusService = new StatusService(new EvidenceService(), new WorkflowService());
        var status = statusService.GetOne("proj");

        Assert.NotNull(status);
        Assert.NotNull(status!.Evidence);
        Assert.True(status.Evidence!.HasFreshVerification);
        Assert.True(status.Evidence.NeedsVerification);
        Assert.NotNull(status.Workflow);
        Assert.Equal(WorkflowStage.Review, status.Workflow!.Stage);
        Assert.True(status.Workflow.NeedsEvidence);
    }
}