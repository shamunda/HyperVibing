// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Background timer that runs every N seconds (default 30).
/// Each tick: reflects on pending nudges, checks all projects for stalls,
/// runs safety evaluation on new stream events, auto-sends nudges for
/// stalled projects, and detects cross-project cascading issues.
///
/// Respects the session budget and the LockFile (single-writer guarantee
/// across multiple MCP server instances).
/// </summary>
public class WatchLoopService(
    DeliberationService  deliberation,
    NudgeService         nudges,
    BudgetService        budget,
    CrossProjectService  crossProject,
    ILogger<WatchLoopService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = SettingsLoader.Get();
        if (!settings.AutoLoopEnabled)
        {
            logger.LogInformation("Auto-loop disabled in settings.");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(5, settings.AutoLoopIntervalSeconds));
        logger.LogInformation("Watch loop started — interval: {Interval}s", interval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(interval, stoppingToken);
            RunTick();
        }
    }

    // ── Internal for testability ──────────────────────────────────────────

    internal void RunTick()
    {
        if (!TryAcquireLock()) return;

        try
        {
            RunDeliberationCycle();
            DetectCrossProjectIssues();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Watch loop tick failed.");
        }
        finally
        {
            LockFile.Release();
        }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private bool TryAcquireLock()
    {
        if (LockFile.Acquire()) return true;

        logger.LogDebug("Lock held by another instance — skipping tick.");
        return false;
    }

    private void RunDeliberationCycle()
    {
        if (!budget.HasBudget) return;

        var decisions = deliberation.RunLoop();

        foreach (var d in decisions.Where(d => d.Action == DeliberationAction.Nudge))
        {
            try
            {
                var content = BuildNudgeContent(d);
                nudges.Send(
                    project:            d.Project,
                    content:            content,
                    priority:           ToneToPriority(d.SuggestedTone),
                    tone:               d.SuggestedTone ?? "reminder",
                    expiresInMins:      30,
                    urgencyScore:       d.UrgencyScore,
                    deliberationSummary: d.Reason,
                    stallSeconds:       d.StallSeconds,
                    budgetRemaining:    d.BudgetRemaining);

                logger.LogInformation("Auto-nudge sent to {Project} (tone: {Tone}, urgency: {Urgency:F2})",
                    d.Project, d.SuggestedTone, d.UrgencyScore);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogWarning("Auto-nudge failed for {Project}: {Message}", d.Project, ex.Message);
            }
        }
    }

    private static string BuildNudgeContent(DeliberationDecision d)
    {
        var tone = (d.SuggestedTone ?? "reminder").ToLowerInvariant();
        return tone switch
        {
            "redirect"   => $"[Watchdog] You appear to be stalled. Consider a different approach. Urgency: {d.UrgencyScore:F2}.",
            "escalation" => $"[Watchdog] Action required. Stalled for an extended period. Urgency: {d.UrgencyScore:F2}. Review your plan.",
            _            => $"[Watchdog] Friendly reminder — check your progress and continue. Urgency: {d.UrgencyScore:F2}.",
        };
    }

    private static string ToneToPriority(string? tone) =>
        (tone ?? "reminder").ToLowerInvariant() switch
        {
            "escalation" => "high",
            "redirect"   => "normal",
            _            => "normal",
        };

    private void DetectCrossProjectIssues()
    {
        var alerts = crossProject.DetectCascading();
        foreach (var alert in alerts)
        {
            logger.LogInformation("Cross-project alert: {Message}", alert.Message);
            crossProject.SendCrossAlert(alert);
        }
    }
}
