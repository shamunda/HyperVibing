// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Business logic for registering and managing monitored projects.
/// Coordinates: registry persistence, mailbox provisioning, identity file.
/// </summary>
public class ProjectService
{
    public (Project Project, bool Existed) Add(string name, string path)
    {
        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"Project directory does not exist: {path}");

        var result = ProjectRegistry.Add(name, path);
        Mailbox.Ensure(name);

        // .watchdog identity file — hooks read this to resolve the project name from cwd
        File.WriteAllText(Path.Combine(path, ".watchdog"), name + Environment.NewLine);

        return result;
    }

    public bool Remove(string name) => ProjectRegistry.Remove(name);

    public Project? Get(string name) => ProjectRegistry.Get(name);

    public ProjectsConfig List() => ProjectRegistry.Load();
}
