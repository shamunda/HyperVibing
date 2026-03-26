using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Watchdog.Server.Lib;
using Watchdog.Server.Services;

namespace Watchdog.Server.Tools;

[McpServerToolType]
public class ObserveTools(
    StatusService        statusService,
    ReportService        reportService,
    CrossProjectService  crossProjectService,
    PatternService       patternService)
{
    [McpServerTool(Name = "watchdog_read_stream")]
    [Description("Read tool-use events from a monitored project's event stream since a given cursor position.")]
    public string ReadStream(
        [Description("Project name")] string project,
        [Description("Line offset to start reading from (0 = beginning)")] int cursor = 0,
        [Description("Maximum events to return (1–200)")] int limit = 50)
    {
        var slice = statusService.ReadStream(project, cursor, limit);
        return JsonSerializer.Serialize(new
        {
            events       = slice.Events,
            next_cursor  = slice.NextCursor,
            returned     = slice.Events.Count,
            total_events = statusService.StreamLineCount(project),
        }, JsonOptions.Indented);
    }

    [McpServerTool(Name = "watchdog_get_status")]
    [Description("Get a health snapshot of one or all monitored projects. Shows stall status, last event time, and mailbox counts.")]
    public string GetStatus(
        [Description("Project name. Omit to get status for all projects.")] string? project = null)
    {
        // Load once — reused for both statuses and stalled summary to avoid double file I/O.
        var all      = statusService.GetAll();
        var statuses = project is not null
            ? all.Where(s => s.Name == project).Cast<object>().ToList()
            : all.Cast<object>().ToList();

        var stalledNames = all.Where(s => s.IsStalled).Select(s => s.Name).ToList();

        return JsonSerializer.Serialize(new
        {
            projects = statuses,
            summary  = new
            {
                total            = statuses.Count,
                stalled          = stalledNames.Count,
                stalled_projects = stalledNames,
            },
        }, JsonOptions.Indented);
    }

    [McpServerTool(Name = "watchdog_list_projects")]
    [Description("List all projects registered with Watchdog.")]
    public string ListProjects()
    {
        var config = statusService.GetAll();
        return JsonSerializer.Serialize(config, JsonOptions.Indented);
    }

    [McpServerTool(Name = "watchdog_get_alerts")]
    [Description("Get safety alerts for a project. Returns alerts triggered by safety rule violations (destructive commands, secrets in code, force pushes).")]
    public string GetAlerts(
        [Description("Project name")] string project,
        [Description("Only show unacknowledged alerts")] bool unacknowledgedOnly = false)
    {
        try
        {
            var alerts = unacknowledgedOnly
                ? AlertStore.ReadUnacknowledged(project)
                : AlertStore.ReadRecent(project);

            return JsonSerializer.Serialize(new
            {
                project,
                alert_count = alerts.Count,
                alerts,
            }, JsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = true, message = ex.Message }, JsonOptions.Indented);
        }
    }

    [McpServerTool(Name = "watchdog_self_report")]
    [Description("Generate a session summary report: nudges sent, outcomes, tone effectiveness, stall patterns, safety alerts.")]
    public string SelfReport()
    {
        try
        {
            var report = reportService.GenerateReport();
            return JsonSerializer.Serialize(report, JsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = true, message = ex.Message }, JsonOptions.Indented);
        }
    }

    [McpServerTool(Name = "watchdog_get_cross_alerts")]
    [Description("Get cross-project alerts — cascading stalls and shared-dependency issues detected across multiple projects.")]
    public string GetCrossAlerts()
    {
        try
        {
            var alerts = crossProjectService.GetRecent();
            return JsonSerializer.Serialize(new
            {
                alert_count = alerts.Count,
                alerts,
            }, JsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = true, message = ex.Message }, JsonOptions.Indented);
        }
    }

    [McpServerTool(Name = "watchdog_get_patterns")]
    [Description("Get crystallized patterns — statistically significant strategy insights discovered from episode history. Shows which tones work better or worse than expected for each project.")]
    public string GetPatterns(
        [Description("Project name. Omit to get patterns for all projects.")] string? project = null)
    {
        try
        {
            var patterns = project is not null
                ? patternService.GetPatterns(project)
                : patternService.GetAllPatterns();

            return JsonSerializer.Serialize(new
            {
                pattern_count = patterns.Count,
                patterns,
            }, JsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new { error = true, message = ex.Message }, JsonOptions.Indented);
        }
    }
}
