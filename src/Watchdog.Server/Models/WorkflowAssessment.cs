// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public enum WorkflowStage
{
    Observe,
    Implement,
    Validate,
    Refine,
    Review,
    Escalate
}

public record WorkflowAssessment(
    WorkflowStage Stage,
    string        Summary,
    bool          NeedsAttention,
    bool          NeedsEvidence,
    string?       BlockingReason
);