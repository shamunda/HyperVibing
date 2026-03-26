using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.Server;
using Watchdog.Server.Services;
using Watchdog.Server.Tools;

var builder = Host.CreateApplicationBuilder(args);

// ── Services — business logic layer ───────────────────────────────────────
// Phase 1
builder.Services.AddSingleton<ProjectService>();
builder.Services.AddSingleton<HookInstaller>();
builder.Services.AddSingleton<StatusService>();

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

await builder.Build().RunAsync();
