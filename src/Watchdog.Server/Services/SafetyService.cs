// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text;
using System.Text.Json;
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
    public SafetyAlert? Evaluate(StreamEvent ev, string[] enabledRules)
    {
        foreach (var ruleName in enabledRules)
        {
            var match = FindViolation(ev, ruleName);
            if (match is null) continue;

            var alert = new SafetyAlert(
                AlertId:    Guid.NewGuid().ToString("N"),
                Project:    ev.Project,
                ToolName:   ev.ToolName,
                RuleMatched: ruleName,
                Severity:   match.Severity,
                Detail:     match.Detail,
                DetectedAt: DateTimeOffset.UtcNow);

            AlertStore.Append(ev.Project, alert);
            return alert;
        }
        return null;
    }

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

    private static SafetyMatch? FindViolation(StreamEvent streamEvent, string ruleName) =>
        ruleName.ToLowerInvariant() switch
        {
            "destructive_command" => StructuredSafetyInspector.DetectDestructiveCommand(streamEvent),
            "force_push" => StructuredSafetyInspector.DetectForcePush(streamEvent),
            "secret_in_code" => StructuredSafetyInspector.DetectSecretWrite(streamEvent),
            _ => null
        };

    private sealed record SafetyMatch(AlertSeverity Severity, string Detail);

    private static class StructuredSafetyInspector
    {
        private static readonly string[] TerminalToolMarkers =
            ["bash", "shell", "terminal", "execute", "pwsh", "powershell", "run_in_terminal"];

        private static readonly string[] FileMutationToolMarkers =
            ["write", "create", "edit", "replace", "apply_patch", "delete"];

        private static readonly HashSet<string> SensitiveNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "apikey", "api_key", "apisecret", "api_secret", "password", "passwd", "token", "secret",
            "clientsecret", "client_secret", "connectionstring", "connection_string", "accesskey", "access_key",
            "awskey", "aws_key"
        };

        public static SafetyMatch? DetectForcePush(StreamEvent streamEvent)
        {
            if (!TryGetCommandTokens(streamEvent, out var commandText, out var tokens)) return null;
            if (tokens.Count < 3) return null;
            if (!IsCommand(tokens, "git", "push")) return null;

            if (tokens.Skip(2).Any(token => token.Equals("-f", StringComparison.OrdinalIgnoreCase)
                                          || token.Equals("--force", StringComparison.OrdinalIgnoreCase)
                                          || token.Equals("--force-with-lease", StringComparison.OrdinalIgnoreCase)))
            {
                return new SafetyMatch(AlertSeverity.Critical,
                    $"Structured command analysis confirmed a force push: {commandText}");
            }

            return null;
        }

        public static SafetyMatch? DetectDestructiveCommand(StreamEvent streamEvent)
        {
            if (TryGetCommandTokens(streamEvent, out var commandText, out var tokens))
            {
                if (IsCommand(tokens, "git", "reset") && tokens.Any(token => token.Equals("--hard", StringComparison.OrdinalIgnoreCase)))
                {
                    return new SafetyMatch(AlertSeverity.Critical,
                        $"Structured command analysis confirmed a hard git reset: {commandText}");
                }

                if (LooksLikeDestructiveSql(commandText))
                {
                    return new SafetyMatch(AlertSeverity.Critical,
                        $"Structured command analysis confirmed a destructive SQL statement: {commandText}");
                }

                if (IsCommand(tokens, "git", "clean") && tokens.Skip(2).Any(token => HasFlag(token, 'f') || token.Equals("--force", StringComparison.OrdinalIgnoreCase)))
                {
                    return new SafetyMatch(AlertSeverity.Critical,
                        $"Structured command analysis confirmed a forced git clean: {commandText}");
                }

                if (IsRmRecursiveForce(tokens) || IsRemoveItemRecursiveForce(tokens) || IsCmdRmdir(tokens))
                {
                    return new SafetyMatch(AlertSeverity.Critical,
                        $"Structured command analysis confirmed a recursive destructive delete: {commandText}");
                }
            }

            if (TryGetStructuredDeleteCount(streamEvent, out var deleteCount) && deleteCount >= 3)
            {
                return new SafetyMatch(AlertSeverity.Critical,
                    $"Structured tool input is deleting {deleteCount} files in one operation.");
            }

            return null;
        }

        public static SafetyMatch? DetectSecretWrite(StreamEvent streamEvent)
        {
            if (!IsFileMutationTool(streamEvent.ToolName)) return null;

            foreach (var candidate in EnumeratePotentialWrittenContent(streamEvent.ToolInput))
            {
                if (TryDetectSecret(candidate, out var detail))
                    return new SafetyMatch(AlertSeverity.Warning, detail);
            }

            return null;
        }

        private static bool TryGetCommandTokens(StreamEvent streamEvent, out string commandText, out List<string> tokens)
        {
            commandText = string.Empty;
            tokens = [];
            if (!IsTerminalTool(streamEvent.ToolName)) return false;
            if (!TryExtractCommandText(streamEvent.ToolInput, out commandText)) return false;

            tokens = Tokenize(commandText);
            return tokens.Count > 0;
        }

        private static bool IsTerminalTool(string toolName) =>
            TerminalToolMarkers.Any(marker => toolName.Contains(marker, StringComparison.OrdinalIgnoreCase));

        private static bool IsFileMutationTool(string toolName) =>
            FileMutationToolMarkers.Any(marker => toolName.Contains(marker, StringComparison.OrdinalIgnoreCase));

        private static bool TryExtractCommandText(object? toolInput, out string commandText)
        {
            commandText = string.Empty;
            return TryExtractString(toolInput, ["command", "input", "text", "script", "query"], out commandText);
        }

        private static bool TryExtractString(object? value, string[] preferredProperties, out string text)
        {
            text = string.Empty;
            switch (value)
            {
                case null:
                    return false;
                case string s when !string.IsNullOrWhiteSpace(s):
                    text = s;
                    return true;
                case JsonElement element:
                    return TryExtractString(element, preferredProperties, out text);
                default:
                    text = value.ToString() ?? string.Empty;
                    return !string.IsNullOrWhiteSpace(text);
            }
        }

        private static bool TryExtractString(JsonElement element, string[] preferredProperties, out string text)
        {
            text = string.Empty;
            if (element.ValueKind == JsonValueKind.String)
            {
                text = element.GetString() ?? string.Empty;
                return !string.IsNullOrWhiteSpace(text);
            }

            if (element.ValueKind != JsonValueKind.Object) return false;

            foreach (var propertyName in preferredProperties)
            {
                if (!element.TryGetProperty(propertyName, out var property)) continue;
                if (property.ValueKind == JsonValueKind.String)
                {
                    text = property.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text)) return true;
                }

                if (property.ValueKind == JsonValueKind.Array)
                {
                    var parts = property.EnumerateArray()
                        .Where(item => item.ValueKind == JsonValueKind.String)
                        .Select(item => item.GetString())
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToArray();
                    if (parts.Length > 0)
                    {
                        text = string.Join(' ', parts!);
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsCommand(IReadOnlyList<string> tokens, string executable, string subcommand) =>
            tokens.Count >= 2
            && tokens[0].Equals(executable, StringComparison.OrdinalIgnoreCase)
            && tokens[1].Equals(subcommand, StringComparison.OrdinalIgnoreCase);

        private static bool HasFlag(string token, char shortFlag) =>
            token.StartsWith('-') && !token.StartsWith("--", StringComparison.Ordinal) && token.Contains(shortFlag, StringComparison.OrdinalIgnoreCase);

        private static bool IsRmRecursiveForce(IReadOnlyList<string> tokens)
        {
            if (tokens.Count < 2) return false;
            if (!tokens[0].Equals("rm", StringComparison.OrdinalIgnoreCase)) return false;

            var hasRecursive = tokens.Skip(1).Any(token => HasFlag(token, 'r') || HasFlag(token, 'R'));
            var hasForce = tokens.Skip(1).Any(token => HasFlag(token, 'f'));
            if (!hasRecursive || !hasForce) return false;

            var targets = tokens.Skip(1).Where(token => !token.StartsWith('-')).ToArray();
            return targets.Any(IsBroadDeleteTarget);
        }

        private static bool IsRemoveItemRecursiveForce(IReadOnlyList<string> tokens)
        {
            if (tokens.Count < 2) return false;
            if (!tokens[0].Equals("Remove-Item", StringComparison.OrdinalIgnoreCase)) return false;

            var hasRecursive = tokens.Any(token => token.Equals("-Recurse", StringComparison.OrdinalIgnoreCase));
            var hasForce = tokens.Any(token => token.Equals("-Force", StringComparison.OrdinalIgnoreCase));
            if (!hasRecursive || !hasForce) return false;

            var targets = tokens.Skip(1)
                .Where(token => !token.StartsWith('-'))
                .ToArray();
            return targets.Any(IsBroadDeleteTarget);
        }

        private static bool IsCmdRmdir(IReadOnlyList<string> tokens)
        {
            if (tokens.Count < 2) return false;
            if (!tokens[0].Equals("rmdir", StringComparison.OrdinalIgnoreCase) && !tokens[0].Equals("rd", StringComparison.OrdinalIgnoreCase))
                return false;

            var hasRecursive = tokens.Any(token => token.Equals("/s", StringComparison.OrdinalIgnoreCase));
            if (!hasRecursive) return false;

            var targets = tokens.Skip(1)
                .Where(token => !token.StartsWith('/'))
                .ToArray();
            return targets.Any(IsBroadDeleteTarget);
        }

        private static bool IsBroadDeleteTarget(string target)
        {
            var normalized = target.Trim().Trim('"', '\'');
            return normalized is "/" or "\\" or "." or ".\\" or "./" or "*" or "*.*"
                || normalized.EndsWith(":\\", StringComparison.OrdinalIgnoreCase)
                || normalized.EndsWith(":/", StringComparison.OrdinalIgnoreCase);
        }

        private static bool LooksLikeDestructiveSql(string commandText)
        {
            var normalized = commandText.Trim().TrimEnd(';');
            if (normalized.StartsWith("DROP TABLE ", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("DROP DATABASE ", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("TRUNCATE TABLE ", StringComparison.OrdinalIgnoreCase)) return true;
            if (normalized.StartsWith("DELETE FROM ", StringComparison.OrdinalIgnoreCase)
                && normalized.IndexOf(" WHERE ", StringComparison.OrdinalIgnoreCase) < 0)
                return true;
            return false;
        }

        private static int CountApplyPatchDeletes(string patch)
        {
            var count = 0;
            foreach (var line in patch.Split('\n'))
            {
                if (line.TrimStart().StartsWith("*** Delete File:", StringComparison.Ordinal))
                    count++;
            }

            return count;
        }

        private static bool TryGetStructuredDeleteCount(StreamEvent streamEvent, out int deleteCount)
        {
            deleteCount = 0;
            if (!IsFileMutationTool(streamEvent.ToolName)) return false;

            if (streamEvent.ToolInput is JsonElement element && element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.String)
                    deleteCount = CountApplyPatchDeletes(input.GetString() ?? string.Empty);

                if (element.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
                    deleteCount += files.GetArrayLength();
            }

            return deleteCount > 0;
        }

        private static IEnumerable<string> EnumeratePotentialWrittenContent(object? toolInput)
        {
            if (toolInput is null) yield break;

            if (toolInput is string text)
            {
                yield return text;
                yield break;
            }

            if (toolInput is not JsonElement element) yield break;

            if (element.ValueKind == JsonValueKind.String)
            {
                yield return element.GetString() ?? string.Empty;
                yield break;
            }

            if (element.ValueKind != JsonValueKind.Object) yield break;

            foreach (var propertyName in new[] { "content", "contents", "text", "newCode", "input" })
            {
                if (!element.TryGetProperty(propertyName, out var property)) continue;
                if (property.ValueKind == JsonValueKind.String)
                {
                    var value = property.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(value))
                        yield return propertyName == "input" ? ExtractAddedPatchContent(value) : value;
                }
                else if (property.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in property.EnumerateArray())
                    {
                        if (item.ValueKind == JsonValueKind.String)
                            yield return item.GetString() ?? string.Empty;
                    }
                }
            }
        }

        private static string ExtractAddedPatchContent(string patch)
        {
            var builder = new StringBuilder();
            foreach (var rawLine in patch.Split('\n'))
            {
                var line = rawLine.TrimEnd('\r');
                if (line.StartsWith("+++", StringComparison.Ordinal) || line.StartsWith("***", StringComparison.Ordinal))
                    continue;
                if (line.StartsWith('+') && !line.StartsWith("++", StringComparison.Ordinal))
                    builder.AppendLine(line[1..]);
            }

            return builder.ToString();
        }

        private static bool TryDetectSecret(string content, out string detail)
        {
            detail = string.Empty;
            foreach (var rawLine in content.Split('\n'))
            {
                var line = rawLine.Trim();
                if (string.IsNullOrWhiteSpace(line)) continue;
                if (!TrySplitAssignment(line, out var key, out var value)) continue;
                if (!SensitiveNames.Contains(NormalizeKey(key))) continue;
                if (!IsHighConfidenceSecretValue(value)) continue;

                detail = $"Structured file-write analysis found a likely secret assigned to '{key}'.";
                return true;
            }

            return false;
        }

        private static bool TrySplitAssignment(string line, out string key, out string value)
        {
            key = string.Empty;
            value = string.Empty;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0)
                separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0) return false;

            key = line[..separatorIndex].Trim().Trim('"', '\'');
            var remainder = line[(separatorIndex + 1)..].Trim();
            if (string.IsNullOrWhiteSpace(remainder)) return false;

            if (remainder[0] is '"' or '\'')
            {
                var quote = remainder[0];
                var endIndex = remainder.IndexOf(quote, 1);
                if (endIndex <= 1) return false;
                value = remainder[1..endIndex];
                return true;
            }

            value = remainder.Split(' ', ',', ';')[0].Trim();
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string NormalizeKey(string key) =>
            new string(key.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        private static bool IsHighConfidenceSecretValue(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length < 16) return false;
            if (value.StartsWith("sk-", StringComparison.OrdinalIgnoreCase) && value.Length >= 20) return true;
            if (value.StartsWith("ghp_", StringComparison.OrdinalIgnoreCase) && value.Length >= 30) return true;
            if (value.StartsWith("github_pat_", StringComparison.OrdinalIgnoreCase) && value.Length >= 30) return true;
            if (value.StartsWith("AKIA", StringComparison.OrdinalIgnoreCase) && value.Length == 20 && value.All(char.IsLetterOrDigit)) return true;

            var hasUpper = value.Any(char.IsUpper);
            var hasLower = value.Any(char.IsLower);
            var hasDigit = value.Any(char.IsDigit);
            return (hasUpper || hasLower) && hasDigit && value.Length >= 24 && value.All(ch => char.IsLetterOrDigit(ch) || ch is '_' or '-' or '/');
        }

        private static List<string> Tokenize(string text)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();
            char? quote = null;

            foreach (var ch in text)
            {
                if (quote is not null)
                {
                    if (ch == quote)
                    {
                        quote = null;
                        continue;
                    }

                    current.Append(ch);
                    continue;
                }

                if (ch is '"' or '\'')
                {
                    quote = ch;
                    continue;
                }

                if (char.IsWhiteSpace(ch))
                {
                    if (current.Length == 0) continue;
                    tokens.Add(current.ToString());
                    current.Clear();
                    continue;
                }

                current.Append(ch);
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }
    }
}
