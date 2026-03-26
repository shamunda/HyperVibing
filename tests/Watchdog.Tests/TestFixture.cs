// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;

namespace Watchdog.Tests;

/// <summary>
/// Base class for tests that need file system isolation.
/// Redirects all Paths to a temp directory and cleans up after.
/// </summary>
public abstract class TestFixture : IDisposable
{
    protected string TestRoot { get; }

    protected TestFixture()
    {
        TestRoot = Path.Combine(Path.GetTempPath(), "watchdog-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(TestRoot);
        Paths.SetRoot(TestRoot);
        SettingsLoader.Invalidate();
    }

    /// <summary>
    /// Registers a project in the test registry so services can find it.
    /// </summary>
    protected void RegisterProject(string name, string? path = null)
    {
        var projectPath = path ?? Path.Combine(TestRoot, "projects", name);
        Directory.CreateDirectory(projectPath);
        ProjectRegistry.Add(name, projectPath);
        Mailbox.Ensure(name);
    }

    public void Dispose()
    {
        Paths.ResetRoot();
        SettingsLoader.Invalidate();
        try { Directory.Delete(TestRoot, recursive: true); } catch { /* best effort */ }
        GC.SuppressFinalize(this);
    }
}
