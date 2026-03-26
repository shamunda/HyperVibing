// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Generates a session summary report aggregating episodes, outcomes,
/// tone effectiveness, safety alerts, crystallized patterns, and meta-cognition.
/// </summary>
public class ReportService(BudgetService budget, PatternService patterns)
{
    public SessionReport GenerateReport()
    {
        var settings       = SettingsLoader.Get();
        var projects       = ProjectRegistry.Load().Projects;
        var projectReports = projects.Select(BuildProjectReport).ToList();
        var globalScores   = ProfileStore.Load().Global;
        var totalAlerts    = projectReports.Sum(r => r.AlertCount);
        var allPatterns    = patterns.GetAllPatterns();
        var meta           = ComputeMetaSummary(projectReports);

        // Trigger crystallization for all projects as a side effect
        foreach (var project in projects)
            patterns.Crystallize(project.Name);

        return new SessionReport(
            GeneratedAt:      DateTimeOffset.UtcNow,
            ProjectReports:   projectReports,
            GlobalToneScores: globalScores,
            BudgetUsed:       settings.SessionBudget - budget.Remaining,
            BudgetRemaining:  budget.Remaining,
            AlertsSurfaced:   totalAlerts,
            Patterns:         allPatterns,
            MetaAssessment:   meta);
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static ProjectReport BuildProjectReport(Project project)
    {
        var episodes     = EpisodeStore.ReadRecent(project.Name, maxDays: 1);
        var effective    = episodes.Count(e => e.Outcome is { Effective: true });
        var ineffective  = episodes.Count(e => e.Outcome is { Effective: false });
        var pending      = ReflectionQueue.LoadAll().Count(r => r.Project == project.Name);
        var alerts       = AlertStore.ReadRecent(project.Name, maxDays: 1).Count;

        var avgStall = episodes.Count > 0
            ? episodes.Average(e => e.Trigger.StallSeconds)
            : (double?)null;

        return new ProjectReport(
            Name:               project.Name,
            EpisodesToday:      episodes.Count,
            EffectiveNudges:    effective,
            IneffectiveNudges:  ineffective,
            PendingReflections: pending,
            AlertCount:         alerts,
            AverageStallSeconds: avgStall);
    }

    private static string? ComputeMetaSummary(List<ProjectReport> reports)
    {
        var totalNudges    = reports.Sum(r => r.EffectiveNudges + r.IneffectiveNudges);
        var totalEffective = reports.Sum(r => r.EffectiveNudges);

        if (totalNudges < 1) return "No nudges sent this session.";

        var winRate = (double)totalEffective / totalNudges;
        return winRate switch
        {
            < 0.30 => $"Low effectiveness ({winRate:P0} win rate). Consider changing approach or observing longer before nudging.",
            < 0.60 => $"Moderate effectiveness ({winRate:P0} win rate). Some nudge strategies are working.",
            _      => $"Good effectiveness ({winRate:P0} win rate). Current strategies are working well."
        };
    }
}
