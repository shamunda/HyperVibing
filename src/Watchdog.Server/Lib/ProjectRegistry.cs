// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class ProjectRegistry
{
    public static ProjectsConfig Load() => WatchdogDataStore.Current.LoadProjects();

    public static (Project Project, bool Existed) Add(string name, string path)
        => WatchdogDataStore.Current.AddProject(name, path, ProjectWorkflowPolicy.Default);

    public static Project? Get(string name) => WatchdogDataStore.Current.GetProject(name);

    public static void MarkHooksInstalled(string name) =>
        WatchdogDataStore.Current.MarkProjectHooksInstalled(name);

    public static Project? UpdatePolicy(string name, ProjectWorkflowPolicy policy)
        => WatchdogDataStore.Current.UpdateProjectPolicy(name, policy);

    public static bool Remove(string name) => WatchdogDataStore.Current.RemoveProject(name);
}
