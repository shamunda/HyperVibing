using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Business logic for sending nudge messages to monitored projects.
/// Validates inputs, enforces the session budget, creates an episode record,
/// and enqueues a pending reflection for outcome evaluation.
/// </summary>
public class NudgeService(BudgetService budget)
{
    /// <param name="project">Registered project name.</param>
    /// <param name="content">Nudge message body.</param>
    /// <param name="priority">low | normal | high | critical</param>
    /// <param name="tone">reminder | redirect | escalation</param>
    /// <param name="expiresInMins">TTL in minutes (1–1440).</param>
    /// <param name="urgencyScore">Urgency computed by the deliberation loop (optional).</param>
    /// <param name="deliberationSummary">Human-readable reason from the deliberation loop (optional).</param>
    public MailboxMessage Send(
        string  project,
        string  content,
        string  priority             = "normal",
        string  tone                 = "reminder",
        int     expiresInMins        = 30,
        double? urgencyScore         = null,
        string? deliberationSummary  = null,
        double  stallSeconds         = 0,
        int     budgetRemaining      = 0)
    {
        if (ProjectRegistry.Get(project) is null)
            throw new InvalidOperationException($"Project \"{project}\" is not registered.");

        if (!Enum.TryParse<MessagePriority>(priority, ignoreCase: true, out var pri))
            throw new ArgumentException($"Invalid priority \"{priority}\". Use: low, normal, high, critical.");

        if (!Enum.TryParse<NudgeTone>(tone, ignoreCase: true, out var nudgeTone))
            throw new ArgumentException($"Invalid tone \"{tone}\". Use: reminder, redirect, escalation.");

        if (!budget.TryConsume())
            throw new InvalidOperationException("Session nudge budget is exhausted. Reset the budget or wait for the next session.");

        var expiry   = TimeSpan.FromMinutes(Math.Clamp(expiresInMins, 1, 1440));
        var settings = SettingsLoader.Get();
        var msg      = Mailbox.Write(project, pri, MessageType.Nudge, content, nudgeTone,
                                     requiresAck: true, expiresIn: expiry);

        // Record the episode and queue outcome evaluation.
        var cursorAtNudge = StreamStore.LineCount(project);
        var episode       = BuildEpisode(project, msg, tone, urgencyScore,
                                         deliberationSummary, cursorAtNudge,
                                         stallSeconds, budgetRemaining);
        EpisodeStore.Append(project, episode);

        var reflection = new PendingReflection(
            EpisodeId:    episode.EpisodeId,
            Project:      project,
            MsgId:        msg.MsgId,
            Tone:         tone.ToLowerInvariant(),
            NudgedAt:     episode.NudgedAt,
            ReflectAfter: episode.NudgedAt.AddMinutes(settings.ReflectionWindowMinutes),
            CursorAtNudge: cursorAtNudge);

        ReflectionQueue.Enqueue(reflection);

        return msg;
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static Episode BuildEpisode(
        string project, MailboxMessage msg, string tone,
        double? urgencyScore, string? deliberationSummary, int cursorAtNudge,
        double stallSeconds, int budgetRemaining)
    {
        var trigger  = new EpisodeTrigger(
            Type:             "stall",
            UrgencyScore:     urgencyScore ?? 0.0,
            StallSeconds:     stallSeconds,
            BudgetRemaining:  budgetRemaining);

        var action = new EpisodeAction(
            Tone:    tone.ToLowerInvariant(),
            MsgId:   msg.MsgId,
            Content: msg.Content);

        return new Episode(
            EpisodeId:            Guid.NewGuid().ToString("N"),
            Project:              project,
            NudgedAt:             DateTimeOffset.UtcNow,
            Trigger:              trigger,
            DeliberationSummary:  deliberationSummary ?? "Manual nudge.",
            Action:               action,
            Outcome:              null,
            ReflectedAt:          null);
    }
}
