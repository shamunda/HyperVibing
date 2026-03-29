// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Watchdog.Server.Lib;
using Watchdog.Server.Services;
using Watchdog.Server.Tools;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<IWatchdogDataStore>(_ => WatchdogDataStore.Current);
builder.Services.AddSingleton<HookCommandService>();

// ── Services — business logic layer ───────────────────────────────────────
// Phase 1
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<HookInstaller>();
builder.Services.AddSingleton<StatusService>();
builder.Services.AddSingleton<EvidenceService>();
builder.Services.AddSingleton<WorkflowService>();
builder.Services.AddSingleton<WorkerSmokeTestService>();

// Phase 2 — deliberative loop
builder.Services.AddSingleton<BudgetService>();
builder.Services.AddSingleton<UrgencyService>();
builder.Services.AddSingleton<ReflectionService>();
builder.Services.AddSingleton<DeliberationService>();
builder.Services.AddSingleton<NudgeService>();

// Phase 3 — safety, corrections, reporting, auto-loop
builder.Services.AddSingleton<SafetyService>();
builder.Services.AddSingleton<CorrectionService>();
builder.Services.AddSingleton<ReportService>();
builder.Services.AddHostedService<WatchLoopService>();

// Phase 4 — multi-project, subagents
builder.Services.AddSingleton<CrossProjectService>();
builder.Services.AddSingleton<SubagentService>();

// Phase 5 — self-improvement
builder.Services.AddSingleton<PatternService>();

// ── MCP server — tools receive services via constructor injection ──────────
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ObserveTools>()
    .WithTools<InterveneTools>()
    .WithTools<LoopTools>();

using var host = builder.Build();

if (args.Length > 0)
{
    var hooks = host.Services.GetRequiredService<HookCommandService>();
    var raw = Console.In.ReadToEnd();
    switch (args[0].ToLowerInvariant())
    {
        case "hook-pre-tool-use":
            var output = hooks.RunPreToolUse(raw);
            if (!string.IsNullOrWhiteSpace(output))
                Console.WriteLine(output);
            return;
        case "hook-post-tool-use":
            hooks.RunPostToolUse(raw);
            return;
    }
}

await host.RunAsync();
