// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record EvidenceSummary(
    int             RunningJobs,
    int             PendingJobs,
    int             FailedJobs,
    int             CompletedJobs,
    int             CriticalAlerts,
    int             WarningAlerts,
    DateTimeOffset? LastBuildAt,
    bool?           LastBuildSucceeded,
    DateTimeOffset? LastTestAt,
    bool?           LastTestSucceeded,
    DateTimeOffset? LastVerificationAt,
    bool            HasFreshVerification,
    bool            NeedsVerification,
    string?         LatestFailure,
    string[]        Findings
);