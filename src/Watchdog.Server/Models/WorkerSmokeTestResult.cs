// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record WorkerSmokeTestResult(
    string          Project,
    string          JobId,
    JobStatus       Status,
    bool            CompletedWithinWaitWindow,
    string          TaskSpec,
    string?         Result,
    string?         ArtifactPath,
    string?         ArtifactPreview
);