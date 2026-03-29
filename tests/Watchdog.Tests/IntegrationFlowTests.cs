using System.Text.Json;
using Microsoft.Data.Sqlite;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;
using Watchdog.Server.Services;
using Watchdog.Server.Tools;

namespace Watchdog.Tests;

public class IntegrationFlowTests : TestFixture
{
    [Fact]
    public void HookPostToolUse_PersistsEvent_AndRaisesStructuredAlert()
    {
        var projectPath = Path.Combine(TestRoot, "workspace", "proj");
        RegisterProject("proj", projectPath);
        File.WriteAllText(Path.Combine(projectPath, ".watchdog"), "proj\n");

        var budget = new BudgetService();
        var nudges = new NudgeService(budget);
        var safety = new SafetyService(nudges);
        var hooks = new HookCommandService(safety);

        var rawEvent = JsonSerializer.Serialize(new
        {
            cwd = projectPath,
            session_id = "session-1",
            tool_name = "bash",
            tool_input = new { command = "git push origin main --force" },
            tool_response = new { }
        }, JsonOptions.Default);

        hooks.RunPostToolUse(rawEvent);

        Assert.Equal(1, StreamStore.LineCount("proj"));
        var alerts = AlertStore.ReadRecent("proj", maxDays: 1);
        Assert.Single(alerts);
        Assert.Equal("force_push", alerts[0].RuleMatched);
    }

    [Fact]
    public void HookPreToolUse_DeliversQueuedMessage_AndMovesItToOutbox()
    {
        var projectPath = Path.Combine(TestRoot, "workspace", "proj");
        RegisterProject("proj", projectPath);
        File.WriteAllText(Path.Combine(projectPath, ".watchdog"), "proj\n");

        Mailbox.Write("proj", MessagePriority.High, MessageType.Nudge,
            "Check your failing tests.", NudgeTone.Redirect, requiresAck: true, expiresIn: TimeSpan.FromMinutes(30));

        var budget = new BudgetService();
        var nudges = new NudgeService(budget);
        var safety = new SafetyService(nudges);
        var hooks = new HookCommandService(safety);

        var rawEvent = JsonSerializer.Serialize(new { cwd = projectPath }, JsonOptions.Default);
        var output = hooks.RunPreToolUse(rawEvent);

        Assert.Contains("[WATCHDOG] [REDIRECT] Check your failing tests.", output, StringComparison.Ordinal);
        var counts = Mailbox.Counts("proj");
        Assert.Equal(0, counts.Inbox);
        Assert.Equal(1, counts.Outbox);
    }

    [Fact]
    public void SqliteStore_RestoresLatestBackup_WhenPrimaryDatabaseIsCorrupted()
    {
        RegisterProject("proj");
        _ = ProjectRegistry.Get("proj");
        Assert.True(File.Exists(Paths.Database));
        Assert.True(File.Exists(Paths.DatabaseBackup));

        WatchdogDataStore.Reset();
        SqliteConnection.ClearAllPools();
        File.WriteAllText(Paths.Database, "not-a-sqlite-database");

        var restored = ProjectRegistry.Get("proj");

        Assert.NotNull(restored);
        Assert.Equal("proj", restored!.Name);
        Assert.NotEmpty(Directory.GetFiles(TestRoot, "watchdog.db.corrupt-*", SearchOption.TopDirectoryOnly));
        Assert.True(
            File.Exists(Paths.Database) || Directory.GetFiles(TestRoot, "watchdog-recovered-*.db", SearchOption.TopDirectoryOnly).Length > 0);
    }

    [Fact]
    public void ObserveTools_GetStatus_UsesIntegratedDiskBackedState()
    {
        var projectPath = Path.Combine(TestRoot, "workspace", "proj");
        RegisterProject("proj", projectPath);
        StreamStore.Append("proj", new StreamEvent(
            Ts: DateTimeOffset.UtcNow,
            SessionId: "session-1",
            Project: "proj",
            ToolName: "write_file",
            ToolInput: "updated README",
            ToolResponse: null,
            Outcome: "success",
            WorkingDirectory: projectPath));

        var budget = new BudgetService();
        var nudges = new NudgeService(budget);
        var safety = new SafetyService(nudges);
        var evidence = new EvidenceService();
        var workflow = new WorkflowService();
        var status = new StatusService(evidence, workflow);
        var patterns = new PatternService();
        var report = new ReportService(budget, patterns);
        var crossProject = new CrossProjectService(status, nudges);
        var tools = new ObserveTools(status, report, crossProject, patterns, new ProjectService(), new SubagentService());

        var payload = tools.GetStatus("proj");
        using var document = JsonDocument.Parse(payload);
        var projects = document.RootElement.GetProperty("projects");

        Assert.Single(projects.EnumerateArray());
        Assert.Equal("proj", projects[0].GetProperty("name").GetString());
        Assert.Equal(1, projects[0].GetProperty("eventCount").GetInt32());
    }
}