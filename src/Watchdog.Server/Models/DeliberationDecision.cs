// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public enum DeliberationAction
{
    Nudge,            // supervisor should send a nudge
    Skip,             // not stalled or urgency below threshold
    AlreadyQueued,    // a nudge is already in the project's inbox
    BudgetExhausted   // session budget is at zero
}

/// <summary>
/// Output of the deliberation loop for one project.
/// This is a recommendation — the supervisor (Claude) acts on it;
/// the server does not auto-send nudges.
/// </summary>
public record DeliberationDecision(
    string             Project,
    DeliberationAction Action,
    string             Reason,
    double?            UrgencyScore,
    string?            SuggestedTone,   // "reminder" | "redirect" | "escalation"
    int                BudgetRemaining,
    double?            StrategyScore,   // effectiveness of SuggestedTone for this project
    double             StallSeconds = 0,
    string?            MetaAssessment = null
);
