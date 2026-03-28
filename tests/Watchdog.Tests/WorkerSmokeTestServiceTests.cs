// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.Json;
using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class WorkerSmokeTestServiceTests : TestFixture
{
    [Fact]
    public void BuildTaskSpec_UsesClaudePolicyAndReadOnlyPrompt()
    {
        RegisterProject("proj");
        SetProjectPolicy("proj", new ProjectWorkflowPolicy
        {
            WorkerBackend = new WorkerBackendPolicy
            {
                DefaultBackend = WorkerBackendKind.Claude,
                ClaudeModel = "opus",
                ClaudeEffort = "high",
                ClaudeAgent = "reviewer",
                AllowedTools = ["Read", "Grep", "LS"],
                AdditionalDirectories = ["D:/shared"]
            }
        });

        var project = new ProjectService().Get("proj")!;
        var sut = new WorkerSmokeTestService(new ProjectService(), new SubagentService());

        var json = sut.BuildTaskSpec(project);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        Assert.Equal("ClaudeWorker", root.GetProperty("kind").GetString());
        Assert.Equal("opus", root.GetProperty("model").GetString());
        Assert.Equal("high", root.GetProperty("effort").GetString());
        Assert.Equal("reviewer", root.GetProperty("agent").GetString());
        Assert.Contains("read-only", root.GetProperty("prompt").GetString()!, StringComparison.OrdinalIgnoreCase);
    }
}