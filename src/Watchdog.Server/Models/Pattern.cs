// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

/// <summary>
/// A crystallized pattern emerges when ≥5 episodes with the same trigger+tone combination
/// show a success rate that differs from the current EMA score by ≥20%. 
/// Patterns surface actionable insights about which strategies work for specific projects.
/// </summary>
public record CrystallizedPattern(
    string         PatternId,
    string         Project,
    string         TriggerType,  // "stall" | "manual"
    string         Tone,         // "reminder" | "redirect" | "escalation"
    int            SampleSize,
    double         SuccessRate,  // empirical: effective / sample
    double         CurrentEma,   // current EMA score for comparison
    double         Delta,        // successRate - currentEma
    DateTimeOffset DiscoveredAt,
    string         Description
);
