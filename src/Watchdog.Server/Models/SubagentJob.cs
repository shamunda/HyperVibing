namespace Watchdog.Server.Models;

public enum JobStatus { Pending, Running, Completed, Failed }

public record SubagentJob(
    string          JobId,
    string          Project,
    string          TaskDescription,
    JobStatus       Status,
    DateTimeOffset  CreatedAt,
    DateTimeOffset? CompletedAt,
    string?         Result
);
