// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Models;

public enum WorkerBackendKind
{
    Command,
    Claude
}

public record ReviewEvidencePolicy
{
    public bool RequireFreshTests      { get; init; } = true;
    public bool RequireFreshBuild      { get; init; }
    public bool RequireNoCriticalAlerts { get; init; } = true;
    public bool RequireNoWarnings      { get; init; }
    public int? EvidenceFreshnessMinutes { get; init; }
}

public record WorkerBackendPolicy
{
    public WorkerBackendKind DefaultBackend { get; init; } = WorkerBackendKind.Command;
    public string ClaudeExecutable          { get; init; } = "claude";
    public string? ClaudeModel             { get; init; } = "sonnet";
    public string? ClaudeEffort            { get; init; } = "medium";
    public string? ClaudeAgent             { get; init; }
    public string PermissionMode           { get; init; } = "plan";
    public bool UseBareMode                { get; init; } = false;
    public bool DisableSessionPersistence  { get; init; } = true;
    public decimal? MaxBudgetUsd           { get; init; }
    public string[] AllowedTools           { get; init; } = ["Read", "Grep", "LS", "Glob"];
    public string[] AdditionalDirectories  { get; init; } = [];
    public string? AppendSystemPrompt      { get; init; }
}

public record ProjectWorkflowPolicy
{
    public ReviewEvidencePolicy ReviewEvidence { get; init; } = new();
    public WorkerBackendPolicy WorkerBackend   { get; init; } = new();

    public static ProjectWorkflowPolicy Default => new();
}