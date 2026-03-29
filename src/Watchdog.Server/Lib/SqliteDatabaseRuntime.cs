using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Watchdog.Server.Lib;

internal sealed class SqliteDatabaseRuntime
{
    private readonly object _bootstrapLock = new();
    private bool _bootstrapped;

    public void EnsureBootstrapped()
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

    public SqliteConnection OpenConnection() => OpenConnection(Paths.Database, useWal: true);

    public void BackupDatabase()
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
}