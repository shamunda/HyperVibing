namespace Watchdog.Server.Models;

public enum AlertSeverity { Warning, Critical }

public record SafetyAlert(
    string          AlertId,
    string          Project,
    string          ToolName,
    string          RuleMatched,
    AlertSeverity   Severity,
    string          Detail,
    DateTimeOffset  DetectedAt,
    bool            Acknowledged = false
);
