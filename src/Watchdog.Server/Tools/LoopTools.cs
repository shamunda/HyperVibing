// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Server.Tools;

/// <summary>
/// MCP tools that expose the deliberative loop to the supervisor (Claude).
/// The supervisor calls these to perceive project health, obtain tone
/// recommendations, and act on them by calling watchdog_send_nudge.
/// </summary>
[McpServerToolType]
public class LoopTools(
    DeliberationService deliberation,
    BudgetService       budget,
    NudgeService        nudges)
{
    [McpServerTool(Name = "watchdog_deliberate")]
    [Description(
        "Run one full deliberation cycle across all registered projects. " +
        "Returns a recommendation per project (Nudge | Skip | AlreadyQueued | BudgetExhausted) " +
        "with urgency score and suggested tone. Does NOT send nudges — call watchdog_send_nudge to act.")]
    public string Deliberate()
    {
        try
        {
            var decisions = deliberation.RunLoop();
            return JsonSerializer.Serialize(new
            {
                cycle_at   = DateTimeOffset.UtcNow,
                budget_remaining = budget.Remaining,
                decisions  = decisions.Select(MapDecision),
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    [McpServerTool(Name = "watchdog_reset_budget")]
    [Description(
        "Reset the session nudge budget back to the configured maximum. " +
        "Call this when a new Claude Code session starts for a monitored project.")]
    public string ResetBudget()
    {
        budget.Reset();
        return JsonSerializer.Serialize(new
        {
            reset    = true,
            remaining = budget.Remaining,
        }, JsonOptions.Indented);
    }

    [McpServerTool(Name = "watchdog_act_on_decision")]
    [Description(
        "Convenience tool: runs deliberation and immediately sends a nudge for every project " +
        "where the recommendation is Nudge. Returns one result entry per project.")]
    public string ActOnDecision()
    {
        try
        {
            var decisions = deliberation.RunLoop();
            var results   = new List<object>();

            foreach (var d in decisions)
            {
                if (d.Action != DeliberationAction.Nudge)
                {
                    results.Add(new { project = d.Project, action = d.Action.ToString(), skipped = true });
                    continue;
                }

                try
                {
                    var content = BuildNudgeContent(d);
                    var msg     = nudges.Send(
                        project:            d.Project,
                        content:            content,
                        priority:           ToneToPriority(d.SuggestedTone),
                        tone:               d.SuggestedTone ?? "reminder",
                        expiresInMins:      30,
                        urgencyScore:       d.UrgencyScore,
                        deliberationSummary: d.Reason,
                        stallSeconds:       d.StallSeconds,
                        budgetRemaining:    d.BudgetRemaining);

                    results.Add(new
                    {
                        project    = d.Project,
                        action     = "Nudge",
                        sent       = true,
                        msg_id     = msg.MsgId,
                        tone       = d.SuggestedTone,
                        urgency    = d.UrgencyScore,
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new { project = d.Project, action = "Nudge", sent = false, error = ex.Message });
                }
            }

            return JsonSerializer.Serialize(new
            {
                acted_at         = DateTimeOffset.UtcNow,
                budget_remaining = budget.Remaining,
                results,
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static object MapDecision(DeliberationDecision d) => new
    {
        project         = d.Project,
        action          = d.Action.ToString(),
        reason          = d.Reason,
        urgency_score   = d.UrgencyScore,
        suggested_tone  = d.SuggestedTone,
        strategy_score  = d.StrategyScore,
        budget_remaining = d.BudgetRemaining,
        stall_seconds   = d.StallSeconds,
        meta_assessment = d.MetaAssessment,
    };

    private static string BuildNudgeContent(DeliberationDecision d)
    {
        var tone = (d.SuggestedTone ?? "reminder").ToLowerInvariant();
        return tone switch
        {
            "redirect"   => $"[Watchdog] You appear to be stalled. Consider a different approach. Urgency: {d.UrgencyScore:F2}.",
            "escalation" => $"[Watchdog] Action required. You have been stalled for an extended period. Urgency: {d.UrgencyScore:F2}. Review your plan immediately.",
            _            => $"[Watchdog] Friendly reminder — check your progress and continue with the current task. Urgency: {d.UrgencyScore:F2}.",
        };
    }

    private static string ToneToPriority(string? tone) =>
        (tone ?? "reminder").ToLowerInvariant() switch
        {
            "escalation" => "high",
            "redirect"   => "normal",
            _            => "normal",
        };

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = true, message }, JsonOptions.Indented);
}
