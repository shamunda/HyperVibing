namespace Watchdog.Server.Models;

public record CrossProjectAlert(
    string          AlertId,
    string          SourceProject,
    List<string>    TargetProjects,
    string          AlertType,      // "cascading_stall" | "shared_dependency" | "safety_cascade"
    string          Message,
    DateTimeOffset  CreatedAt
);
