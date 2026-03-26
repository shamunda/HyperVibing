// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.RegularExpressions;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Detects user correction signals in tool-use events.
/// A correction indicates the user is overriding or undoing the project agent's action,
/// which implies the last nudge (if any) may have been unhelpful.
/// </summary>
public class CorrectionService
{
    private static readonly Regex CorrectionPattern = new(
        @"\b(no[,.]?\s+(don'?t|stop|wrong|that'?s\s+wrong))|" +
        @"\b(stop\s+(doing|that))|" +
        @"\b(undo\s+(that|this|it))|" +
        @"\b(revert\s+(that|this|it))|" +
        @"\b(wrong\s+approach)|" +
        @"\b(ignore\s+(that|previous|last))|" +
        @"\b(that\s+was\s+wrong)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Checks a stream event for correction signals in the tool input.
    /// Returns a <see cref="CorrectionSignal"/> if detected, null otherwise.
    /// </summary>
    public CorrectionSignal? Detect(StreamEvent ev)
    {
        var inputText = ev.ToolInput?.ToString() ?? "";
        if (string.IsNullOrWhiteSpace(inputText)) return null;

        if (!CorrectionPattern.IsMatch(inputText)) return null;

        return new CorrectionSignal(
            Project:    ev.Project,
            DetectedAt: ev.Ts,
            ToolName:   ev.ToolName,
            SignalType:  "explicit_correction",
            Content:    inputText.Length > 200 ? inputText[..200] : inputText);
    }

    /// <summary>
    /// Applies a correction signal: finds the most recent pending reflection for the project
    /// and marks the associated episode as ineffective (forces a negative EMA update).
    /// </summary>
    public void Apply(CorrectionSignal signal)
    {
        var pending = ReflectionQueue.LoadAll()
            .Where(r => r.Project == signal.Project)
            .OrderByDescending(r => r.NudgedAt)
            .FirstOrDefault();

        if (pending is null) return;

        var outcome = new EpisodeOutcome(
            ActivityResumed: false,
            Effective:       false,
            CursorDelta:     0);

        EpisodeStore.PatchOutcome(pending.Project, pending.EpisodeId, outcome, DateTimeOffset.UtcNow);

        var profile = ProfileStore.Load();
        var updated = ProfileStore.ApplyEma(profile, pending.Project, pending.Tone, effective: false);
        ProfileStore.Save(updated);

        ReflectionQueue.Remove(pending.EpisodeId);
    }

    /// <summary>
    /// Analyzes correction trends for a project: if ≥3 corrections after the same tone
    /// within the last 7 days, applies aggressive EMA decay to that tone.
    /// Returns the tones that were decayed.
    /// </summary>
    public List<string> AnalyzeTrends(string project)
    {
        var recentEpisodes = EpisodeStore.ReadRecent(project, maxDays: 7);
        var decayedTones = new List<string>();

        var correctedEpisodes = recentEpisodes
            .Where(e => e.Outcome is { Effective: false })
            .GroupBy(e => e.Action.Tone)
            .Where(g => g.Count() >= 3);

        var profile = ProfileStore.Load();
        foreach (var group in correctedEpisodes)
        {
            // Aggressive decay: α=0.5 with outcome=0 (ineffective)
            profile = ApplyAggressiveDecay(profile, project, group.Key);
            decayedTones.Add(group.Key);
        }

        if (decayedTones.Count > 0)
            ProfileStore.Save(profile);

        return decayedTones;
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static StrategyProfile ApplyAggressiveDecay(StrategyProfile profile, string project, string tone)
    {
        const double aggressiveAlpha = 0.5;
        var scores = profile.PerProject.TryGetValue(project, out var s) ? s : profile.Global;

        var updated = tone.ToLowerInvariant() switch
        {
            "reminder"   => scores with { Reminder   = aggressiveAlpha * 0.0 + (1.0 - aggressiveAlpha) * scores.Reminder },
            "redirect"   => scores with { Redirect   = aggressiveAlpha * 0.0 + (1.0 - aggressiveAlpha) * scores.Redirect },
            "escalation" => scores with { Escalation = aggressiveAlpha * 0.0 + (1.0 - aggressiveAlpha) * scores.Escalation },
            _            => scores
        };

        var newPerProject = new Dictionary<string, ToneScores>(profile.PerProject) { [project] = updated };
        return profile with { PerProject = newPerProject, UpdatedAt = DateTimeOffset.UtcNow };
    }
}
