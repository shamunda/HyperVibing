using System.Text.Json;
using Microsoft.Data.Sqlite;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

internal static class LegacyDataImporter
{
    public static bool HasLegacyData()
    {
        return File.Exists(Paths.Projects)
               || Directory.Exists(Path.Combine(Paths.Home, "streams"))
               || Directory.Exists(Path.Combine(Paths.Home, "alerts"))
               || Directory.Exists(Path.Combine(Paths.Home, "jobs"))
               || Directory.Exists(Path.Combine(Paths.Home, "episodes"))
               || File.Exists(Paths.PendingReflections)
               || File.Exists(Paths.Profile)
               || File.Exists(Paths.Patterns)
               || Directory.Exists(Path.Combine(Paths.Home, "mailboxes"));
    }

    public static void Import(SqliteConnection connection)
    {
        ImportProjects(connection);
        ImportStreamEvents(connection);
        ImportAlerts(connection);
        ImportJobs(connection);
        ImportEpisodes(connection);
        ImportReflections(connection);
        ImportProfile(connection);
        ImportPatterns(connection);
        ImportMailbox(connection);
    }

    private static void ImportProjects(SqliteConnection connection)
    {
        if (!File.Exists(Paths.Projects)) return;

        var config = TryDeserialize<ProjectsConfig>(File.ReadAllText(Paths.Projects)) ?? new ProjectsConfig([]);
        foreach (var project in config.Projects)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO projects(name, path, added_at, hooks_installed, policy_json)
                VALUES($name, $path, $addedAt, $hooksInstalled, $policyJson);";
            command.Parameters.AddWithValue("$name", project.Name);
            command.Parameters.AddWithValue("$path", project.Path);
            command.Parameters.AddWithValue("$addedAt", project.AddedAt.ToString("O"));
            command.Parameters.AddWithValue("$hooksInstalled", project.HooksInstalled ? 1 : 0);
            command.Parameters.AddWithValue("$policyJson", JsonSerializer.Serialize(project.Policy ?? ProjectWorkflowPolicy.Default, JsonOptions.Default));
            command.ExecuteNonQuery();
        }
    }

    private static void ImportStreamEvents(SqliteConnection connection)
    {
        var streamsRoot = Path.Combine(Paths.Home, "streams");
        if (!Directory.Exists(streamsRoot)) return;

        foreach (var projectDir in Directory.GetDirectories(streamsRoot))
        {
            var streamFile = Path.Combine(projectDir, "stream.jsonl");
            if (!File.Exists(streamFile)) continue;

            foreach (var line in File.ReadLines(streamFile))
            {
                var streamEvent = TryDeserialize<StreamEvent>(line);
                if (streamEvent is null) continue;
                InsertStreamEvent(connection, streamEvent);
            }
        }
    }

    private static void ImportAlerts(SqliteConnection connection)
    {
        var alertsRoot = Path.Combine(Paths.Home, "alerts");
        if (!Directory.Exists(alertsRoot)) return;

        foreach (var projectDir in Directory.GetDirectories(alertsRoot))
        {
            foreach (var file in Directory.GetFiles(projectDir, "*.jsonl"))
            {
                foreach (var line in File.ReadLines(file))
                {
                    var alert = TryDeserialize<SafetyAlert>(line);
                    if (alert is null) continue;
                    InsertAlert(connection, alert);
                }
            }
        }
    }

    private static void ImportJobs(SqliteConnection connection)
    {
        var jobsRoot = Path.Combine(Paths.Home, "jobs");
        if (!Directory.Exists(jobsRoot)) return;

        foreach (var projectDir in Directory.GetDirectories(jobsRoot))
        {
            var jobsFile = Path.Combine(projectDir, "jobs.jsonl");
            if (!File.Exists(jobsFile)) continue;

            foreach (var line in File.ReadLines(jobsFile))
            {
                var job = TryDeserialize<SubagentJob>(line);
                if (job is null) continue;
                InsertJob(connection, job);
            }
        }
    }

    private static void ImportEpisodes(SqliteConnection connection)
    {
        var episodesRoot = Path.Combine(Paths.Home, "episodes");
        if (!Directory.Exists(episodesRoot)) return;

        foreach (var projectDir in Directory.GetDirectories(episodesRoot))
        {
            foreach (var file in Directory.GetFiles(projectDir, "*.jsonl"))
            {
                foreach (var line in File.ReadLines(file))
                {
                    var episode = TryDeserialize<Episode>(line);
                    if (episode is null) continue;
                    InsertEpisode(connection, episode);
                }
            }
        }
    }

    private static void ImportReflections(SqliteConnection connection)
    {
        if (!File.Exists(Paths.PendingReflections)) return;

        var reflections = TryDeserialize<List<PendingReflection>>(File.ReadAllText(Paths.PendingReflections)) ?? [];
        foreach (var reflection in reflections)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO reflections(episode_id, project, msg_id, tone, nudged_at, reflect_after, cursor_at_nudge)
                VALUES($episodeId, $project, $msgId, $tone, $nudgedAt, $reflectAfter, $cursorAtNudge);";
            command.Parameters.AddWithValue("$episodeId", reflection.EpisodeId);
            command.Parameters.AddWithValue("$project", reflection.Project);
            command.Parameters.AddWithValue("$msgId", reflection.MsgId);
            command.Parameters.AddWithValue("$tone", reflection.Tone);
            command.Parameters.AddWithValue("$nudgedAt", reflection.NudgedAt.ToString("O"));
            command.Parameters.AddWithValue("$reflectAfter", reflection.ReflectAfter.ToString("O"));
            command.Parameters.AddWithValue("$cursorAtNudge", reflection.CursorAtNudge);
            command.ExecuteNonQuery();
        }
    }

    private static void ImportProfile(SqliteConnection connection)
    {
        if (!File.Exists(Paths.Profile)) return;

        var profile = TryDeserialize<StrategyProfile>(File.ReadAllText(Paths.Profile));
        if (profile is null) return;

        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO profiles(id, global_scores_json, per_project_json, updated_at)
            VALUES(1, $globalScoresJson, $perProjectJson, $updatedAt);";
        command.Parameters.AddWithValue("$globalScoresJson", JsonSerializer.Serialize(profile.Global, JsonOptions.Default));
        command.Parameters.AddWithValue("$perProjectJson", JsonSerializer.Serialize(profile.PerProject, JsonOptions.Default));
        command.Parameters.AddWithValue("$updatedAt", profile.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
    }

    private static void ImportPatterns(SqliteConnection connection)
    {
        if (!File.Exists(Paths.Patterns)) return;

        var patterns = TryDeserialize<List<CrystallizedPattern>>(File.ReadAllText(Paths.Patterns)) ?? [];
        foreach (var pattern in patterns)
        {
            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO patterns(pattern_id, project, trigger_type, tone, sample_size, success_rate, current_ema, delta, discovered_at, description)
                VALUES($patternId, $project, $triggerType, $tone, $sampleSize, $successRate, $currentEma, $delta, $discoveredAt, $description);";
            command.Parameters.AddWithValue("$patternId", pattern.PatternId);
            command.Parameters.AddWithValue("$project", pattern.Project);
            command.Parameters.AddWithValue("$triggerType", pattern.TriggerType);
            command.Parameters.AddWithValue("$tone", pattern.Tone);
            command.Parameters.AddWithValue("$sampleSize", pattern.SampleSize);
            command.Parameters.AddWithValue("$successRate", pattern.SuccessRate);
            command.Parameters.AddWithValue("$currentEma", pattern.CurrentEma);
            command.Parameters.AddWithValue("$delta", pattern.Delta);
            command.Parameters.AddWithValue("$discoveredAt", pattern.DiscoveredAt.ToString("O"));
            command.Parameters.AddWithValue("$description", pattern.Description);
            command.ExecuteNonQuery();
        }
    }

    private static void ImportMailbox(SqliteConnection connection)
    {
        var mailboxesRoot = Path.Combine(Paths.Home, "mailboxes");
        if (!Directory.Exists(mailboxesRoot)) return;

        foreach (var projectDir in Directory.GetDirectories(mailboxesRoot))
        {
            var project = Path.GetFileName(projectDir);
            ImportMailboxState(connection, project, Path.Combine(projectDir, "inbox"), "inbox");
            ImportMailboxState(connection, project, Path.Combine(projectDir, "outbox"), "outbox");
            ImportMailboxState(connection, project, Path.Combine(projectDir, "dead-letter"), "dead-letter");
        }
    }

    private static void ImportMailboxState(SqliteConnection connection, string project, string directory, string state)
    {
        if (!Directory.Exists(directory)) return;

        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            var message = TryDeserialize<MailboxMessage>(File.ReadAllText(file));
            if (message is null) continue;

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO mailbox_messages(msg_id, project, priority, type, content, tone, requires_ack, created_at, expires_at, source, state, delivered_at)
                VALUES($msgId, $project, $priority, $type, $content, $tone, $requiresAck, $createdAt, $expiresAt, $source, $state, $deliveredAt);";
            command.Parameters.AddWithValue("$msgId", message.MsgId);
            command.Parameters.AddWithValue("$project", project);
            command.Parameters.AddWithValue("$priority", message.Priority.ToString());
            command.Parameters.AddWithValue("$type", message.Type.ToString());
            command.Parameters.AddWithValue("$content", message.Content);
            command.Parameters.AddWithValue("$tone", message.Tone?.ToString());
            command.Parameters.AddWithValue("$requiresAck", message.RequiresAck ? 1 : 0);
            command.Parameters.AddWithValue("$createdAt", message.CreatedAt.ToString("O"));
            command.Parameters.AddWithValue("$expiresAt", message.ExpiresAt.ToString("O"));
            command.Parameters.AddWithValue("$source", message.Source);
            command.Parameters.AddWithValue("$state", state);
            command.Parameters.AddWithValue("$deliveredAt", state == "inbox" ? DBNull.Value : message.CreatedAt.ToString("O"));
            command.ExecuteNonQuery();
        }
    }

    private static void InsertStreamEvent(SqliteConnection connection, StreamEvent streamEvent)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO stream_events(ts, session_id, project, tool_name, tool_input_json, tool_response_json, outcome, working_directory)
            VALUES($ts, $sessionId, $project, $toolName, $toolInputJson, $toolResponseJson, $outcome, $workingDirectory);";
        command.Parameters.AddWithValue("$ts", streamEvent.Ts.ToString("O"));
        command.Parameters.AddWithValue("$sessionId", streamEvent.SessionId);
        command.Parameters.AddWithValue("$project", streamEvent.Project);
        command.Parameters.AddWithValue("$toolName", streamEvent.ToolName);
        command.Parameters.AddWithValue("$toolInputJson", JsonSerializer.Serialize(streamEvent.ToolInput, JsonOptions.Default));
        command.Parameters.AddWithValue("$toolResponseJson", JsonSerializer.Serialize(streamEvent.ToolResponse, JsonOptions.Default));
        command.Parameters.AddWithValue("$outcome", streamEvent.Outcome);
        command.Parameters.AddWithValue("$workingDirectory", ToDbValue(streamEvent.WorkingDirectory));
        command.ExecuteNonQuery();
    }

    private static void InsertAlert(SqliteConnection connection, SafetyAlert alert)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO alerts(alert_id, project, tool_name, rule_matched, severity, detail, detected_at, acknowledged)
            VALUES($alertId, $project, $toolName, $ruleMatched, $severity, $detail, $detectedAt, $acknowledged);";
        command.Parameters.AddWithValue("$alertId", alert.AlertId);
        command.Parameters.AddWithValue("$project", alert.Project);
        command.Parameters.AddWithValue("$toolName", alert.ToolName);
        command.Parameters.AddWithValue("$ruleMatched", alert.RuleMatched);
        command.Parameters.AddWithValue("$severity", alert.Severity.ToString());
        command.Parameters.AddWithValue("$detail", alert.Detail);
        command.Parameters.AddWithValue("$detectedAt", alert.DetectedAt.ToString("O"));
        command.Parameters.AddWithValue("$acknowledged", alert.Acknowledged ? 1 : 0);
        command.ExecuteNonQuery();
    }

    private static void InsertJob(SqliteConnection connection, SubagentJob job)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO jobs(job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path)
            VALUES($jobId, $project, $taskDescription, $taskKind, $status, $createdAt, $startedAt, $completedAt, $command, $argumentsJson, $exitCode, $result, $artifactPath);";
        command.Parameters.AddWithValue("$jobId", job.JobId);
        command.Parameters.AddWithValue("$project", job.Project);
        command.Parameters.AddWithValue("$taskDescription", job.TaskDescription);
        command.Parameters.AddWithValue("$taskKind", job.TaskKind.ToString());
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", job.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$startedAt", ToDbValue(job.StartedAt));
        command.Parameters.AddWithValue("$completedAt", ToDbValue(job.CompletedAt));
        command.Parameters.AddWithValue("$command", ToDbValue(job.Command));
        command.Parameters.AddWithValue("$argumentsJson", JsonSerializer.Serialize(job.Arguments, JsonOptions.Default));
        command.Parameters.AddWithValue("$exitCode", ToDbValue(job.ExitCode));
        command.Parameters.AddWithValue("$result", ToDbValue(job.Result));
        command.Parameters.AddWithValue("$artifactPath", ToDbValue(job.ArtifactPath));
        command.ExecuteNonQuery();
    }

    private static void InsertEpisode(SqliteConnection connection, Episode episode)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR IGNORE INTO episodes(episode_id, project, nudged_at, trigger_json, deliberation_summary, action_json, outcome_json, reflected_at)
            VALUES($episodeId, $project, $nudgedAt, $triggerJson, $deliberationSummary, $actionJson, $outcomeJson, $reflectedAt);";
        command.Parameters.AddWithValue("$episodeId", episode.EpisodeId);
        command.Parameters.AddWithValue("$project", episode.Project);
        command.Parameters.AddWithValue("$nudgedAt", episode.NudgedAt.ToString("O"));
        command.Parameters.AddWithValue("$triggerJson", JsonSerializer.Serialize(episode.Trigger, JsonOptions.Default));
        command.Parameters.AddWithValue("$deliberationSummary", episode.DeliberationSummary);
        command.Parameters.AddWithValue("$actionJson", JsonSerializer.Serialize(episode.Action, JsonOptions.Default));
        command.Parameters.AddWithValue("$outcomeJson", episode.Outcome is null ? DBNull.Value : JsonSerializer.Serialize(episode.Outcome, JsonOptions.Default));
        command.Parameters.AddWithValue("$reflectedAt", ToDbValue(episode.ReflectedAt));
        command.ExecuteNonQuery();
    }

    private static T? TryDeserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions.Default);
        }
        catch
        {
            return default;
        }
    }

    private static object ToDbValue(object? value) => value ?? DBNull.Value;
}