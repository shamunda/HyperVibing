# Watchdog — Claude Code Supervisor Agent

**Author:** Dennis "Shamunda" Ross  
**Date:** March 2026

An MCP server that monitors and supervises Claude Code sessions. Watchdog runs in a separate "supervisor" Claude Code session and watches your project sessions for stalls, safety issues, and budget overruns — then nudges you to intervene.

## What It Does

- **Stall Detection** — Detects when a Claude Code session goes idle (60s+ default) and alerts the supervisor
- **Safety Scanning** — Regex-based rules flag dangerous tool calls (mass deletes, force pushes, etc.)
- **Budget Tracking** — Monitors token spend per session with configurable limits
- **Workflow + Evidence Gating** — Tracks whether a project is implementing, validating, refining, or review-ready based on real verification evidence
- **Bounded Subagent Jobs** — Runs tests, builds, diff analysis, and Claude worker tasks as tracked jobs with artifacts
- **Secret Detection** — Git pre-commit/pre-push hooks block commits containing API keys, tokens, or passwords
- **Multi-Project** — Monitor multiple Claude Code sessions from one supervisor
- **Adaptive Learning** — Builds strategy profiles per project using EMA-smoothed metrics

## Quick Start

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Claude Code CLI](https://docs.anthropic.com/claude-code)
- PowerShell Core (`pwsh`)

### Install

```powershell
git clone https://github.com/LumiLabs/HyperVibing.git
cd HyperVibing
.\install.ps1
```

This builds the server, copies hooks/config to `~/.watchdog/`, and registers the MCP server in `~/.claude/mcp.json`.

### Usage

1. **Open Claude Code** in any terminal — Watchdog MCP tools are now available
2. **Register a project** — tell Claude: *"Add my project at C:\path\to\myproject called my-project"*
3. **Install hooks** — tell Claude: *"Install watchdog hooks for my-project"*
4. **Open a second Claude Code session** in your project folder — Watchdog monitors it automatically

### Supervisor Mode

Open Claude Code in the `supervisor/` folder. It will use `CLAUDE.md` for its persona and can:
- Check project status
- Read event streams
- Inspect job history and job artifacts
- Send nudges to stalled sessions
- Update project workflow policy
- Review safety alerts

## MCP Tool Highlights

Watchdog now exposes a fuller control plane:

- `watchdog_list_jobs` — show recent bounded worker jobs for a project
- `watchdog_read_job_artifact` — read captured stdout/stderr from a worker job
- `watchdog_get_project_policy` — inspect the review-evidence and worker-backend policy for a project
- `watchdog_set_project_policy` — require tests/builds before review or switch a project to the Claude worker backend
- `watchdog_smoke_test_worker` — launch a real bounded Claude worker smoke test and wait briefly for a result

## Supervisor Slash Commands

The repo now ships first-class supervisor commands under `.claude/commands/`:

- `/watchdog.status`
- `/watchdog.jobs <project>`
- `/watchdog.artifact <project> <job-id>`
- `/watchdog.policy <project>`
- `/watchdog.set-policy <project> ...`
- `/watchdog.smoke-worker <project>`

These wrap the MCP tools so the operator session can use Watchdog as a product instead of prompting against raw tool names.

## Worker Backends

Subagent jobs now support two backend styles:

- **Command backend** — runs bounded local commands like `dotnet test`, `dotnet build`, or `git diff`
- **Claude backend** — launches a real non-interactive Claude Code worker via `claude --print` using project policy for model, effort, tool permissions, extra directories, and budget

Projects can choose their default backend in project policy. A repo can stay command-first, or switch to Claude-backed workers for review and analysis tasks.

## Project Workflow Policy

Each project now carries a persisted workflow policy in the registry. The policy controls:

- whether fresh tests are required before review
- whether a fresh build is required before review
- whether warning alerts block review
- how long verification evidence stays fresh
- whether subagent jobs default to the command backend or Claude backend
- which Claude tools, model, effort, and directories are allowed for worker jobs

## Project Structure

```
config/          Default settings and project registry
hooks/           Pre/post tool-use hooks + git safety hooks
src/             .NET 9 MCP server (C#)
  Watchdog.Server/
    Lib/         Data stores (episodes, streams, mailbox, etc.)
    Models/      DTOs and domain types
    Services/    Business logic (urgency, nudge, safety, budget, etc.)
    Tools/       MCP tool definitions (observe, intervene, loop)
tests/           xUnit test suite
supervisor/      CLAUDE.md persona + LOOP.md auto-loop instructions
install.ps1      One-step installer
```

## Configuration

Settings live in `~/.watchdog/config/settings.json`:

| Setting | Default | Description |
|---------|---------|-------------|
| `stall_threshold_seconds` | 60 | Seconds idle before stall alert |
| `session_budget` | 5 | Nudges available per supervisor session |
| `subagent_timeout_seconds` | 180 | Default timeout for bounded subagent jobs |
| `evidence_freshness_minutes` | 30 | Default freshness window for verification evidence |
| `auto_loop_interval_seconds` | 30 | How often the watch loop checks |

## License

MIT License — Copyright (c) 2026 Lumiotic, Inc. See [LICENSE](LICENSE) for details.
