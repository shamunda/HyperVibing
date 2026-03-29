// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class AlertStore
{
    public static void Append(string project, SafetyAlert alert) => WatchdogDataStore.Current.AppendAlert(alert);

    public static List<SafetyAlert> ReadRecent(string project, int maxDays = 7) =>
        WatchdogDataStore.Current.ReadRecentAlerts(project, maxDays);

    public static List<SafetyAlert> ReadUnacknowledged(string project, int maxDays = 7) =>
        WatchdogDataStore.Current.ReadUnacknowledgedAlerts(project, maxDays);

    public static bool Acknowledge(string project, string alertId) =>
        WatchdogDataStore.Current.AcknowledgeAlert(project, alertId);
}
