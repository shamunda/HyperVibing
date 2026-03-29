// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class Mailbox
{
    public static void Ensure(string project) => WatchdogDataStore.Current.EnsureMailbox(project);

    public static MailboxMessage Write(string project, MessagePriority priority, MessageType type,
        string content, NudgeTone? tone, bool requiresAck, TimeSpan expiresIn)
        => WatchdogDataStore.Current.EnqueueMailboxMessage(project, priority, type, content, tone, requiresAck, expiresIn);

    public static (MailboxMessage Message, string Filename)? ReadNext(string project)
    {
        var message = WatchdogDataStore.Current.ClaimNextMailboxMessage(project);
        return message is null ? null : (message, message.MsgId);
    }

    public static void Ack(string project, string filename)
        => WatchdogDataStore.Current.AckMailboxMessage(project, filename);

    public static MailboxCounts Counts(string project) => WatchdogDataStore.Current.GetMailboxCounts(project);
}
