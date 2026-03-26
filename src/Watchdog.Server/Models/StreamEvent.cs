namespace Watchdog.Server.Models;

public record StreamEvent(
    DateTimeOffset      Ts,
    string              SessionId,
    string              Project,
    string              ToolName,
    object?             ToolInput,
    object?             ToolResponse,
    string              Outcome       // "success" | "error"
);
