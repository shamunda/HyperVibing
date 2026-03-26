using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Closes the feedback loop: evaluates nudges whose reflection window has elapsed,
/// updates the strategy profile via EMA, and patches episode records with outcomes.
///
/// Called at the start of each deliberation cycle so fresh signal informs
/// the tone-selection step that follows.
/// </summary>
public class ReflectionService
{
    /// <summary>
    /// Processes all <see cref="PendingReflection"/> entries whose
    /// <c>ReflectAfter</c> timestamp has passed.
    /// </summary>
    /// <returns>Number of reflections resolved in this cycle.</returns>
    public int ProcessDue()
    {
        var due       = ReflectionQueue.GetDue();
        var resolved  = 0;

        foreach (var pending in due)
        {
            var effective = EvaluateEffectiveness(pending);
            CommitOutcome(pending, effective);
            resolved++;
        }

        return resolved;
    }

    // ── Private ───────────────────────────────────────────────────────────

    /// <summary>
    /// An nudge is considered effective if the stream cursor advanced since the
    /// nudge was sent — i.e., the agent produced new tool calls after receiving
    /// the message.
    /// </summary>
    private static bool EvaluateEffectiveness(PendingReflection pending)
    {
        var currentCursor = StreamStore.LineCount(pending.Project);
        return currentCursor > pending.CursorAtNudge;
    }

    private static void CommitOutcome(PendingReflection pending, bool effective)
    {
        var cursorDelta = Math.Max(0, StreamStore.LineCount(pending.Project) - pending.CursorAtNudge);

        var outcome = new EpisodeOutcome(
            ActivityResumed: effective,
            Effective:       effective,
            CursorDelta:     cursorDelta);

        // 1. Patch the episode file with the concrete outcome.
        EpisodeStore.PatchOutcome(
            pending.Project, pending.EpisodeId, outcome, DateTimeOffset.UtcNow);

        // 2. Update the strategy profile (EMA) and persist.
        var profile = ProfileStore.Load();
        var updated = ProfileStore.ApplyEma(profile, pending.Project, pending.Tone, effective);
        ProfileStore.Save(updated);

        // 3. Remove from pending queue — done regardless of effectiveness so we
        //    don't retry the same episode forever.
        ReflectionQueue.Remove(pending.EpisodeId);
    }
}
