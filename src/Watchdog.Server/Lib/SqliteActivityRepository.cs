using System.Globalization;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

internal sealed class SqliteActivityRepository
{
    private readonly SqliteDatabaseRuntime _runtime;

    public SqliteActivityRepository(SqliteDatabaseRuntime runtime)
    {
        _runtime = runtime;
    }

    public void AppendStreamEvent(StreamEvent streamEvent)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO stream_events(ts, session_id, project, tool_name, tool_input_json, tool_response_json, outcome, working_directory)
            VALUES($ts, $sessionId, $project, $toolName, $toolInputJson, $toolResponseJson, $outcome, $workingDirectory);";
        command.Parameters.AddWithValue("$ts", streamEvent.Ts.ToString("O"));
        command.Parameters.AddWithValue("$sessionId", streamEvent.SessionId);
        command.Parameters.AddWithValue("$project", streamEvent.Project);
        command.Parameters.AddWithValue("$toolName", streamEvent.ToolName);
        command.Parameters.AddWithValue("$toolInputJson", SqliteDataMapper.ToDbValue(SqliteDataMapper.SerializeNode(streamEvent.ToolInput)));
        command.Parameters.AddWithValue("$toolResponseJson", SqliteDataMapper.ToDbValue(SqliteDataMapper.SerializeNode(streamEvent.ToolResponse)));
        command.Parameters.AddWithValue("$outcome", streamEvent.Outcome);
        command.Parameters.AddWithValue("$workingDirectory", SqliteDataMapper.ToDbValue(streamEvent.WorkingDirectory));
        command.ExecuteNonQuery();
    }

    public StreamSlice ReadStreamSince(string project, int cursor, int limit)
    {
        using var connection = _runtime.OpenConnection();
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
            events.Add(SqliteDataMapper.ReadStreamEvent(reader));

        return new StreamSlice(events, cursor + events.Count);
    }

    public int GetStreamCount(string project)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM stream_events WHERE project = $project;";
        command.Parameters.AddWithValue("$project", project);
        return Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    public StreamEvent? GetLastStreamEvent(string project)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT ts, session_id, project, tool_name, tool_input_json, tool_response_json, outcome, working_directory
            FROM stream_events WHERE project = $project ORDER BY id DESC LIMIT 1;";
        command.Parameters.AddWithValue("$project", project);
        using var reader = command.ExecuteReader();
        return reader.Read() ? SqliteDataMapper.ReadStreamEvent(reader) : null;
    }

    public void AppendAlert(SafetyAlert alert)
    {
        using var connection = _runtime.OpenConnection();
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
        _runtime.BackupDatabase();
    }

    public List<SafetyAlert> ReadRecentAlerts(string project, int maxDays = 7)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT alert_id, project, tool_name, rule_matched, severity, detail, detected_at, acknowledged
            FROM alerts
            WHERE project = $project AND detected_at >= $cutoff
            ORDER BY detected_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.AddDays(-Math.Max(1, maxDays)).ToString("O"));
        return SqliteDataMapper.ReadAlerts(command);
    }

    public List<SafetyAlert> ReadUnacknowledgedAlerts(string project, int maxDays = 7)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            SELECT alert_id, project, tool_name, rule_matched, severity, detail, detected_at, acknowledged
            FROM alerts
            WHERE project = $project AND acknowledged = 0 AND detected_at >= $cutoff
            ORDER BY detected_at DESC;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$cutoff", DateTimeOffset.UtcNow.AddDays(-Math.Max(1, maxDays)).ToString("O"));
        return SqliteDataMapper.ReadAlerts(command);
    }

    public bool AcknowledgeAlert(string project, string alertId)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE alerts SET acknowledged = 1 WHERE project = $project AND alert_id = $alertId;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$alertId", alertId);
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) _runtime.BackupDatabase();
        return updated;
    }

    public MailboxMessage EnqueueMailboxMessage(string project, MessagePriority priority, MessageType type,
        string content, NudgeTone? tone, bool requiresAck, TimeSpan expiresIn)
    {
        var message = new MailboxMessage(
            MsgId: Guid.NewGuid().ToString(),
            Priority: priority,
            Type: type,
            Content: content,
            Tone: tone,
            RequiresAck: requiresAck,
            CreatedAt: DateTimeOffset.UtcNow,
            ExpiresAt: DateTimeOffset.UtcNow.Add(expiresIn));

        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT INTO mailbox_messages(msg_id, project, priority, type, content, tone, requires_ack, created_at, expires_at, source, state)
            VALUES($msgId, $project, $priority, $type, $content, $tone, $requiresAck, $createdAt, $expiresAt, $source, 'inbox');";
        command.Parameters.AddWithValue("$msgId", message.MsgId);
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$priority", message.Priority.ToString());
        command.Parameters.AddWithValue("$type", message.Type.ToString());
        command.Parameters.AddWithValue("$content", message.Content);
        command.Parameters.AddWithValue("$tone", SqliteDataMapper.ToDbValue(message.Tone?.ToString()));
        command.Parameters.AddWithValue("$requiresAck", message.RequiresAck ? 1 : 0);
        command.Parameters.AddWithValue("$createdAt", message.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$expiresAt", message.ExpiresAt.ToString("O"));
        command.Parameters.AddWithValue("$source", message.Source);
        command.ExecuteNonQuery();
        _runtime.BackupDatabase();
        return message;
    }

    public MailboxMessage? ClaimNextMailboxMessage(string project)
    {
        using var connection = _runtime.OpenConnection();
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

        string? msgId;
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
        return SqliteDataMapper.GetMailboxMessage(connection, msgId);
    }

    public bool AckMailboxMessage(string project, string msgId)
    {
        using var connection = _runtime.OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = @"
            UPDATE mailbox_messages SET state = 'outbox', delivered_at = COALESCE(delivered_at, $now)
            WHERE project = $project AND msg_id = $msgId;";
        command.Parameters.AddWithValue("$project", project);
        command.Parameters.AddWithValue("$msgId", msgId);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));
        var updated = command.ExecuteNonQuery() > 0;
        if (updated) _runtime.BackupDatabase();
        return updated;
    }

    public MailboxCounts GetMailboxCounts(string project)
    {
        using var connection = _runtime.OpenConnection();
        return new MailboxCounts(
            Inbox: SqliteDataMapper.CountMailboxState(connection, project, "inbox"),
            Outbox: SqliteDataMapper.CountMailboxState(connection, project, "outbox"),
            DeadLetter: SqliteDataMapper.CountMailboxState(connection, project, "dead-letter"));
    }
}