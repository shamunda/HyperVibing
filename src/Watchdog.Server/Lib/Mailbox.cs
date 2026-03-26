using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class Mailbox
{
    // Priority prefix ensures alphabetic sort == priority order
    private static readonly Dictionary<MessagePriority, string> PriorityPrefix = new()
    {
        [MessagePriority.Critical] = "0",
        [MessagePriority.High]     = "1",
        [MessagePriority.Normal]   = "2",
        [MessagePriority.Low]      = "3",
    };

    public static void Ensure(string project)
    {
        Directory.CreateDirectory(Paths.Mailbox.Inbox(project));
        Directory.CreateDirectory(Paths.Mailbox.Outbox(project));
        Directory.CreateDirectory(Paths.Mailbox.DeadLetter(project));
    }

    public static MailboxMessage Write(string project, MessagePriority priority, MessageType type,
        string content, NudgeTone? tone, bool requiresAck, TimeSpan expiresIn)
    {
        Ensure(project);
        var msg = new MailboxMessage(
            MsgId:       Guid.NewGuid().ToString(),
            Priority:    priority,
            Type:        type,
            Content:     content,
            Tone:        tone,
            RequiresAck: requiresAck,
            CreatedAt:   DateTimeOffset.UtcNow,
            ExpiresAt:   DateTimeOffset.UtcNow + expiresIn
        );
        var filename = $"{PriorityPrefix[priority]}-{msg.MsgId}.json";
        File.WriteAllText(
            Path.Combine(Paths.Mailbox.Inbox(project), filename),
            JsonSerializer.Serialize(msg, JsonOptions.Default));
        return msg;
    }

    public static (MailboxMessage Message, string Filename)? ReadNext(string project)
    {
        var inbox = Paths.Mailbox.Inbox(project);
        if (!Directory.Exists(inbox)) return null;

        foreach (var file in Directory.GetFiles(inbox, "*.json").OrderBy(f => f))
        {
            try
            {
                var msg = JsonSerializer.Deserialize<MailboxMessage>(
                    File.ReadAllText(file), JsonOptions.Default)!;

                if (msg.ExpiresAt < DateTimeOffset.UtcNow)
                {
                    MoveFile(file, Path.Combine(Paths.Mailbox.DeadLetter(project), Path.GetFileName(file)));
                    continue;
                }
                return (msg, Path.GetFileName(file));
            }
            catch { continue; }
        }
        return null;
    }

    public static void Ack(string project, string filename)
        => MoveFile(
            Path.Combine(Paths.Mailbox.Inbox(project), filename),
            Path.Combine(Paths.Mailbox.Outbox(project), filename));

    public static MailboxCounts Counts(string project) => new(
        Inbox:      CountJson(Paths.Mailbox.Inbox(project)),
        Outbox:     CountJson(Paths.Mailbox.Outbox(project)),
        DeadLetter: CountJson(Paths.Mailbox.DeadLetter(project)));

    private static int CountJson(string dir) =>
        Directory.Exists(dir) ? Directory.GetFiles(dir, "*.json").Length : 0;

    private static void MoveFile(string src, string dst)
    {
        if (File.Exists(src)) File.Move(src, dst, overwrite: true);
    }
}
