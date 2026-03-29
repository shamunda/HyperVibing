using System.Globalization;
using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

internal sealed class SqliteInsightRepository
{
    private readonly SqliteDatabaseRuntime _runtime;

    public SqliteInsightRepository(SqliteDatabaseRuntime runtime)
    {
        _runtime = runtime;
    }

    public StrategyProfile LoadProfile()
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT global_scores_json, per_project_json, updated_at FROM profiles WHERE id = 1 LIMIT 1;";
        using var reader = command.ExecuteReader();
        if (!reader.Read()) return StrategyProfile.Empty();

        var global = SqliteDataMapper.Deserialize<ToneScores>(reader.GetString(0)) ?? ToneScores.Default;
        var perProject = SqliteDataMapper.Deserialize<Dictionary<string, ToneScores>>(reader.GetString(1)) ?? [];
        var updatedAt = DateTimeOffset.Parse(reader.GetString(2), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        return new StrategyProfile(global, perProject, updatedAt);
    }

    public void SaveProfile(StrategyProfile profile)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO profiles(id, global_scores_json, per_project_json, updated_at)
            VALUES(1, $globalScoresJson, $perProjectJson, $updatedAt);";
        command.Parameters.AddWithValue("$globalScoresJson", JsonSerializer.Serialize(profile.Global, JsonOptions.Default));
        command.Parameters.AddWithValue("$perProjectJson", JsonSerializer.Serialize(profile.PerProject, JsonOptions.Default));
        command.Parameters.AddWithValue("$updatedAt", profile.UpdatedAt.ToString("O"));
        command.ExecuteNonQuery();
        _runtime.BackupDatabase();
    }

    public List<CrystallizedPattern> LoadPatterns()
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pattern_id, project, trigger_type, tone, sample_size, success_rate, current_ema, delta, discovered_at, description
            FROM patterns ORDER BY discovered_at DESC;";
        return SqliteDataMapper.ReadPatterns(command);
    }

    public List<CrystallizedPattern> ReadPatternsForProject(string project)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT pattern_id, project, trigger_type, tone, sample_size, success_rate, current_ema, delta, discovered_at, description
            FROM patterns WHERE project = $project ORDER BY discovered_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        return SqliteDataMapper.ReadPatterns(command);
    }

    public void AppendPattern(CrystallizedPattern pattern)
    {
        using var connection = _runtime.OpenConnection();
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
        _runtime.BackupDatabase();
    }

    public bool TryAcquireLease(string name, int pid, TimeSpan ttl)
    {
        using var connection = _runtime.OpenConnection();
        using var transaction = connection.BeginTransaction();
        var now = DateTimeOffset.UtcNow;

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
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM leases WHERE name = $name AND pid = $pid;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$pid", pid);
        command.ExecuteNonQuery();
    }

    public LockInfo? ReadLease(string name)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT pid, started_at FROM leases WHERE name = $name AND expires_at >= $now LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        using var reader = command.ExecuteReader();
        return reader.Read() ? new LockInfo(reader.GetInt32(0), SqliteDataMapper.Parse(reader.GetString(1))) : null;
    }
}