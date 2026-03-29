using System.Text.Json;
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

public class HookCommandService(SafetyService safety)
{
    public string RunPreToolUse(string rawEvent)
    {
        var context = HookContext.Parse(rawEvent);
        if (context is null) return string.Empty;

        var message = Mailbox.ReadNext(context.ProjectName);
        if (message is null) return string.Empty;
        var tonePrefix = message.Value.Message.Tone is null
            ? string.Empty
            : $"[{message.Value.Message.Tone.Value.ToString().ToUpperInvariant()}] ";
        return $"[WATCHDOG] {tonePrefix}{message.Value.Message.Content}";
    }

    public void RunPostToolUse(string rawEvent)
    {
        var context = HookContext.Parse(rawEvent);
        if (context is null) return;

        var streamEvent = context.ToStreamEvent();
        StreamStore.Append(context.ProjectName, streamEvent);

        var settings = SettingsLoader.Get();
        var alert = safety.Evaluate(streamEvent, settings.SafetyRules);
        if (alert is { Severity: AlertSeverity.Critical })
            safety.AutoEscalate(alert);
    }

    private sealed record HookContext(
        string ProjectName,
        string? WorkingDirectory,
        string SessionId,
        string ToolName,
        object? ToolInput,
        object? ToolResponse,
        string Outcome)
    {
        public StreamEvent ToStreamEvent() => new(
            Ts: DateTimeOffset.UtcNow,
            SessionId: SessionId,
            Project: ProjectName,
            ToolName: ToolName,
            ToolInput: ToolInput,
            ToolResponse: ToolResponse,
            Outcome: Outcome,
            WorkingDirectory: WorkingDirectory);

        public static HookContext? Parse(string rawEvent)
        {
            if (string.IsNullOrWhiteSpace(rawEvent)) return null;

            using var document = JsonDocument.Parse(rawEvent);
            var root = document.RootElement;
            var cwd = root.TryGetProperty("cwd", out var cwdElement)
                ? cwdElement.GetString()
                : Directory.GetCurrentDirectory();
            if (string.IsNullOrWhiteSpace(cwd)) return null;

            var identityFile = Path.Combine(cwd, ".watchdog");
            if (!File.Exists(identityFile)) return null;

            var projectName = File.ReadAllText(identityFile).Trim();
            if (string.IsNullOrWhiteSpace(projectName)) return null;

            var toolInput = root.TryGetProperty("tool_input", out var toolInputElement)
                ? toolInputElement.Clone()
                : default(JsonElement?);
            var toolResponse = root.TryGetProperty("tool_response", out var toolResponseElement)
                ? toolResponseElement.Clone()
                : default(JsonElement?);
            var outcome = root.TryGetProperty("tool_response", out var responseElement) &&
                          responseElement.ValueKind == JsonValueKind.Object &&
                          responseElement.TryGetProperty("error", out _)
                ? "error"
                : "success";

            return new HookContext(
                ProjectName: projectName,
                WorkingDirectory: cwd,
                SessionId: root.TryGetProperty("session_id", out var sessionElement) ? sessionElement.GetString() ?? "unknown" : "unknown",
                ToolName: root.TryGetProperty("tool_name", out var toolElement) ? toolElement.GetString() ?? "unknown" : "unknown",
                ToolInput: toolInput,
                ToolResponse: toolResponse,
                Outcome: outcome);
        }
    }
}