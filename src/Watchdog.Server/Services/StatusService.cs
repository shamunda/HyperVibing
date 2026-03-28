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
/// Business logic for computing project health status.
/// Aggregates data from stream, mailbox, and registry into a unified snapshot.
/// </summary>
public class StatusService(EvidenceService evidenceService, WorkflowService workflowService)
{
    public List<ProjectStatus> GetAll() =>
        ProjectRegistry.Load().Projects.Select(Compute).ToList();

    public ProjectStatus? GetOne(string name)
    {
        var project = ProjectRegistry.Get(name);
        return project is null ? null : Compute(project);
    }

    public StreamSlice ReadStream(string project, int cursor, int limit) =>
        StreamStore.ReadSince(project, cursor, Math.Clamp(limit, 1, 200));

    public int StreamLineCount(string project) => StreamStore.LineCount(project);

    // ── Private ───────────────────────────────────────────────────────────

    private ProjectStatus Compute(Project p)
    {
        var settings  = SettingsLoader.Get();
        var lastEvent = StreamStore.LastEvent(p.Name);
        var total     = StreamStore.LineCount(p.Name);
        var counts    = Mailbox.Counts(p.Name);
        var now       = DateTimeOffset.UtcNow;

        var lastAt  = lastEvent?.Ts;
        var seconds = lastAt.HasValue ? (now - lastAt.Value).TotalSeconds : (double?)null;
        var stalled = seconds.HasValue && seconds.Value > settings.StallThresholdSeconds;
        var evidence = evidenceService.Summarize(p.Name);

        var baseStatus = new ProjectStatus(
            Name:                  p.Name,
            Path:                  p.Path,
            HooksInstalled:        p.HooksInstalled,
            LastEventAt:           lastAt,
            SecondsSinceLastEvent: seconds,
            EventCount:            total,
            InboxCount:            counts.Inbox,
            OutboxCount:           counts.Outbox,
            DeadLetterCount:       counts.DeadLetter,
            IsStalled:             stalled,
            Evidence:              evidence);

        return baseStatus with
        {
            Workflow = workflowService.Assess(baseStatus, evidence)
        };
    }
}
