namespace Watchdog.Server.Models;

public record ProjectStatus(
    string          Name,
    string          Path,
    bool            HooksInstalled,
    DateTimeOffset? LastEventAt,
    double?         SecondsSinceLastEvent,
    int             EventCount,
    int             InboxCount,
    int             OutboxCount,
    int             DeadLetterCount,
    bool            IsStalled
);
