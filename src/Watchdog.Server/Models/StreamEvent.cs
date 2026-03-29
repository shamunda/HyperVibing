// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public record StreamEvent(
    DateTimeOffset      Ts,
    string              SessionId,
    string              Project,
    string              ToolName,
    object?             ToolInput,
    object?             ToolResponse,
    string              Outcome,
    string?             WorkingDirectory = null
);
