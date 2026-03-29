using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public sealed class SqliteWatchdogDataStore : IWatchdogDataStore
{
    private const string LeaseName = "watch-loop";
    private readonly object _bootstrapLock = new();
    private bool _bootstrapped;

    public ProjectsConfig LoadProjects()
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, path, added_at, hooks_installed, policy_json FROM projects ORDER BY name;";

        var projects = new List<Project>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            projects.Add(ReadProject(reader));

        return new ProjectsConfig(projects);
    }

    public Project? GetProject(string name)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, path, added_at, hooks_installed, policy_json FROM projects WHERE name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProject(reader) : null;
    }

    public (Project Project, bool Existed) AddProject(string name, string path, ProjectWorkflowPolicy policy)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existing = GetProjectInternal(connection, transaction, name);
        if (existing is not null)
        {
            transaction.Commit();
            return (existing, true);
        }

        var project = new Project(name, path, DateTimeOffset.UtcNow, HooksInstalled: false, Policy: policy);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
            INSERT INTO projects(name, path, added_at, hooks_installed, policy_json)
            VALUES($name, $path, $addedAt, 0, $policyJson);";
        command.Parameters.AddWithValue("$name", project.Name);
        command.Parameters.AddWithValue("$path", project.Path);
        command.Parameters.AddWithValue("$addedAt", project.AddedAt.ToString("O"));
        command.Parameters.AddWithValue("$policyJson", JsonSerializer.Serialize(project.EffectivePolicy, JsonOptions.Default));
        command.ExecuteNonQuery();

        transaction.Commit();
        BackupDatabase();
        return (project, false);
    }

    public bool MarkProjectHooksInstalled(string name)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET hooks_installed = 1 WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) BackupDatabase();
        return updated;
    }

    public Project? UpdateProjectPolicy(string name, ProjectWorkflowPolicy policy)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE projects SET policy_json = $policyJson WHERE name = $name;";
        command.Parameters.AddWithValue("$policyJson", JsonSerializer.Serialize(policy, JsonOptions.Default));
        command.Parameters.AddWithValue("$name", name);
        var updated = command.ExecuteNonQuery() > 0;
        transaction.Commit();
        if (updated) BackupDatabase();
        return updated ? GetProject(name) : null;
    }

    public bool RemoveProject(string name)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        foreach (var table in new[]
                 {
                     "projects", "stream_events", "alerts", "mailbox_messages", "jobs",
                     "episodes", "reflections", "patterns"
                 })
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = table == "projects"
                ? "DELETE FROM projects WHERE name = $name;"
                : $"DELETE FROM {table} WHERE project = $name;";
            command.Parameters.AddWithValue("$name", name);
            command.ExecuteNonQuery();
        }

        transaction.Commit();
    BackupDatabase();
        return true;
    }

    public void AppendStreamEvent(StreamEvent streamEvent)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO stream_events(ts, session_id, project, tool_name, tool_input_json, tool_response_json, outcome, working_directory)
            VALUES($ts, $sessionId, $project, $toolName, $toolInputJson, $toolResponseJson, $outcome, $workingDirectory);";
        command.Parameters.AddWithValue("$ts", streamEvent.Ts.ToString("O"));
        command.Parameters.AddWithValue("$sessionId", streamEvent.SessionId);
        command.Parameters.AddWithValue("$project", streamEvent.Project);
        command.Parameters.AddWithValue("$toolName", streamEvent.ToolName);
        command.Parameters.AddWithValue("$toolInputJson", ToDbValue(SerializeNode(streamEvent.ToolInput)));
        command.Parameters.AddWithValue("$toolResponseJson", ToDbValue(SerializeNode(streamEvent.ToolResponse)));
        command.Parameters.AddWithValue("$outcome", streamEvent.Outcome);
        command.Parameters.AddWithValue("$workingDirectory", ToDbValue(streamEvent.WorkingDirectory));
        command.ExecuteNonQuery();
    }

    public StreamSlice ReadStreamSince(string project, int cursor, int limit)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ts, session_id, project, tool_name, tool_input_json, tool_response_json, outcome, working_directory
            FROM stream_events
            WHERE project = $project
            ORDER BY id
            LIMIT $limit OFFSET $offset;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$limit", Math.Clamp(limit, 1, 200));
        command.Parameters.AddWithValue("$offset", Math.Max(0, cursor));

        var events = new List<StreamEvent>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            events.Add(ReadStreamEvent(reader));

        return new StreamSlice(events, cursor + events.Count);
    }

    public int GetStreamCount(string project)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM stream_events WHERE project = $project;";
        command.Parameters.AddWithValue("$project", project);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public StreamEvent? GetLastStreamEvent(string project)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ts, session_id, project, tool_name, tool_input_json, tool_response_json, outcome, working_directory
            FROM stream_events WHERE project = $project ORDER BY id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$project", project);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadStreamEvent(reader) : null;
    }

    public void AppendAlert(SafetyAlert alert)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO alerts(alert_id, project, tool_name, rule_matched, severity, detail, detected_at, acknowledged)
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
        BackupDatabase();
    }

    public List<SafetyAlert> ReadRecentAlerts(string project, int maxDays = 7)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT alert_id, project, tool_name, rule_matched, severity, detail, detected_at, acknowledged
            FROM alerts
            WHERE project = $project AND detected_at >= $cutoff
            ORDER BY detected_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.AddDays(-Math.Max(1, maxDays)).ToString("O"));
        return ReadAlerts(command);
    }

    public List<SafetyAlert> ReadUnacknowledgedAlerts(string project, int maxDays = 7)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT alert_id, project, tool_name, rule_matched, severity, detail, detected_at, acknowledged
            FROM alerts
            WHERE project = $project AND acknowledged = 0 AND detected_at >= $cutoff
            ORDER BY detected_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.AddDays(-Math.Max(1, maxDays)).ToString("O"));
        return ReadAlerts(command);
    }

    public bool AcknowledgeAlert(string project, string alertId)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE alerts SET acknowledged = 1 WHERE project = $project AND alert_id = $alertId;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$alertId", alertId);
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) BackupDatabase();
        return updated;
    }

    public MailboxMessage EnqueueMailboxMessage(string project, MessagePriority priority, MessageType type,
        string content, NudgeTone? tone, bool requiresAck, TimeSpan expiresIn)
    {
        EnsureBootstrapped();
        var message = new MailboxMessage(
            MsgId: Guid.NewGuid().ToString(),
            Priority: priority,
            Type: type,
            Content: content,
            Tone: tone,
            RequiresAck: requiresAck,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.Add(expiresIn));

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO mailbox_messages(msg_id, project, priority, type, content, tone, requires_ack, created_at, expires_at, source, state)
            VALUES($msgId, $project, $priority, $type, $content, $tone, $requiresAck, $createdAt, $expiresAt, $source, 'inbox');";
        command.Parameters.AddWithValue("$msgId", message.MsgId);
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$priority", message.Priority.ToString());
        command.Parameters.AddWithValue("$type", message.Type.ToString());
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$tone", ToDbValue(message.Tone?.ToString()));
        command.Parameters.AddWithValue("$requiresAck", message.RequiresAck ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$expiresAt", message.ExpiresAt.ToString("O"));
        command.Parameters.AddWithValue("$source", message.Source);
        command.ExecuteNonQuery();
        BackupDatabase();
        return message;
    }

    public MailboxMessage? ClaimNextMailboxMessage(string project)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var expire = connection.CreateCommand())
        {
            expire.Transaction = transaction;
            expire.CommandText = @"
                UPDATE mailbox_messages
                SET state = 'dead-letter', delivered_at = $now
                WHERE project = $project AND state = 'inbox' AND expires_at < $now;";
            expire.Parameters.AddWithValue("$project", project);
            expire.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            expire.ExecuteNonQuery();
        }

        string? msgId = null;
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = @"
                SELECT msg_id
                FROM mailbox_messages
                WHERE project = $project AND state = 'inbox'
                ORDER BY CASE priority
                    WHEN 'Critical' THEN 0
                    WHEN 'High' THEN 1
                    WHEN 'Normal' THEN 2
                    ELSE 3 END,
                    created_at
                LIMIT 1;";
            select.Parameters.AddWithValue("$project", project);
            msgId = select.ExecuteScalar() as string;
        }

        if (string.IsNullOrWhiteSpace(msgId))
        {
            transaction.Commit();
            return null;
        }

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = @"
                UPDATE mailbox_messages
                SET state = 'outbox', delivered_at = $now
                WHERE msg_id = $msgId;";
            update.Parameters.AddWithValue("$msgId", msgId);
            update.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
            update.ExecuteNonQuery();
        }

        transaction.Commit();
        return GetMailboxMessage(connection, msgId);
    }

    public bool AckMailboxMessage(string project, string msgId)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE mailbox_messages SET state = 'outbox', delivered_at = COALESCE(delivered_at, $now)
            WHERE project = $project AND msg_id = $msgId;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$msgId", msgId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) BackupDatabase();
        return updated;
    }

    public MailboxCounts GetMailboxCounts(string project)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        return new MailboxCounts(
            Inbox: CountMailboxState(connection, project, "inbox"),
            Outbox: CountMailboxState(connection, project, "outbox"),
            DeadLetter: CountMailboxState(connection, project, "dead-letter"));
    }

    public void EnsureMailbox(string project)
    {
        EnsureBootstrapped();
        _ = GetProject(project);
    }

    public void AppendJob(string project, SubagentJob job)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        InsertOrReplaceJob(connection, job, replace: false);
        BackupDatabase();
    }

    public bool ReplaceJob(string project, SubagentJob updatedJob)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        var updated = InsertOrReplaceJob(connection, updatedJob, replace: true) > 0;
        if (updated) BackupDatabase();
        return updated;
    }

    public SubagentJob? GetJob(string project, string jobId)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path
            FROM jobs WHERE project = $project AND job_id = $jobId LIMIT 1;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$jobId", jobId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadJob(reader) : null;
    }

    public List<SubagentJob> ListRecentJobs(string project, int max = 50)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path
            FROM jobs WHERE project = $project ORDER BY created_at DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$limit", Math.Clamp(max, 1, 200));
        return ReadJobs(command);
    }

    public List<SubagentJob> ListAllJobs(string project)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path
            FROM jobs WHERE project = $project ORDER BY created_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        return ReadJobs(command);
    }

    public void AppendEpisode(string project, Episode episode)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO episodes(episode_id, project, nudged_at, trigger_json, deliberation_summary, action_json, outcome_json, reflected_at)
            VALUES($episodeId, $project, $nudgedAt, $triggerJson, $deliberationSummary, $actionJson, $outcomeJson, $reflectedAt);";
        command.Parameters.AddWithValue("$episodeId", episode.EpisodeId);
        command.Parameters.AddWithValue("$project", episode.Project);
        command.Parameters.AddWithValue("$nudgedAt", episode.NudgedAt.ToString("O"));
        command.Parameters.AddWithValue("$triggerJson", JsonSerializer.Serialize(episode.Trigger, JsonOptions.Default));
        command.Parameters.AddWithValue("$deliberationSummary", episode.DeliberationSummary);
        command.Parameters.AddWithValue("$actionJson", JsonSerializer.Serialize(episode.Action, JsonOptions.Default));
        command.Parameters.AddWithValue("$outcomeJson", ToDbValue(episode.Outcome is null ? null : JsonSerializer.Serialize(episode.Outcome, JsonOptions.Default)));
        command.Parameters.AddWithValue("$reflectedAt", ToDbValue(episode.ReflectedAt?.ToString("O")));
        command.ExecuteNonQuery();
        BackupDatabase();
    }

    public List<Episode> ReadRecentEpisodes(string project, int maxDays = 7)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT episode_id, project, nudged_at, trigger_json, deliberation_summary, action_json, outcome_json, reflected_at
            FROM episodes
            WHERE project = $project AND nudged_at >= $cutoff
            ORDER BY nudged_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.AddDays(-Math.Max(1, maxDays)).ToString("O"));
        return ReadEpisodes(command);
    }

    public bool PatchEpisodeOutcome(string project, string episodeId, EpisodeOutcome outcome, DateTimeOffset reflectedAt)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE episodes SET outcome_json = $outcomeJson, reflected_at = $reflectedAt
            WHERE project = $project AND episode_id = $episodeId;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$episodeId", episodeId);
        command.Parameters.AddWithValue("$outcomeJson", JsonSerializer.Serialize(outcome, JsonOptions.Default));
        command.Parameters.AddWithValue("$reflectedAt", reflectedAt.ToString("O"));
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) BackupDatabase();
        return updated;
    }

    public void EnqueueReflection(PendingReflection reflection)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO reflections(episode_id, project, msg_id, tone, nudged_at, reflect_after, cursor_at_nudge)
            VALUES($episodeId, $project, $msgId, $tone, $nudgedAt, $reflectAfter, $cursorAtNudge);";
        command.Parameters.AddWithValue("$episodeId", reflection.EpisodeId);
        command.Parameters.AddWithValue("$project", reflection.Project);
        command.Parameters.AddWithValue("$msgId", reflection.MsgId);
        command.Parameters.AddWithValue("$tone", reflection.Tone);
        command.Parameters.AddWithValue("$nudgedAt", reflection.NudgedAt.ToString("O"));
        command.Parameters.AddWithValue("$reflectAfter", reflection.ReflectAfter.ToString("O"));
        command.Parameters.AddWithValue("$cursorAtNudge", reflection.CursorAtNudge);
        command.ExecuteNonQuery();
        BackupDatabase();
    }

    public List<PendingReflection> GetDueReflections(DateTimeOffset? asOf = null)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT episode_id, project, msg_id, tone, nudged_at, reflect_after, cursor_at_nudge
            FROM reflections WHERE reflect_after <= $cutoff ORDER BY reflect_after;";
        command.Parameters.AddWithValue("$cutoff", (asOf ?? DateTimeOffset.UtcNow).ToString("O"));
        return ReadReflections(command);
    }

    public void RemoveReflection(string episodeId)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM reflections WHERE episode_id = $episodeId;";
        command.Parameters.AddWithValue("$episodeId", episodeId);
        command.ExecuteNonQuery();
        BackupDatabase();
    }

    public int GetReflectionCount()
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM reflections;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public List<PendingReflection> LoadAllReflections()
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT episode_id, project, msg_id, tone, nudged_at, reflect_after, cursor_at_nudge
            FROM reflections ORDER BY reflect_after;";
        return ReadReflections(command);
    }

    public StrategyProfile LoadProfile()
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT global_scores_json, per_project_json, updated_at FROM profiles WHERE id = 1 LIMIT 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return StrategyProfile.Empty();

        var global = Deserialize<ToneScores>(reader.GetString(0)) ?? ToneScores.Default;
        var perProject = Deserialize<Dictionary<string, ToneScores>>(reader.GetString(1)) ?? [];
        var updatedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new StrategyProfile(global, perProject, updatedAt);
    }

    public void SaveProfile(StrategyProfile profile)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO profiles(id, global_scores_json, per_project_json, updated_at)
            VALUES(1, $globalScoresJson, $perProjectJson, $updatedAt);";
        command.Parameters.AddWithValue("$globalScoresJson", JsonSerializer.Serialize(profile.Global, JsonOptions.Default));
        command.Parameters.AddWithValue("$perProjectJson", JsonSerializer.Serialize(profile.PerProject, JsonOptions.Default));
        command.Parameters.AddWithValue("$updatedAt", profile.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
        BackupDatabase();
    }

    public List<CrystallizedPattern> LoadPatterns()
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pattern_id, project, trigger_type, tone, sample_size, success_rate, current_ema, delta, discovered_at, description
            FROM patterns ORDER BY discovered_at DESC;";
        return ReadPatterns(command);
    }

    public List<CrystallizedPattern> ReadPatternsForProject(string project)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pattern_id, project, trigger_type, tone, sample_size, success_rate, current_ema, delta, discovered_at, description
            FROM patterns WHERE project = $project ORDER BY discovered_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        return ReadPatterns(command);
    }

    public void AppendPattern(CrystallizedPattern pattern)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO patterns(pattern_id, project, trigger_type, tone, sample_size, success_rate, current_ema, delta, discovered_at, description)
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
        BackupDatabase();
    }

    public bool TryAcquireLease(string name, int pid, TimeSpan ttl)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

        LockInfo? existing;
        using (var select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText = "SELECT pid, started_at, expires_at FROM leases WHERE name = $name LIMIT 1;";
            select.Parameters.AddWithValue("$name", name);
            using var reader = select.ExecuteReader();
            existing = reader.Read() ? new LockInfo(reader.GetInt32(0), Parse(reader.GetString(1))) : null;
            if (reader.Read()) { }
        }

        using var upsert = connection.CreateCommand();
        upsert.Transaction = transaction;
        upsert.CommandText = @"
            INSERT INTO leases(name, pid, started_at, expires_at)
            VALUES($name, $pid, $startedAt, $expiresAt)
            ON CONFLICT(name) DO UPDATE SET pid = excluded.pid, started_at = excluded.started_at, expires_at = excluded.expires_at
            WHERE leases.expires_at < $now OR leases.pid = $pid;";
        upsert.Parameters.AddWithValue("$name", name);
        upsert.Parameters.AddWithValue("$pid", pid);
        upsert.Parameters.AddWithValue("$startedAt", now.ToString("O"));
        upsert.Parameters.AddWithValue("$expiresAt", now.Add(ttl).ToString("O"));
        upsert.Parameters.AddWithValue("$now", now.ToString("O"));
        var changed = upsert.ExecuteNonQuery() > 0;

        transaction.Commit();
        return changed;
    }

    public void ReleaseLease(string name, int pid)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM leases WHERE name = $name AND pid = $pid;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$pid", pid);
        command.ExecuteNonQuery();
    }

    public LockInfo? ReadLease(string name)
    {
        EnsureBootstrapped();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pid, started_at FROM leases WHERE name = $name AND expires_at >= $now LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        using var reader = command.ExecuteReader();
        return reader.Read() ? new LockInfo(reader.GetInt32(0), Parse(reader.GetString(1))) : null;
    }

    private void EnsureBootstrapped()
    {
        if (_bootstrapped) return;

        lock (_bootstrapLock)
        {
            if (_bootstrapped) return;

            Directory.CreateDirectory(Paths.Home);
            Directory.CreateDirectory(Paths.Backups);

            if (File.Exists(Paths.Database))
                EnsureHealthyDatabase();

            using var connection = OpenConnection();
            InitializeSchema(connection);

            if (GetSchemaVersion(connection) == 0 && LegacyDataImporter.HasLegacyData())
                LegacyDataImporter.Import(connection);

            SetSchemaVersion(connection, 1);
            BackupDatabase();
            _bootstrapped = true;
        }
    }

    private static SqliteConnection OpenConnection()
        => OpenConnection(Paths.Database, useWal: true);

    private static SqliteConnection OpenConnection(string dataSource, bool useWal)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dataSource,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            Pooling = true
        };

        var connection = new SqliteConnection(builder.ToString());
        connection.Open();

        using var pragma = connection.CreateCommand();
        pragma.CommandText = $@"
            PRAGMA journal_mode = {(useWal ? "WAL" : "DELETE")};
            PRAGMA foreign_keys = ON;
            PRAGMA busy_timeout = 5000;
            PRAGMA synchronous = NORMAL;";
        pragma.ExecuteNonQuery();
        return connection;
    }

    private void EnsureHealthyDatabase()
    {
        try
        {
            using var connection = OpenConnection();
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA integrity_check;";
            var result = Convert.ToString(command.ExecuteScalar(), CultureInfo.InvariantCulture);
            if (string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                return;
        }
        catch
        {
        }

        SqliteConnection.ClearAllPools();

        var corruptPath = Paths.Database + ".corrupt-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        if (File.Exists(Paths.Database))
        {
            try
            {
                File.Move(Paths.Database, corruptPath, overwrite: true);
            }
            catch (IOException)
            {
                AtomicFile.Copy(Paths.Database, corruptPath);
                try { File.Delete(Paths.Database); } catch { }
            }
        }

        if (File.Exists(Paths.DatabaseBackup))
            RestoreFromBackup();
    }

    private static void RestoreFromBackup()
    {
        for (var attempt = 1; attempt <= 20; attempt++)
        {
            try
            {
                SqliteConnection.ClearAllPools();
                if (File.Exists(Paths.Database))
                    File.Delete(Paths.Database);

                File.Copy(Paths.DatabaseBackup, Paths.Database, overwrite: true);
                Paths.SetDatabaseOverride(null);
                return;
            }
            catch (IOException) when (attempt < 20)
            {
                Thread.Sleep(100 * attempt);
            }
            catch (IOException)
            {
                break;
            }
        }

        var recoveredPath = Paths.NewRecoveredDatabasePath();
        File.Copy(Paths.DatabaseBackup, recoveredPath, overwrite: true);
        Paths.SetDatabaseOverride(recoveredPath);
    }

    private static void BackupDatabase()
    {
        if (!File.Exists(Paths.Database)) return;

        SqliteConnection.ClearAllPools();
        Directory.CreateDirectory(Paths.Backups);

        var tempBackup = Paths.DatabaseBackup + ".tmp-" + Guid.NewGuid().ToString("N");
        using (var source = OpenConnection(Paths.Database, useWal: true))
        using (var destination = OpenConnection(tempBackup, useWal: false))
        {
            source.BackupDatabase(destination);
        }

        AtomicFile.Copy(tempBackup, Paths.DatabaseBackup);
        try { File.Delete(tempBackup); } catch { }
    }

    private static void InitializeSchema(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            CREATE TABLE IF NOT EXISTS metadata (
                key TEXT PRIMARY KEY,
                value TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS projects (
                name TEXT PRIMARY KEY,
                path TEXT NOT NULL,
                added_at TEXT NOT NULL,
                hooks_installed INTEGER NOT NULL,
                policy_json TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS stream_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                ts TEXT NOT NULL,
                session_id TEXT NOT NULL,
                project TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                tool_input_json TEXT,
                tool_response_json TEXT,
                outcome TEXT NOT NULL,
                working_directory TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_stream_events_project_id ON stream_events(project, id);

            CREATE TABLE IF NOT EXISTS alerts (
                alert_id TEXT PRIMARY KEY,
                project TEXT NOT NULL,
                tool_name TEXT NOT NULL,
                rule_matched TEXT NOT NULL,
                severity TEXT NOT NULL,
                detail TEXT NOT NULL,
                detected_at TEXT NOT NULL,
                acknowledged INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_alerts_project_detected ON alerts(project, detected_at DESC);

            CREATE TABLE IF NOT EXISTS mailbox_messages (
                msg_id TEXT PRIMARY KEY,
                project TEXT NOT NULL,
                priority TEXT NOT NULL,
                type TEXT NOT NULL,
                content TEXT NOT NULL,
                tone TEXT,
                requires_ack INTEGER NOT NULL,
                created_at TEXT NOT NULL,
                expires_at TEXT NOT NULL,
                source TEXT NOT NULL,
                state TEXT NOT NULL,
                delivered_at TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_mailbox_project_state_created ON mailbox_messages(project, state, created_at);

            CREATE TABLE IF NOT EXISTS jobs (
                job_id TEXT PRIMARY KEY,
                project TEXT NOT NULL,
                task_description TEXT NOT NULL,
                task_kind TEXT NOT NULL,
                status TEXT NOT NULL,
                created_at TEXT NOT NULL,
                started_at TEXT,
                completed_at TEXT,
                command TEXT,
                arguments_json TEXT NOT NULL,
                exit_code INTEGER,
                result TEXT,
                artifact_path TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_jobs_project_created ON jobs(project, created_at DESC);

            CREATE TABLE IF NOT EXISTS episodes (
                episode_id TEXT PRIMARY KEY,
                project TEXT NOT NULL,
                nudged_at TEXT NOT NULL,
                trigger_json TEXT NOT NULL,
                deliberation_summary TEXT NOT NULL,
                action_json TEXT NOT NULL,
                outcome_json TEXT,
                reflected_at TEXT
            );
            CREATE INDEX IF NOT EXISTS idx_episodes_project_nudged ON episodes(project, nudged_at DESC);

            CREATE TABLE IF NOT EXISTS reflections (
                episode_id TEXT PRIMARY KEY,
                project TEXT NOT NULL,
                msg_id TEXT NOT NULL,
                tone TEXT NOT NULL,
                nudged_at TEXT NOT NULL,
                reflect_after TEXT NOT NULL,
                cursor_at_nudge INTEGER NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_reflections_due ON reflections(reflect_after);

            CREATE TABLE IF NOT EXISTS profiles (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                global_scores_json TEXT NOT NULL,
                per_project_json TEXT NOT NULL,
                updated_at TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS patterns (
                pattern_id TEXT PRIMARY KEY,
                project TEXT NOT NULL,
                trigger_type TEXT NOT NULL,
                tone TEXT NOT NULL,
                sample_size INTEGER NOT NULL,
                success_rate REAL NOT NULL,
                current_ema REAL NOT NULL,
                delta REAL NOT NULL,
                discovered_at TEXT NOT NULL,
                description TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS idx_patterns_project_discovered ON patterns(project, discovered_at DESC);

            CREATE TABLE IF NOT EXISTS leases (
                name TEXT PRIMARY KEY,
                pid INTEGER NOT NULL,
                started_at TEXT NOT NULL,
                expires_at TEXT NOT NULL
            );";
        command.ExecuteNonQuery();
    }

    private static int GetSchemaVersion(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT value FROM metadata WHERE key = 'schema_version' LIMIT 1;";
        var value = command.ExecuteScalar() as string;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0;
    }

    private static void SetSchemaVersion(SqliteConnection connection, int version)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO metadata(key, value) VALUES('schema_version', $value)
            ON CONFLICT(key) DO UPDATE SET value = excluded.value;";
        command.Parameters.AddWithValue("$value", version.ToString(CultureInfo.InvariantCulture));
        command.ExecuteNonQuery();
    }

    private static Project ReadProject(SqliteDataReader reader)
    {
        var policy = Deserialize<ProjectWorkflowPolicy>(reader.GetString(4)) ?? ProjectWorkflowPolicy.Default;
        return new Project(
            Name: reader.GetString(0),
            Path: reader.GetString(1),
            AddedAt: Parse(reader.GetString(2)),
            HooksInstalled: reader.GetInt64(3) == 1,
            Policy: policy);
    }

    private static StreamEvent ReadStreamEvent(SqliteDataReader reader) => new(
        Ts: Parse(reader.GetString(0)),
        SessionId: reader.GetString(1),
        Project: reader.GetString(2),
        ToolName: reader.GetString(3),
        ToolInput: DeserializeNode(reader.IsDBNull(4) ? null : reader.GetString(4)),
        ToolResponse: DeserializeNode(reader.IsDBNull(5) ? null : reader.GetString(5)),
        Outcome: reader.GetString(6),
        WorkingDirectory: reader.IsDBNull(7) ? null : reader.GetString(7));

    private static List<SafetyAlert> ReadAlerts(SqliteCommand command)
    {
        var alerts = new List<SafetyAlert>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            alerts.Add(new SafetyAlert(
                AlertId: reader.GetString(0),
                Project: reader.GetString(1),
                ToolName: reader.GetString(2),
                RuleMatched: reader.GetString(3),
                Severity: Enum.Parse<AlertSeverity>(reader.GetString(4), ignoreCase: true),
                Detail: reader.GetString(5),
                DetectedAt: Parse(reader.GetString(6)),
                Acknowledged: reader.GetInt64(7) == 1));
        }

        return alerts;
    }

    private static int CountMailboxState(SqliteConnection connection, string project, string state)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM mailbox_messages WHERE project = $project AND state = $state;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$state", state);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static MailboxMessage? GetMailboxMessage(SqliteConnection connection, string msgId)
    {
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT msg_id, priority, type, content, tone, requires_ack, created_at, expires_at, source
            FROM mailbox_messages WHERE msg_id = $msgId LIMIT 1;";
        command.Parameters.AddWithValue("$msgId", msgId);
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return null;

        return new MailboxMessage(
            MsgId: reader.GetString(0),
            Priority: Enum.Parse<MessagePriority>(reader.GetString(1), ignoreCase: true),
            Type: Enum.Parse<MessageType>(reader.GetString(2), ignoreCase: true),
            Content: reader.GetString(3),
            Tone: reader.IsDBNull(4) ? null : Enum.Parse<NudgeTone>(reader.GetString(4), ignoreCase: true),
            RequiresAck: reader.GetInt64(5) == 1,
            CreatedAt: Parse(reader.GetString(6)),
            ExpiresAt: Parse(reader.GetString(7)),
            Source: reader.GetString(8));
    }

    private static int InsertOrReplaceJob(SqliteConnection connection, SubagentJob job, bool replace)
    {
        using var command = connection.CreateCommand();
        command.CommandText = replace
            ? @"
                INSERT INTO jobs(job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path)
                VALUES($jobId, $project, $taskDescription, $taskKind, $status, $createdAt, $startedAt, $completedAt, $command, $argumentsJson, $exitCode, $result, $artifactPath)
                ON CONFLICT(job_id) DO UPDATE SET
                    project = excluded.project,
                    task_description = excluded.task_description,
                    task_kind = excluded.task_kind,
                    status = excluded.status,
                    created_at = excluded.created_at,
                    started_at = excluded.started_at,
                    completed_at = excluded.completed_at,
                    command = excluded.command,
                    arguments_json = excluded.arguments_json,
                    exit_code = excluded.exit_code,
                    result = excluded.result,
                    artifact_path = excluded.artifact_path;"
            : @"
                INSERT INTO jobs(job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path)
                VALUES($jobId, $project, $taskDescription, $taskKind, $status, $createdAt, $startedAt, $completedAt, $command, $argumentsJson, $exitCode, $result, $artifactPath);";
        command.Parameters.AddWithValue("$jobId", job.JobId);
        command.Parameters.AddWithValue("$project", job.Project);
        command.Parameters.AddWithValue("$taskDescription", job.TaskDescription);
        command.Parameters.AddWithValue("$taskKind", job.TaskKind.ToString());
        command.Parameters.AddWithValue("$status", job.Status.ToString());
        command.Parameters.AddWithValue("$createdAt", job.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$startedAt", ToDbValue(job.StartedAt?.ToString("O")));
        command.Parameters.AddWithValue("$completedAt", ToDbValue(job.CompletedAt?.ToString("O")));
        command.Parameters.AddWithValue("$command", ToDbValue(job.Command));
        command.Parameters.AddWithValue("$argumentsJson", JsonSerializer.Serialize(job.Arguments, JsonOptions.Default));
        command.Parameters.AddWithValue("$exitCode", ToDbValue(job.ExitCode));
        command.Parameters.AddWithValue("$result", ToDbValue(job.Result));
        command.Parameters.AddWithValue("$artifactPath", ToDbValue(job.ArtifactPath));
        return command.ExecuteNonQuery();
    }

    private static List<SubagentJob> ReadJobs(SqliteCommand command)
    {
        var jobs = new List<SubagentJob>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            jobs.Add(ReadJob(reader));
        return jobs;
    }

    private static SubagentJob ReadJob(SqliteDataReader reader) => new(
        JobId: reader.GetString(0),
        Project: reader.GetString(1),
        TaskDescription: reader.GetString(2),
        TaskKind: Enum.Parse<SubagentTaskKind>(reader.GetString(3), ignoreCase: true),
        Status: Enum.Parse<JobStatus>(reader.GetString(4), ignoreCase: true),
        CreatedAt: Parse(reader.GetString(5)),
        StartedAt: reader.IsDBNull(6) ? null : Parse(reader.GetString(6)),
        CompletedAt: reader.IsDBNull(7) ? null : Parse(reader.GetString(7)),
        Command: reader.IsDBNull(8) ? null : reader.GetString(8),
        Arguments: Deserialize<string[]>(reader.GetString(9)) ?? [],
        ExitCode: reader.IsDBNull(10) ? null : reader.GetInt32(10),
        Result: reader.IsDBNull(11) ? null : reader.GetString(11),
        ArtifactPath: reader.IsDBNull(12) ? null : reader.GetString(12));

    private static List<Episode> ReadEpisodes(SqliteCommand command)
    {
        var episodes = new List<Episode>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            episodes.Add(new Episode(
                EpisodeId: reader.GetString(0),
                Project: reader.GetString(1),
                NudgedAt: Parse(reader.GetString(2)),
                Trigger: Deserialize<EpisodeTrigger>(reader.GetString(3))!,
                DeliberationSummary: reader.GetString(4),
                Action: Deserialize<EpisodeAction>(reader.GetString(5))!,
                Outcome: reader.IsDBNull(6) ? null : Deserialize<EpisodeOutcome>(reader.GetString(6)),
                ReflectedAt: reader.IsDBNull(7) ? null : Parse(reader.GetString(7))));
        }

        return episodes;
    }

    private static List<PendingReflection> ReadReflections(SqliteCommand command)
    {
        var reflections = new List<PendingReflection>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            reflections.Add(new PendingReflection(
                EpisodeId: reader.GetString(0),
                Project: reader.GetString(1),
                MsgId: reader.GetString(2),
                Tone: reader.GetString(3),
                NudgedAt: Parse(reader.GetString(4)),
                ReflectAfter: Parse(reader.GetString(5)),
                CursorAtNudge: reader.GetInt32(6)));
        }

        return reflections;
    }

    private static List<CrystallizedPattern> ReadPatterns(SqliteCommand command)
    {
        var patterns = new List<CrystallizedPattern>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            patterns.Add(new CrystallizedPattern(
                PatternId: reader.GetString(0),
                Project: reader.GetString(1),
                TriggerType: reader.GetString(2),
                Tone: reader.GetString(3),
                SampleSize: reader.GetInt32(4),
                SuccessRate: reader.GetDouble(5),
                CurrentEma: reader.GetDouble(6),
                Delta: reader.GetDouble(7),
                DiscoveredAt: Parse(reader.GetString(8)),
                Description: reader.GetString(9)));
        }

        return patterns;
    }

    private static Project? GetProjectInternal(SqliteConnection connection, SqliteTransaction transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name, path, added_at, hooks_installed, policy_json FROM projects WHERE name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        return reader.Read() ? ReadProject(reader) : null;
    }

    private static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, JsonOptions.Default);
    }

    private static object? DeserializeNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string? SerializeNode(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions.Default);

    private static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    private static object ToDbValue(object? value) => value ?? DBNull.Value;
}