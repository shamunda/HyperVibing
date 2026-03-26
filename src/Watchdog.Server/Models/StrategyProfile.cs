// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

/// <summary>
/// EMA effectiveness scores for each nudge tone. All values are [0.0–1.0].
/// Start at 0.5 (neutral) so the cold-start state is distinguishable from
/// "this tone never works" (near 0) or "this tone always works" (near 1).
/// </summary>
public record ToneScores(
    double Reminder   = 0.5,
    double Redirect   = 0.5,
    double Escalation = 0.5
)
{
    public static readonly ToneScores Default = new();
}

/// <summary>
/// On-disk structure of profile.json.
/// Global scores apply when no per-project data exists for a project.
/// </summary>
public record StrategyProfile(
    ToneScores                     Global,
    Dictionary<string, ToneScores> PerProject,
    DateTimeOffset                 UpdatedAt
)
{
    public static StrategyProfile Empty() =>
        new(ToneScores.Default, [], DateTimeOffset.UtcNow);
}
