// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class ProjectRegistry
{
    public static ProjectsConfig Load()
    {
        if (!File.Exists(Paths.Projects)) return new ProjectsConfig([]);
        try
        {
            return JsonSerializer.Deserialize<ProjectsConfig>(
                File.ReadAllText(Paths.Projects), JsonOptions.Default) ?? new ProjectsConfig([]);
        }
        catch { return new ProjectsConfig([]); }
    }

    private static void Save(ProjectsConfig config)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Paths.Projects)!);
        File.WriteAllText(Paths.Projects, JsonSerializer.Serialize(config, JsonOptions.Default));
    }

    public static (Project Project, bool Existed) Add(string name, string path)
    {
        var config   = Load();
        var existing = config.Projects.FirstOrDefault(p => p.Name == name);
        if (existing is not null) return (existing, true);

        var project = new Project(name, path, DateTimeOffset.UtcNow, HooksInstalled: false,
            Policy: ProjectWorkflowPolicy.Default);
        Save(config with { Projects = [..config.Projects, project] });
        return (project, false);
    }

    public static Project? Get(string name) =>
        Load().Projects.FirstOrDefault(p => p.Name == name);

    public static void MarkHooksInstalled(string name)
    {
        var config  = Load();
        var updated = config.Projects
            .Select(p => p.Name == name ? p with { HooksInstalled = true } : p)
            .ToList();
        Save(config with { Projects = updated });
    }

    public static Project? UpdatePolicy(string name, ProjectWorkflowPolicy policy)
    {
        var config = Load();
        Project? updatedProject = null;

        var updated = config.Projects
            .Select(p =>
            {
                if (p.Name != name) return p;
                updatedProject = p with { Policy = policy };
                return updatedProject;
            })
            .ToList();

        if (updatedProject is null) return null;
        Save(config with { Projects = updated });
        return updatedProject;
    }

    public static bool Remove(string name)
    {
        var config   = Load();
        var filtered = config.Projects.Where(p => p.Name != name).ToList();
        if (filtered.Count == config.Projects.Count) return false;
        Save(config with { Projects = filtered });
        return true;
    }
}
