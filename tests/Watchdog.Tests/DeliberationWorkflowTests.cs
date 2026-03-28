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

public class DeliberationWorkflowTests : TestFixture
{
    [Fact]
    public void RunLoop_WhenVerificationMissingAndProjectStalled_RedirectsForEvidence()
    {
        RegisterProject("proj");
        StreamStore.Append("proj", MakeEvent("proj", DateTimeOffset.UtcNow.AddMinutes(-5)));

        var sut = CreateSut();
        var decision = Assert.Single(sut.RunLoop());

        Assert.Equal(DeliberationAction.Nudge, decision.Action);
        Assert.Equal("redirect", decision.SuggestedTone);
        Assert.Equal(WorkflowStage.Implement, decision.WorkflowStage);
        Assert.True(decision.NeedsEvidence);
    }

    [Fact]
    public void RunLoop_WhenVerificationJobIsRunning_SkipsNudge()
    {
        RegisterProject("proj");
        StreamStore.Append("proj", MakeEvent("proj", DateTimeOffset.UtcNow.AddMinutes(-5)));
        JobStore.Append("proj", new SubagentJob(
            JobId:           Guid.NewGuid().ToString("N"),
            Project:         "proj",
            TaskDescription: "run tests",
            TaskKind:        SubagentTaskKind.RunTests,
            Status:          JobStatus.Running,
            CreatedAt:       DateTimeOffset.UtcNow.AddMinutes(-1),
            StartedAt:       DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedAt:     null,
            Command:         "dotnet",
            Arguments:       ["test"],
            ExitCode:        null,
            Result:          "Running dotnet test",
            ArtifactPath:    null));

        var sut = CreateSut();
        var decision = Assert.Single(sut.RunLoop());

        Assert.Equal(DeliberationAction.Skip, decision.Action);
        Assert.Equal(WorkflowStage.Validate, decision.WorkflowStage);
        Assert.False(decision.NeedsEvidence);
    }

    private static StreamEvent MakeEvent(string project, DateTimeOffset ts) => new(
        Ts:           ts,
        SessionId:    "test-session",
        Project:      project,
        ToolName:     "edit",
        ToolInput:    "changed code",
        ToolResponse: null,
        Outcome:      "success");

    private static DeliberationService CreateSut()
    {
        var statusService = new StatusService(new EvidenceService(), new WorkflowService());
        return new DeliberationService(statusService, new UrgencyService(), new BudgetService(), new ReflectionService());
    }
}