using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Watchdog.Server.Lib;
using Watchdog.Server.Services;

namespace Watchdog.Server.Tools;

[McpServerToolType]
public class InterveneTools(
    ProjectService   projects,
    HookInstaller    hooks,
    NudgeService     nudges,
    SubagentService  subagents)
{
    [McpServerTool(Name = "watchdog_send_nudge")]
    [Description("Send a nudge message to a monitored project. Injected into the project agent's context on its next tool call via the PreToolUse hook. Consumes one session budget credit.")]
    public string SendNudge(
        [Description("Project name")]                                                     string  project,
        [Description("The nudge message content")]                                        string  content,
        [Description("Priority: low | normal | high | critical")]                         string  priority            = "normal",
        [Description("Tone: reminder | redirect | escalation")]                           string  tone                = "reminder",
        [Description("Minutes until the message expires if unread (1–1440)")]             int     expiresInMinutes    = 30,
        [Description("Urgency score from the deliberation loop (0.0–1.0), if available")] double? urgencyScore        = null,
        [Description("Reason or summary from the deliberation loop, if available")]       string? deliberationSummary = null)
    {
        try
        {
            var msg = nudges.Send(project, content, priority, tone, expiresInMinutes,
                                  urgencyScore, deliberationSummary);
            return JsonSerializer.Serialize(new
            {
                sent       = true,
                msg_id     = msg.MsgId,
                project,
                priority,
                tone,
                expires_at = msg.ExpiresAt,
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    [McpServerTool(Name = "watchdog_add_project")]
    [Description("Register a new project for Watchdog to monitor. Creates mailbox directories and a .watchdog identity file in the project root.")]
    public string AddProject(
        [Description("Project name — short, slug-like identifier")] string name,
        [Description("Absolute path to the project directory")]     string path)
    {
        try
        {
            var (project, existed) = projects.Add(name, path);
            return JsonSerializer.Serialize(new
            {
                registered     = true,
                existed_before = existed,
                project,
                next_step      = $"Run watchdog_install_hooks with project: \"{name}\" to activate monitoring.",
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    [McpServerTool(Name = "watchdog_install_hooks")]
    [Description("Install Watchdog's PreToolUse and PostToolUse hooks into a registered project's .claude/settings.json.")]
    public string InstallHooks(
        [Description("Project name")] string project)
    {
        try
        {
            var settingsFile = hooks.Install(project);
            return JsonSerializer.Serialize(new
            {
                installed     = true,
                project,
                settings_file = settingsFile,
                note          = "Hooks are active on the next Claude Code session start in this project.",
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    [McpServerTool(Name = "watchdog_remove_project")]
    [Description("Unregister a project from Watchdog monitoring.")]
    public string RemoveProject(
        [Description("Project name to remove")] string project)
    {
        var removed = projects.Remove(project);
        return JsonSerializer.Serialize(new { removed, project }, JsonOptions.Indented);
    }

    [McpServerTool(Name = "watchdog_spawn_subagent")]
    [Description("Spawn a bounded subagent to perform a task (e.g., run tests, check build, diff analysis) without polluting the project agent's context. Returns a job ID for tracking.")]
    public string SpawnSubagent(
        [Description("Project name")] string project,
        [Description("Task to perform")] string task)
    {
        try
        {
            var job = subagents.Spawn(project, task);
            return JsonSerializer.Serialize(new
            {
                spawned  = true,
                job_id   = job.JobId,
                project,
                status   = job.Status.ToString(),
                result   = job.Result,
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    [McpServerTool(Name = "watchdog_get_job")]
    [Description("Get the status and result of a subagent job.")]
    public string GetJob(
        [Description("Project name")] string project,
        [Description("Job ID returned by watchdog_spawn_subagent")] string jobId)
    {
        var job = subagents.GetJob(project, jobId);
        if (job is null)
            return Error($"Job \"{jobId}\" not found for project \"{project}\".");

        return JsonSerializer.Serialize(job, JsonOptions.Indented);
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = true, message }, JsonOptions.Indented);
}
