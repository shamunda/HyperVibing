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
/// Discovers statistically meaningful patterns from episode history.
/// A pattern crystallizes when ≥5 episodes with the same trigger+tone combination
/// show a success rate that differs from the current EMA score by ≥20%.
/// Patterns are persisted and surfaced in self-reports.
/// </summary>
public class PatternService
{
    private const int    MinSampleSize = 5;
    private const double MinDelta      = 0.20;

    /// <summary>
    /// Analyzes recent episodes for a project and discovers new patterns.
    /// Returns newly crystallized patterns (also persisted to <see cref="PatternStore"/>).
    /// </summary>
    public List<CrystallizedPattern> Crystallize(string project)
    {
        var episodes = EpisodeStore.ReadRecent(project, maxDays: 7);
        var resolved = episodes.Where(e => e.Outcome is not null).ToList();
        if (resolved.Count < MinSampleSize) return [];

        var scores      = ProfileStore.GetScores(project);
        var newPatterns = new List<CrystallizedPattern>();
        var existing    = PatternStore.ReadForProject(project);

        var groups = resolved
            .GroupBy(e => (e.Trigger.Type, e.Action.Tone))
            .Where(g => g.Count() >= MinSampleSize);

        foreach (var group in groups)
        {
            var (triggerType, tone) = group.Key;
            var sampleSize  = group.Count();
            var effectiveCount = group.Count(e => e.Outcome!.Effective);
            var successRate = (double)effectiveCount / sampleSize;
            var currentEma  = GetEmaForTone(scores, tone);
            var delta       = successRate - currentEma;

            if (Math.Abs(delta) < MinDelta) continue;

            // Skip if an equivalent pattern was already discovered recently
            if (existing.Any(p => p.TriggerType == triggerType && p.Tone == tone
                                  && Math.Abs(p.SuccessRate - successRate) < 0.05))
                continue;

            var direction = delta > 0 ? "more effective than" : "less effective than";
            var pattern = new CrystallizedPattern(
                PatternId:    Guid.NewGuid().ToString("N"),
                Project:      project,
                TriggerType:  triggerType,
                Tone:         tone,
                SampleSize:   sampleSize,
                SuccessRate:  Math.Round(successRate, 3),
                CurrentEma:   Math.Round(currentEma, 3),
                Delta:        Math.Round(delta, 3),
                DiscoveredAt: DateTimeOffset.UtcNow,
                Description:  $"Tone '{tone}' on '{triggerType}' triggers is {direction} " +
                              $"expected (empirical: {successRate:P0}, EMA: {currentEma:P0}, " +
                              $"Δ: {delta:+0.0%;-0.0%}, n={sampleSize}).");

            PatternStore.Append(pattern);
            newPatterns.Add(pattern);
        }

        return newPatterns;
    }

    /// <summary>
    /// Returns all known patterns for a project.
    /// </summary>
    public List<CrystallizedPattern> GetPatterns(string project) =>
        PatternStore.ReadForProject(project);

    /// <summary>
    /// Returns all known patterns across all projects.
    /// </summary>
    public List<CrystallizedPattern> GetAllPatterns() =>
        PatternStore.Load();

    // ── Private ───────────────────────────────────────────────────────────

    private static double GetEmaForTone(ToneScores scores, string tone) =>
        tone.ToLowerInvariant() switch
        {
            "reminder"   => scores.Reminder,
            "redirect"   => scores.Redirect,
            "escalation" => scores.Escalation,
            _            => 0.5
        };
}
