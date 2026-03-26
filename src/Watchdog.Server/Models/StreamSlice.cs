namespace Watchdog.Server.Models;

public record StreamSlice(List<StreamEvent> Events, int NextCursor);
