namespace Watchdog.Server.Models;

public enum MessagePriority { Low, Normal, High, Critical }
public enum MessageType    { Nudge, Query, Directive, Alert }
public enum NudgeTone      { Reminder, Redirect, Escalation }

public record MailboxMessage(
    string          MsgId,
    MessagePriority Priority,
    MessageType     Type,
    string          Content,
    NudgeTone?      Tone,
    bool            RequiresAck,
    DateTimeOffset  CreatedAt,
    DateTimeOffset  ExpiresAt,
    string          Source = "watchdog-supervisor"
);
