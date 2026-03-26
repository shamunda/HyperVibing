namespace Watchdog.Server.Models;

public record EpisodeTrigger(
    string  Type,            // "stall_detected" | "manual"
    double  UrgencyScore,
    double  StallSeconds,
    int     BudgetRemaining
);

public record EpisodeAction(
    string  Tone,            // "reminder" | "redirect" | "escalation"
    string  MsgId,
    string  Content
);

public record EpisodeOutcome(
    bool  ActivityResumed,
    bool  Effective,
    int   CursorDelta        // stream lines advanced since nudge was sent
);

public record Episode(
    string          EpisodeId,
    string          Project,
    DateTimeOffset  NudgedAt,
    EpisodeTrigger  Trigger,
    string          DeliberationSummary,
    EpisodeAction   Action,
    EpisodeOutcome? Outcome,             // null until reflection window expires
    DateTimeOffset? ReflectedAt
);
