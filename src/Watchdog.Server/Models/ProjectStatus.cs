// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record ProjectStatus(
    string          Name,
    string          Path,
    bool            HooksInstalled,
    DateTimeOffset? LastEventAt,
    double?         SecondsSinceLastEvent,
    int             EventCount,
    int             InboxCount,
    int             OutboxCount,
    int             DeadLetterCount,
    bool            IsStalled,
    WorkflowAssessment? Workflow = null,
    EvidenceSummary? Evidence = null
);
