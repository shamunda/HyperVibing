using System.Text.Json;
using Microsoft.Data.Sqlite;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

internal sealed class SqliteProjectRepository
{
    private readonly SqliteDatabaseRuntime _runtime;

    public SqliteProjectRepository(SqliteDatabaseRuntime runtime)
    {
        _runtime = runtime;
    }

    public ProjectsConfig LoadProjects()
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name, path, added_at, hooks_installed, policy_json FROM projects ORDER BY name;";

        var projects = new List<Project>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
            projects.Add(SqliteDataMapper.ReadProject(reader));

        return new ProjectsConfig(projects);
    }

    public Project? GetProject(string name)
    {
        using var connection = _runtime.OpenConnection();
        return GetProject(connection, transaction: null, name);
    }

    public (Project Project, bool Existed) AddProject(string name, string path, ProjectWorkflowPolicy policy)
    {
        using var connection = _runtime.OpenConnection();
        using var transaction = connection.BeginTransaction();

        var existing = GetProject(connection, transaction, name);
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
        _runtime.BackupDatabase();
        return (project, false);
    }

    public bool MarkProjectHooksInstalled(string name)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE projects SET hooks_installed = 1 WHERE name = $name;";
        command.Parameters.AddWithValue("$name", name);
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) _runtime.BackupDatabase();
        return updated;
    }

    public Project? UpdateProjectPolicy(string name, ProjectWorkflowPolicy policy)
    {
        using var connection = _runtime.OpenConnection();
        using var transaction = connection.BeginTransaction();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "UPDATE projects SET policy_json = $policyJson WHERE name = $name;";
        command.Parameters.AddWithValue("$policyJson", JsonSerializer.Serialize(policy, JsonOptions.Default));
        command.Parameters.AddWithValue("$name", name);
        var updated = command.ExecuteNonQuery() > 0;
        transaction.Commit();
        if (updated) _runtime.BackupDatabase();
        return updated ? GetProject(name) : null;
    }

    public bool RemoveProject(string name)
    {
        using var connection = _runtime.OpenConnection();
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
        _runtime.BackupDatabase();
        return true;
    }

    private static Project? GetProject(SqliteConnection connection, SqliteTransaction? transaction, string name)
    {
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "SELECT name, path, added_at, hooks_installed, policy_json FROM projects WHERE name = $name LIMIT 1;";
        command.Parameters.AddWithValue("$name", name);
        using var reader = command.ExecuteReader();
        return reader.Read() ? SqliteDataMapper.ReadProject(reader) : null;
    }
}