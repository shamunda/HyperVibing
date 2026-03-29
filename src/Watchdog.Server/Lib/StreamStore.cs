// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class StreamStore
{
    public static StreamSlice ReadSince(string project, int cursor, int limit = 100) =>
        WatchdogDataStore.Current.ReadStreamSince(project, cursor, limit);

    public static int LineCount(string project) => WatchdogDataStore.Current.GetStreamCount(project);

    public static StreamEvent? LastEvent(string project) => WatchdogDataStore.Current.GetLastStreamEvent(project);

    public static void Append(string project, StreamEvent ev) => WatchdogDataStore.Current.AppendStreamEvent(ev);
}
