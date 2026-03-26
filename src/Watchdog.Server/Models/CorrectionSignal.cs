namespace Watchdog.Server.Models;

public record CorrectionSignal(
    string          Project,
    DateTimeOffset  DetectedAt,
    string          ToolName,
    string          SignalType,     // "explicit_correction" | "undo_action"
    string          Content
);
