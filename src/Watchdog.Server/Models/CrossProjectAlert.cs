// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record CrossProjectAlert(
    string          AlertId,
    string          SourceProject,
    List<string>    TargetProjects,
    string          AlertType,      // "cascading_stall" | "shared_dependency" | "safety_cascade"
    string          Message,
    DateTimeOffset  CreatedAt
);
