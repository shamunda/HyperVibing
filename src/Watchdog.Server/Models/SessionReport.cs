namespace Watchdog.Server.Models;

public record ProjectReport(
    string  Name,
    int     EpisodesToday,
    int     EffectiveNudges,
    int     IneffectiveNudges,
    int     PendingReflections,
    int     AlertCount,
    double? AverageStallSeconds
);

public record SessionReport(
    DateTimeOffset          GeneratedAt,
    List<ProjectReport>     ProjectReports,
    ToneScores              GlobalToneScores,
    int                     BudgetUsed,
    int                     BudgetRemaining,
    int                     AlertsSurfaced,
    List<CrystallizedPattern> Patterns,
    string?                 MetaAssessment
);
