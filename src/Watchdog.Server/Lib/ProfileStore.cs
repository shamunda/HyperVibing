// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class ProfileStore
{
    private const double Alpha = 0.2; // EMA learning rate

    public static StrategyProfile Load() => WatchdogDataStore.Current.LoadProfile();

    public static void Save(StrategyProfile profile) => WatchdogDataStore.Current.SaveProfile(profile);

    public static ToneScores GetScores(string project)
    {
        var profile = Load();
        return profile.PerProject.TryGetValue(project, out var scores) ? scores : profile.Global;
    }

    /// <summary>
    /// Pure computation — returns an updated profile without saving.
    /// The caller decides when to persist, keeping I/O separate from logic.
    /// </summary>
    public static StrategyProfile ApplyEma(StrategyProfile profile, string project,
        string tone, bool effective)
    {
        var outcome        = effective ? 1.0 : 0.0;
        var updatedGlobal  = ApplyToScores(profile.Global, tone, outcome);
        var existing       = profile.PerProject.TryGetValue(project, out var s) ? s : profile.Global;
        var updatedProject = ApplyToScores(existing, tone, outcome);

        var newPerProject = new Dictionary<string, ToneScores>(profile.PerProject)
        {
            [project] = updatedProject
        };
        return profile with { Global = updatedGlobal, PerProject = newPerProject, UpdatedAt = DateTimeOffset.UtcNow };
    }

    private static ToneScores ApplyToScores(ToneScores scores, string tone, double outcome) =>
        tone.ToLowerInvariant() switch
        {
            "reminder"   => scores with { Reminder   = Ema(scores.Reminder,   outcome) },
            "redirect"   => scores with { Redirect   = Ema(scores.Redirect,   outcome) },
            "escalation" => scores with { Escalation = Ema(scores.Escalation, outcome) },
            _            => scores
        };

    private static double Ema(double current, double outcome) =>
        Alpha * outcome + (1.0 - Alpha) * current;
}
