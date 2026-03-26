namespace Watchdog.Server.Models;

public record Project(
    string         Name,
    string         Path,
    DateTimeOffset AddedAt,
    bool           HooksInstalled
);

public record ProjectsConfig(List<Project> Projects);
