using System.Globalization;
using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

internal sealed class SqliteWorkflowRepository
{
    private readonly SqliteDatabaseRuntime _runtime;

    public SqliteWorkflowRepository(SqliteDatabaseRuntime runtime)
    {
        _runtime = runtime;
    }

    public void AppendJob(string project, SubagentJob job)
    {
        using var connection = _runtime.OpenConnection();
        SqliteDataMapper.InsertOrReplaceJob(connection, job, replace: false);
        _runtime.BackupDatabase();
    }

    public bool ReplaceJob(string project, SubagentJob updatedJob)
    {
        using var connection = _runtime.OpenConnection();
        var updated = SqliteDataMapper.InsertOrReplaceJob(connection, updatedJob, replace: true) > 0;
        if (updated) _runtime.BackupDatabase();
        return updated;
    }

    public SubagentJob? GetJob(string project, string jobId)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path
            FROM jobs WHERE project = $project AND job_id = $jobId LIMIT 1;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$jobId", jobId);
        using var reader = command.ExecuteReader();
        return reader.Read() ? SqliteDataMapper.ReadJob(reader) : null;
    }

    public List<SubagentJob> ListRecentJobs(string project, int max = 50)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path
            FROM jobs WHERE project = $project ORDER BY created_at DESC LIMIT $limit;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$limit", Math.Clamp(max, 1, 200));
        return SqliteDataMapper.ReadJobs(command);
    }

    public List<SubagentJob> ListAllJobs(string project)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT job_id, project, task_description, task_kind, status, created_at, started_at, completed_at, command, arguments_json, exit_code, result, artifact_path
            FROM jobs WHERE project = $project ORDER BY created_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        return SqliteDataMapper.ReadJobs(command);
    }

    public void AppendEpisode(string project, Episode episode)
    {
        using var connection = _runtime.OpenConnection();
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
        command.Parameters.AddWithValue("$outcomeJson", SqliteDataMapper.ToDbValue(episode.Outcome is null ? null : JsonSerializer.Serialize(episode.Outcome, JsonOptions.Default)));
        command.Parameters.AddWithValue("$reflectedAt", SqliteDataMapper.ToDbValue(episode.ReflectedAt?.ToString("O")));
        command.ExecuteNonQuery();
        _runtime.BackupDatabase();
    }

    public List<Episode> ReadRecentEpisodes(string project, int maxDays = 7)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT episode_id, project, nudged_at, trigger_json, deliberation_summary, action_json, outcome_json, reflected_at
            FROM episodes
            WHERE project = $project AND nudged_at >= $cutoff
            ORDER BY nudged_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.AddDays(-Math.Max(1, maxDays)).ToString("O"));
        return SqliteDataMapper.ReadEpisodes(command);
    }

    public bool PatchEpisodeOutcome(string project, string episodeId, EpisodeOutcome outcome, DateTimeOffset reflectedAt)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE episodes SET outcome_json = $outcomeJson, reflected_at = $reflectedAt
            WHERE project = $project AND episode_id = $episodeId;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$episodeId", episodeId);
        command.Parameters.AddWithValue("$outcomeJson", JsonSerializer.Serialize(outcome, JsonOptions.Default));
        command.Parameters.AddWithValue("$reflectedAt", reflectedAt.ToString("O"));
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) _runtime.BackupDatabase();
        return updated;
    }

    public void EnqueueReflection(PendingReflection reflection)
    {
        using var connection = _runtime.OpenConnection();
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
        _runtime.BackupDatabase();
    }

    public List<PendingReflection> GetDueReflections(DateTimeOffset? asOf = null)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT episode_id, project, msg_id, tone, nudged_at, reflect_after, cursor_at_nudge
            FROM reflections WHERE reflect_after <= $cutoff ORDER BY reflect_after;";
        command.Parameters.AddWithValue("$cutoff", (asOf ?? DateTimeOffset.UtcNow).ToString("O"));
        return SqliteDataMapper.ReadReflections(command);
    }

    public void RemoveReflection(string episodeId)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM reflections WHERE episode_id = $episodeId;";
        command.Parameters.AddWithValue("$episodeId", episodeId);
        command.ExecuteNonQuery();
        _runtime.BackupDatabase();
    }

    public int GetReflectionCount()
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM reflections;";
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public List<PendingReflection> LoadAllReflections()
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT episode_id, project, msg_id, tone, nudged_at, reflect_after, cursor_at_nudge
            FROM reflections ORDER BY reflect_after;";
        return SqliteDataMapper.ReadReflections(command);
    }
}