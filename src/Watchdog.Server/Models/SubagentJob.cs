// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public enum JobStatus { Pending, Running, Completed, Failed }

public record SubagentJob(
    string          JobId,
    string          Project,
    string          TaskDescription,
    JobStatus       Status,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? CompletedAt,
    string?         Result
);
