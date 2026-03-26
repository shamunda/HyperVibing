# Watchdog — Claude Code Supervisor Agent

**Author:** Dennis "Shamunda" Ross  
**Date:** March 2026

An MCP server that monitors and supervises Claude Code sessions. Watchdog runs in a separate "supervisor" Claude Code session and watches your project sessions for stalls, safety issues, and budget overruns — then nudges you to intervene.

## What It Does

- **Stall Detection** — Detects when a Claude Code session goes idle (60s+ default) and alerts the supervisor
- **Safety Scanning** — Regex-based rules flag dangerous tool calls (mass deletes, force pushes, etc.)
- **Budget Tracking** — Monitors token spend per session with configurable limits
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
- Send nudges to stalled sessions
- Review safety alerts

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
| `StallThresholdSeconds` | 60 | Seconds idle before stall alert |
| `MaxBudgetPerSession` | 50000 | Token budget per session |
| `PollIntervalSeconds` | 15 | How often the watch loop checks |

## License

MIT License — Copyright (c) 2026 Lumiotic, Inc. See [LICENSE](LICENSE) for details.
