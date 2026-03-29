// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

public class SubagentService
{
    private static readonly ConcurrentDictionary<string, Task> ActiveJobs = new();

    public SubagentJob Spawn(string project, string taskDescription)
    {
        var registeredProject = ProjectRegistry.Get(project);
        if (registeredProject is null)
            throw new InvalidOperationException($"Project \"{project}\" is not registered.");

        var plan = CreatePlan(registeredProject, taskDescription);

        var job = new SubagentJob(
            JobId:           Guid.NewGuid().ToString("N"),
            Project:         project,
            TaskDescription: taskDescription,
            TaskKind:        plan.TaskKind,
            Status:          JobStatus.Pending,
            CreatedAt:       DateTimeOffset.UtcNow,
            StartedAt:       null,
            CompletedAt:     null,
            Command:         plan.Command,
            Arguments:       plan.Arguments,
            ExitCode:        null,
            Result:          "Queued for execution.",
            ArtifactPath:    Paths.JobArtifact(project, Guid.Empty.ToString("N")));

        var artifactPath = Paths.JobArtifact(project, job.JobId);
        job = job with { ArtifactPath = artifactPath };

        JobStore.Append(project, job);
        var execution = Task.Run(() => ExecuteJob(registeredProject.Path, job, plan));
        ActiveJobs[job.JobId] = execution;
        _ = execution.ContinueWith(_ =>
        {
            ActiveJobs.TryRemove(job.JobId, out var _);
        });
        return job;
    }

    public SubagentJob? GetJob(string project, string jobId) =>
        JobStore.Get(project, jobId);

    public List<SubagentJob> ListJobs(string project, int max = 20) =>
        JobStore.ListRecent(project, Math.Clamp(max, 1, 200));

    public string ReadArtifact(string project, string jobId, int maxLines = 200)
    {
        var job = GetJob(project, jobId)
            ?? throw new InvalidOperationException($"Job \"{jobId}\" is not tracked for project \"{project}\".");

        if (string.IsNullOrWhiteSpace(job.ArtifactPath) || !File.Exists(job.ArtifactPath))
            throw new FileNotFoundException($"Artifact not found for job \"{jobId}\".");

        return string.Join(Environment.NewLine,
            File.ReadLines(job.ArtifactPath).Take(Math.Clamp(maxLines, 1, 1000)));
    }

    internal SubagentExecutionPreview PreviewPlan(string project, string taskDescription)
    {
        var registeredProject = ProjectRegistry.Get(project)
            ?? throw new InvalidOperationException($"Project \"{project}\" is not registered.");

        var plan = CreatePlan(registeredProject, taskDescription);
        return new SubagentExecutionPreview(plan.TaskKind, plan.Command, plan.Arguments, plan.TimeoutMilliseconds);
    }

    private void ExecuteJob(string workingDirectory, SubagentJob job, ExecutionPlan plan)
    {
        Directory.CreateDirectory(Paths.JobArtifacts(job.Project));

        var started = job with
        {
            Status    = JobStatus.Running,
            StartedAt = DateTimeOffset.UtcNow,
            Result    = $"Running {plan.Command} {string.Join(' ', plan.Arguments)}"
        };

        JobStore.Replace(job.Project, started);

        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName               = plan.Command,
                    WorkingDirectory       = workingDirectory,
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    RedirectStandardInput  = plan.StdinPayload is not null,
                    UseShellExecute        = false,
                    CreateNoWindow         = true,
                }
            };

            foreach (var arg in plan.Arguments)
                process.StartInfo.ArgumentList.Add(arg);

            var output = new StringBuilder();
            process.OutputDataReceived += (_, args) => AppendLine(output, args.Data);
            process.ErrorDataReceived += (_, args) => AppendLine(output, args.Data);

            process.Start();

            if (plan.StdinPayload is not null)
            {
                process.StandardInput.WriteLine(plan.StdinPayload);
                process.StandardInput.Close();
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            if (!process.WaitForExit(plan.TimeoutMilliseconds))
            {
                TryKill(process);
                var timeoutText = $"Timed out after {plan.TimeoutMilliseconds / 1000}s.";
                PersistResult(started, JobStatus.Failed, exitCode: null, output, timeoutText);
                return;
            }

            process.WaitForExit();
            var summary = process.ExitCode == 0
                ? $"Command completed successfully (exit {process.ExitCode})."
                : $"Command failed (exit {process.ExitCode}).";

            PersistResult(started,
                process.ExitCode == 0 ? JobStatus.Completed : JobStatus.Failed,
                process.ExitCode,
                output,
                summary);
        }
        catch (Exception ex)
        {
            PersistResult(started, JobStatus.Failed, exitCode: null, new StringBuilder(), ex.Message);
        }
    }

    private static void PersistResult(SubagentJob startedJob, JobStatus status, int? exitCode, StringBuilder output, string summary)
    {
        var artifactPath = startedJob.ArtifactPath ?? Paths.JobArtifact(startedJob.Project, startedJob.JobId);
        File.WriteAllText(artifactPath, output.ToString());

        var outputText = output.ToString();
        var result = string.IsNullOrWhiteSpace(outputText)
            ? summary
            : $"{summary} Artifact: {artifactPath}";

        JobStore.Replace(startedJob.Project, startedJob with
        {
            Status       = status,
            CompletedAt  = DateTimeOffset.UtcNow,
            ExitCode     = exitCode,
            Result       = result,
            ArtifactPath = artifactPath
        });
    }

    private static void AppendLine(StringBuilder output, string? data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        output.AppendLine(data);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
        }
        catch { /* best effort */ }
    }

    private static ExecutionPlan CreatePlan(Project project, string taskDescription)
    {
        var workingDirectory = project.Path;
        var policy = project.EffectivePolicy;

        if (TryParseCommandSpec(taskDescription, out var spec))
            return BuildPlanFromSpec(project, spec!, taskDescription);

        var normalized = taskDescription.Trim().ToLowerInvariant();
        return normalized switch
        {
            "run tests" or "test" or "tests" => BuildDotNetPlan(workingDirectory, SubagentTaskKind.RunTests, "test"),
            "check build" or "build" => BuildDotNetPlan(workingDirectory, SubagentTaskKind.Build, "build"),
            "diff analysis" or "git diff" or "diff" => new ExecutionPlan(
                TaskKind:            SubagentTaskKind.DiffAnalysis,
                Command:             "git",
                Arguments:           ["diff", "--stat", "--no-ext-diff"],
                TimeoutMilliseconds: DefaultTimeoutMilliseconds()),
            _ when policy.WorkerBackend.DefaultBackend == WorkerBackendKind.Claude =>
                BuildClaudePlan(project, taskDescription, null),
            _ => throw new InvalidOperationException(
                "Unsupported subagent task. Use 'run tests', 'check build', 'diff analysis', or a JSON command spec.")
        };
    }

    private static ExecutionPlan BuildPlanFromSpec(Project project, SubagentTaskSpec spec, string originalTask)
    {
        return spec.Kind switch
        {
            SubagentTaskKind.ClaudeWorker => BuildClaudePlan(project, spec.Prompt ?? originalTask, spec),
            _ => BuildCommandPlan(spec)
        };
    }

    private static ExecutionPlan BuildDotNetPlan(string workingDirectory, SubagentTaskKind taskKind, string verb)
    {
        var solution = Directory.GetFiles(workingDirectory, "*.sln", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (solution is not null)
        {
            return new ExecutionPlan(
                TaskKind:            taskKind,
                Command:             "dotnet",
                Arguments:           [verb, Path.GetFileName(solution), "--nologo"],
                TimeoutMilliseconds: DefaultTimeoutMilliseconds());
        }

        var project = Directory.GetFiles(workingDirectory, "*.csproj", SearchOption.AllDirectories).FirstOrDefault();
        if (project is not null)
        {
            return new ExecutionPlan(
                TaskKind:            taskKind,
                Command:             "dotnet",
                Arguments:           [verb, project, "--nologo"],
                TimeoutMilliseconds: DefaultTimeoutMilliseconds());
        }

        throw new InvalidOperationException($"Could not find a .sln or .csproj under {workingDirectory}.");
    }

    private static ExecutionPlan BuildCommandPlan(SubagentTaskSpec spec)
    {
        if (string.IsNullOrWhiteSpace(spec.Command))
            throw new InvalidOperationException("Command task spec must include a command.");

        var settings = SettingsLoader.Get();
        if (!settings.AllowedSubagentCommands.Any(cmd => string.Equals(cmd, spec.Command, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Command '{spec.Command}' is not allowed. Allowed commands: {string.Join(", ", settings.AllowedSubagentCommands)}.");
        }

        return new ExecutionPlan(
            TaskKind:            spec.Kind,
            Command:             spec.Command,
            Arguments:           spec.Args ?? [],
            TimeoutMilliseconds: ResolveTimeoutMilliseconds(spec.TimeoutSeconds));
    }

    private static ExecutionPlan BuildClaudePlan(Project project, string prompt, SubagentTaskSpec? spec)
    {
        if (string.IsNullOrWhiteSpace(prompt))
            throw new InvalidOperationException("Claude worker tasks require a prompt.");

        var backend = project.EffectivePolicy.WorkerBackend;
        var command = string.IsNullOrWhiteSpace(spec?.Command) ? backend.ClaudeExecutable : spec!.Command!;
        var arguments = new List<string>
        {
            "--print"
        };

        if (backend.UseBareMode)
            arguments.Add("--bare");

        if (backend.DisableSessionPersistence)
            arguments.Add("--no-session-persistence");

        if (!string.IsNullOrWhiteSpace(backend.PermissionMode))
        {
            arguments.Add("--permission-mode");
            arguments.Add(backend.PermissionMode);
        }

        if (!string.IsNullOrWhiteSpace(spec?.Model ?? backend.ClaudeModel))
        {
            arguments.Add("--model");
            arguments.Add(spec?.Model ?? backend.ClaudeModel!);
        }

        if (!string.IsNullOrWhiteSpace(spec?.Effort ?? backend.ClaudeEffort))
        {
            arguments.Add("--effort");
            arguments.Add(spec?.Effort ?? backend.ClaudeEffort!);
        }

        if (!string.IsNullOrWhiteSpace(spec?.Agent ?? backend.ClaudeAgent))
        {
            arguments.Add("--agent");
            arguments.Add(spec?.Agent ?? backend.ClaudeAgent!);
        }

        var maxBudgetUsd = spec?.MaxBudgetUsd ?? backend.MaxBudgetUsd;
        if (maxBudgetUsd is not null)
        {
            arguments.Add("--max-budget-usd");
            arguments.Add(maxBudgetUsd.Value.ToString(CultureInfo.InvariantCulture));
        }

        var allowedTools = spec?.AllowedTools ?? backend.AllowedTools;
        if (allowedTools.Length > 0)
        {
            arguments.Add("--allowedTools");
            arguments.AddRange(allowedTools);
        }

        var appendSystemPrompt = spec?.AppendSystemPrompt ?? backend.AppendSystemPrompt;
        if (!string.IsNullOrWhiteSpace(appendSystemPrompt))
        {
            arguments.Add("--append-system-prompt");
            arguments.Add(appendSystemPrompt);
        }

        var addDirs = new[] { project.Path }
            .Concat(spec?.AddDirs ?? backend.AdditionalDirectories)
            .Where(dir => !string.IsNullOrWhiteSpace(dir))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (addDirs.Length > 0)
        {
            arguments.Add("--add-dir");
            arguments.AddRange(addDirs);
        }

        // Deliver prompt via stdin — positional arg delivery fails when spawned without a terminal.
        return new ExecutionPlan(
            TaskKind:            SubagentTaskKind.ClaudeWorker,
            StdinPayload:        prompt,
            Command:             command,
            Arguments:           arguments.ToArray(),
            TimeoutMilliseconds: ResolveTimeoutMilliseconds(spec?.TimeoutSeconds));
    }

    private static bool TryParseCommandSpec(string taskDescription, out SubagentTaskSpec? spec)
    {
        spec = null;
        if (!taskDescription.TrimStart().StartsWith("{")) return false;

        spec = JsonSerializer.Deserialize<SubagentTaskSpec>(taskDescription, JsonOptions.Default);
        return spec is not null;
    }

    private static int DefaultTimeoutMilliseconds() => ResolveTimeoutMilliseconds(timeoutSeconds: null);

    private static int ResolveTimeoutMilliseconds(int? timeoutSeconds)
    {
        var settings = SettingsLoader.Get();
        var seconds  = timeoutSeconds ?? settings.SubagentTimeoutSeconds;
        return Math.Max(5, seconds) * 1000;
    }

    internal sealed record SubagentExecutionPreview(
        SubagentTaskKind TaskKind,
        string           Command,
        string[]         Arguments,
        int              TimeoutMilliseconds);

    private sealed record ExecutionPlan(
        SubagentTaskKind TaskKind,
        string           Command,
        string[]         Arguments,
        int              TimeoutMilliseconds,
        string?          StdinPayload = null);

    private sealed record SubagentTaskSpec(
        SubagentTaskKind Kind,
        string?          Command,
        string[]?        Args,
        int?             TimeoutSeconds,
        string?          Prompt,
        string?          Model,
        string?          Effort,
        string?          Agent,
        string[]?        AllowedTools,
        string[]?        AddDirs,
        decimal?         MaxBudgetUsd,
        string?          AppendSystemPrompt);
}
