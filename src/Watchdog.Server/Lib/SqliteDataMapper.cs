using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

internal static class SqliteDataMapper
{
    public static Project ReadProject(SqliteDataReader reader)
    {
        var policy = Deserialize<ProjectWorkflowPolicy>(reader.GetString(4)) ?? ProjectWorkflowPolicy.Default;
        return new Project(
            Name: reader.GetString(0),
            Path: reader.GetString(1),
            AddedAt: Parse(reader.GetString(2)),
            HooksInstalled: reader.GetInt64(3) == 1,
            Policy: policy);
    }

    public static StreamEvent ReadStreamEvent(SqliteDataReader reader) => new(
        Ts: Parse(reader.GetString(0)),
        SessionId: reader.GetString(1),
        Project: reader.GetString(2),
        ToolName: reader.GetString(3),
        ToolInput: DeserializeNode(reader.IsDBNull(4) ? null : reader.GetString(4)),
        ToolResponse: DeserializeNode(reader.IsDBNull(5) ? null : reader.GetString(5)),
        Outcome: reader.GetString(6),
        WorkingDirectory: reader.IsDBNull(7) ? null : reader.GetString(7));

    public static List<SafetyAlert> ReadAlerts(DbCommand command)
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

    public static int CountMailboxState(SqliteConnection connection, string project, string state)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM mailbox_messages WHERE project = $project AND state = $state;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$state", state);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public static MailboxMessage? GetMailboxMessage(SqliteConnection connection, string msgId)
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

    public static int InsertOrReplaceJob(SqliteConnection connection, SubagentJob job, bool replace)
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

    public static List<SubagentJob> ReadJobs(DbCommand command)
    {
        var jobs = new List<SubagentJob>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            jobs.Add(ReadJob((SqliteDataReader)reader));
        return jobs;
    }

    public static SubagentJob ReadJob(SqliteDataReader reader) => new(
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

    public static List<Episode> ReadEpisodes(DbCommand command)
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

    public static List<PendingReflection> ReadReflections(DbCommand command)
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

    public static List<CrystallizedPattern> ReadPatterns(DbCommand command)
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

    public static T? Deserialize<T>(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        return JsonSerializer.Deserialize<T>(json, JsonOptions.Default);
    }

    public static object? DeserializeNode(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    public static string? SerializeNode(object? value) => value is null ? null : JsonSerializer.Serialize(value, JsonOptions.Default);

    public static DateTimeOffset Parse(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static object ToDbValue(object? value) => value ?? DBNull.Value;
}