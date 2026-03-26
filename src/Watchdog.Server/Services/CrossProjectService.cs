using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Detects cascading issues across multiple projects.
/// When multiple projects stall simultaneously or a safety alert in one project
/// may affect others, surfaces cross-project alerts to the supervisor.
/// </summary>
public class CrossProjectService(StatusService status, NudgeService nudges)
{
    private readonly List<CrossProjectAlert> _recentAlerts = [];

    /// <summary>
    /// Checks for cascading stalls: if 2+ projects are stalled simultaneously,
    /// creates a cross-project alert so the supervisor can correlate them.
    /// </summary>
    public List<CrossProjectAlert> DetectCascading()
    {
        var allStatus    = status.GetAll();
        var stalledNames = allStatus.Where(s => s.IsStalled).Select(s => s.Name).ToList();

        if (stalledNames.Count < 2) return [];

        var alert = new CrossProjectAlert(
            AlertId:        Guid.NewGuid().ToString("N"),
            SourceProject:  stalledNames[0],
            TargetProjects: stalledNames.Skip(1).ToList(),
            AlertType:      "cascading_stall",
            Message:        $"Multiple projects stalled simultaneously: {string.Join(", ", stalledNames)}. " +
                            "These may share a dependency or blocking issue.",
            CreatedAt:      DateTimeOffset.UtcNow);

        _recentAlerts.Add(alert);
        return [alert];
    }

    /// <summary>
    /// Sends a cross-project alert as a nudge to all target project mailboxes.
    /// </summary>
    public void SendCrossAlert(CrossProjectAlert alert)
    {
        var allTargets = new List<string> { alert.SourceProject };
        allTargets.AddRange(alert.TargetProjects);

        foreach (var project in allTargets)
        {
            try
            {
                nudges.Send(
                    project:  project,
                    content:  $"[CROSS-PROJECT] {alert.Message}",
                    priority: "high",
                    tone:     "redirect");
            }
            catch (InvalidOperationException)
            {
                // Budget exhausted or project not registered — continue to next
            }
        }
    }

    /// <summary>
    /// Returns recent cross-project alerts detected in this session.
    /// </summary>
    public List<CrossProjectAlert> GetRecent() => [.. _recentAlerts];
}
