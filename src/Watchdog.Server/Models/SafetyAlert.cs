// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
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
