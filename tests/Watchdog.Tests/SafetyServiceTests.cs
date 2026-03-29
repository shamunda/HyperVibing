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

public class SafetyServiceTests : TestFixture
{
    private readonly SafetyService _sut;

    public SafetyServiceTests()
    {
        RegisterProject("test-proj");
        var budget = new BudgetService();
        var nudges = new NudgeService(budget);
        _sut = new SafetyService(nudges);
    }

    [Fact]
    public void Evaluate_DestructiveRmRf_ReturnsCriticalAlert()
    {
        var ev = MakeEvent("bash", "rm -rf /");
        var alert = _sut.Evaluate(ev, ["destructive_command"]);

        Assert.NotNull(alert);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
        Assert.Equal("destructive_command", alert.RuleMatched);
    }

    [Fact]
    public void Evaluate_DropTable_ReturnsCriticalAlert()
    {
        var ev = MakeEvent("execute", "DROP TABLE users;");
        var alert = _sut.Evaluate(ev, ["destructive_command"]);

        Assert.NotNull(alert);
        Assert.Equal(AlertSeverity.Critical, alert.Severity);
    }

    [Fact]
    public void Evaluate_ForcePush_ReturnsCriticalAlert()
    {
        var ev = MakeEvent("bash", "git push origin main --force");
        var alert = _sut.Evaluate(ev, ["force_push"]);

        Assert.NotNull(alert);
        Assert.Equal("force_push", alert.RuleMatched);
    }

    [Fact]
    public void Evaluate_SecretInCode_ReturnsWarningAlert()
    {
        var ev = MakeEvent("write_file", "api_key = \"DemoSecretValue1234567890\"");
        var alert = _sut.Evaluate(ev, ["secret_in_code"]);

        Assert.NotNull(alert);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
        Assert.Equal("secret_in_code", alert.RuleMatched);
    }

    [Fact]
    public void Evaluate_SafeCommand_ReturnsNull()
    {
        var ev = MakeEvent("bash", "ls -la /home/user");
        var alert = _sut.Evaluate(ev, ["destructive_command", "force_push", "secret_in_code"]);

        Assert.Null(alert);
    }

    [Fact]
    public void Evaluate_DisabledRule_SkipsIt()
    {
        var ev = MakeEvent("bash", "rm -rf /");
        var alert = _sut.Evaluate(ev, ["force_push"]); // destructive_command not enabled

        Assert.Null(alert);
    }

    [Fact]
    public void Evaluate_NonMatchingToolName_SkipsRule()
    {
        var ev = MakeEvent("read_file", "rm -rf /"); // read_file doesn't match bash|shell|terminal
        var alert = _sut.Evaluate(ev, ["destructive_command"]);

        Assert.Null(alert);
    }

    [Fact]
    public void Evaluate_AccessKeyAssignment_ReturnsWarning()
    {
        var ev = MakeEvent("create_file", "aws_key = \"ExampleAccessKey1234567890\"");
        var alert = _sut.Evaluate(ev, ["secret_in_code"]);

        Assert.NotNull(alert);
        Assert.Equal(AlertSeverity.Warning, alert.Severity);
    }

    [Fact]
    public void Evaluate_TargetedDelete_DoesNotRaiseFalsePositive()
    {
        var ev = MakeEvent("bash", "rm -rf /important/dir");
        var alert = _sut.Evaluate(ev, ["destructive_command"]);

        Assert.Null(alert);
    }

    private static StreamEvent MakeEvent(string toolName, string input) => new(
        Ts:           DateTimeOffset.UtcNow,
        SessionId:    "test-session",
        Project:      "test-proj",
        ToolName:     toolName,
        ToolInput:    input,
        ToolResponse: null,
        Outcome:      "success");
}
