// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record Project(
    string         Name,
    string         Path,
    DateTimeOffset AddedAt,
    bool           HooksInstalled
);

public record ProjectsConfig(List<Project> Projects);
