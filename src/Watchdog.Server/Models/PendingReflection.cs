// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

/// <summary>
/// Persisted record of a nudge awaiting outcome evaluation.
/// CursorAtNudge is the stream line count when the nudge was sent —
/// if the count has advanced by ReflectAfter, the agent resumed.
/// Tone is stored here to avoid re-reading the episode file during reflection.
/// </summary>
public record PendingReflection(
    string          EpisodeId,
    string          Project,
    string          MsgId,
    string          Tone,               // stored to avoid re-reading Episode on reflection
    DateTimeOffset  NudgedAt,
    DateTimeOffset  ReflectAfter,
    int             CursorAtNudge
);
