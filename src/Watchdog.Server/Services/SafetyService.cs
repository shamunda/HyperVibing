// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.RegularExpressions;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Evaluates tool-use events against configurable safety rules.
/// Returns a <see cref="SafetyAlert"/> when a match is detected,
/// and can auto-escalate critical violations via <see cref="NudgeService"/>.
/// </summary>
public class SafetyService(NudgeService nudges)
{
    private static readonly Dictionary<string, SafetyRule> Rules = new()
    {
        ["destructive_command"] = new(
            AlertSeverity.Critical,
            ToolNamePatterns: [@"bash|shell|terminal|execute"],
            InputPatterns:    [@"rm\s+-rf\s", @"DROP\s+TABLE", @"DELETE\s+FROM",
                               @"git\s+clean\s+-[a-z]*f", @"rmdir\s+/s",
                               @"Remove-Item.*-Recurse.*-Force"]),

        ["force_push"] = new(
            AlertSeverity.Critical,
            ToolNamePatterns: [@"bash|shell|terminal|execute"],
            InputPatterns:    [@"git\s+push\s+.*--force", @"git\s+push\s+-f\b",
                               @"git\s+reset\s+--hard\s+origin"]),

        ["secret_in_code"] = new(
            AlertSeverity.Warning,
            ToolNamePatterns: [@"write|create|edit|replace"],
            InputPatterns:    [@"(?i)(api[_-]?key|api[_-]?secret|password|token|secret)\s*[:=]\s*[""'][^""']{8,}",
                               @"(?i)AKIA[0-9A-Z]{16}",          // AWS access key pattern
                               @"(?i)sk-[a-zA-Z0-9]{20,}"]),     // OpenAI-style key pattern
    };

    /// <summary>
    /// Evaluates a single stream event against all enabled safety rules.
    /// Returns the first matching alert, or null if no rules are violated.
    /// </summary>
    public SafetyAlert? Evaluate(StreamEvent ev, string[] enabledRules)
    {
        foreach (var ruleName in enabledRules)
        {
            if (!Rules.TryGetValue(ruleName, out var rule)) continue;
            if (!MatchesToolName(ev.ToolName, rule.ToolNamePatterns)) continue;

            var inputText = ev.ToolInput?.ToString() ?? "";
            var matchedPattern = FindMatchingPattern(inputText, rule.InputPatterns);
            if (matchedPattern is null) continue;

            var alert = new SafetyAlert(
                AlertId:    Guid.NewGuid().ToString("N"),
                Project:    ev.Project,
                ToolName:   ev.ToolName,
                RuleMatched: ruleName,
                Severity:   rule.Severity,
                Detail:     $"Pattern matched: {matchedPattern}",
                DetectedAt: DateTimeOffset.UtcNow);

            AlertStore.Append(ev.Project, alert);
            return alert;
        }
        return null;
    }

    /// <summary>
    /// Sends a critical nudge to the project mailbox when a safety violation is detected.
    /// </summary>
    public void AutoEscalate(SafetyAlert alert)
    {
        var message = $"[SAFETY ALERT] Rule '{alert.RuleMatched}' triggered by tool '{alert.ToolName}'. " +
                      $"Severity: {alert.Severity}. {alert.Detail}. Review immediately.";
        try
        {
            nudges.Send(
                project:  alert.Project,
                content:  message,
                priority: "critical",
                tone:     "escalation");
        }
        catch (InvalidOperationException)
        {
            // Budget exhausted — alert is still recorded in AlertStore
        }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static bool MatchesToolName(string toolName, string[] patterns) =>
        patterns.Any(p => Regex.IsMatch(toolName, p, RegexOptions.IgnoreCase));

    private static string? FindMatchingPattern(string input, string[] patterns) =>
        patterns.FirstOrDefault(p => Regex.IsMatch(input, p, RegexOptions.IgnoreCase));

    private record SafetyRule(
        AlertSeverity Severity,
        string[]      ToolNamePatterns,
        string[]      InputPatterns);
}
