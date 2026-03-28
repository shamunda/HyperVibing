// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public enum JobStatus { Pending, Running, Completed, Failed }

public enum SubagentTaskKind
{
    Command,
    RunTests,
    Build,
    DiffAnalysis,
    ClaudeWorker
}

public record SubagentJob(
    string          JobId,
    string          Project,
    string          TaskDescription,
    SubagentTaskKind TaskKind,
    JobStatus       Status,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string?         Command,
    string[]        Arguments,
    int?            ExitCode,
    string?         Result,
    string?         ArtifactPath
);
