namespace Watchdog.Server.Models;

public record MailboxCounts(int Inbox, int Outbox, int DeadLetter);
