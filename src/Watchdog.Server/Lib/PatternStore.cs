// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class PatternStore
{
    public static List<CrystallizedPattern> Load() => WatchdogDataStore.Current.LoadPatterns();

    public static List<CrystallizedPattern> ReadForProject(string project) =>
        WatchdogDataStore.Current.ReadPatternsForProject(project);

    public static void Append(CrystallizedPattern pattern) => WatchdogDataStore.Current.AppendPattern(pattern);
}
