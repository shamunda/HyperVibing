// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Server.Tools;

[McpServerToolType]
public class InterveneTools(
    ProjectService   projects,
    HookInstaller    hooks,
    NudgeService     nudges,
    SubagentService  subagents,
    WorkerSmokeTestService smokeTests)
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
            var result = hooks.Install(project);
            return JsonSerializer.Serialize(new
            {
                installed          = true,
                project,
                settings_file      = result.SettingsPath,
                git_hooks_installed = result.GitHooksInstalled,
                note               = "All hooks installed. Close this Claude session and reopen it in the project directory to activate monitoring.",
            }, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    [McpServerTool(Name = "watchdog_set_project_policy")]
    [Description("Update a project's review-evidence and worker-backend policy. Use this to require tests/builds before review or switch the default subagent backend to Claude.")]
    public string SetProjectPolicy(
        [Description("Project name")] string project,
        [Description("Require a fresh successful test run before review.")] bool requireFreshTests = true,
        [Description("Require a fresh successful build before review.")] bool requireFreshBuild = false,
        [Description("Require zero unacknowledged critical alerts before review.")] bool requireNoCriticalAlerts = true,
        [Description("Require zero unacknowledged warning alerts before review.")] bool requireNoWarnings = false,
        [Description("Freshness window override in minutes. Use 0 to keep the global default.")] int evidenceFreshnessMinutes = 0,
        [Description("Default worker backend: Command | Claude")] string defaultWorkerBackend = "Command",
        [Description("Claude model for worker jobs, e.g. sonnet or opus.")] string claudeModel = "sonnet",
        [Description("Claude effort for worker jobs: low | medium | high | max.")] string claudeEffort = "medium",
        [Description("Claude permission mode for worker jobs, e.g. plan or dontAsk.")] string claudePermissionMode = "plan",
        [Description("Optional Claude agent name to use for worker jobs.")] string? claudeAgent = null,
        [Description("Allowed Claude tools for worker jobs. Omit to use the default read-oriented set.")] string[]? claudeAllowedTools = null,
        [Description("Additional directories to grant the Claude worker access to.")] string[]? claudeAddDirs = null,
        [Description("Optional appended system prompt for Claude worker jobs.")] string? claudeAppendSystemPrompt = null,
        [Description("Maximum USD budget for a single Claude worker job. Use 0 to omit.")] double maxBudgetUsd = 0)
    {
        try
        {
            if (!Enum.TryParse<WorkerBackendKind>(defaultWorkerBackend, ignoreCase: true, out var backendKind))
                return Error($"Unknown worker backend '{defaultWorkerBackend}'. Use Command or Claude.");

            var policy = new ProjectWorkflowPolicy
            {
                ReviewEvidence = new ReviewEvidencePolicy
                {
                    RequireFreshTests = requireFreshTests,
                    RequireFreshBuild = requireFreshBuild,
                    RequireNoCriticalAlerts = requireNoCriticalAlerts,
                    RequireNoWarnings = requireNoWarnings,
                    EvidenceFreshnessMinutes = evidenceFreshnessMinutes > 0 ? evidenceFreshnessMinutes : null
                },
                WorkerBackend = new WorkerBackendPolicy
                {
                    DefaultBackend = backendKind,
                    ClaudeModel = claudeModel,
                    ClaudeEffort = claudeEffort,
                    ClaudeAgent = string.IsNullOrWhiteSpace(claudeAgent) ? null : claudeAgent,
                    PermissionMode = claudePermissionMode,
                    AllowedTools = claudeAllowedTools is { Length: > 0 } ? claudeAllowedTools : ProjectWorkflowPolicy.Default.WorkerBackend.AllowedTools,
                    AdditionalDirectories = claudeAddDirs ?? [],
                    AppendSystemPrompt = string.IsNullOrWhiteSpace(claudeAppendSystemPrompt) ? null : claudeAppendSystemPrompt,
                    MaxBudgetUsd = maxBudgetUsd > 0 ? (decimal?)maxBudgetUsd : null
                }
            };

            var updated = projects.UpdatePolicy(project, policy);
            return JsonSerializer.Serialize(new
            {
                updated = true,
                project,
                policy = updated.EffectivePolicy,
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

    [McpServerTool(Name = "watchdog_smoke_test_worker")]
    [Description("Launch a real bounded Claude worker smoke test for a project and optionally wait briefly for completion. This validates the live Claude worker backend instead of only previewing a plan.")]
    public string SmokeTestWorker(
        [Description("Project name")] string project,
        [Description("Seconds to wait for completion before returning. Use 0 for fire-and-check-later mode.")] int waitForCompletionSeconds = 10)
    {
        try
        {
            var result = smokeTests.Start(project, waitForCompletionSeconds);
            return JsonSerializer.Serialize(result, JsonOptions.Indented);
        }
        catch (Exception ex) { return Error(ex.Message); }
    }

    private static string Error(string message) =>
        JsonSerializer.Serialize(new { error = true, message }, JsonOptions.Indented);
}
