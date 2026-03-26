// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record CorrectionSignal(
    string          Project,
    DateTimeOffset  DetectedAt,
    string          ToolName,
    string          SignalType,     // "explicit_correction" | "undo_action"
    string          Content
);
