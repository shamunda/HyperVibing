# HyperVibing Docs

This directory documents the HyperVibing MCP server as it exists in this repository.

Use these docs based on what you are doing:

1. If you are installing HyperVibing for the first time, read [installation-and-platforms.md](installation-and-platforms.md).
2. If you are using HyperVibing from Claude Code slash commands, read [claude-commands.md](claude-commands.md).
3. If you are using HyperVibing as raw MCP tools from a client, read [mcp-tools.md](mcp-tools.md).
4. If you need to understand storage, hooks, workers, and runtime behavior, read [runtime-architecture.md](runtime-architecture.md).

## Naming

The product name is `HyperVibing`.

The current tool and command prefix is still `watchdog`.

That means:

1. The repository name is `HyperVibing`.
2. Raw MCP tools are named like `watchdog_get_status`.
3. Claude Code slash commands are named like `/watchdog.status`.
4. Runtime files are written under `~/.watchdog/`.

## Supported Use Modes

HyperVibing has two operator-facing modes.

### 1. Claude Code command mode

Use this when you are working inside Claude Code and want friendly commands.

Examples:

1. `/watchdog.add`
2. `/watchdog.status`
3. `/watchdog.stream demo-app`

### 2. Raw MCP tool mode

Use this when your client exposes MCP tools directly instead of Claude slash commands.

Examples:

1. `watchdog_add_project`
2. `watchdog_get_status`
3. `watchdog_read_stream`

## Platform Summary

| Platform | Install command | Runtime root | Claude MCP config |
| --- | --- | --- | --- |
| Windows | `./install.ps1` from PowerShell | `C:\Users\<you>\.watchdog` | `C:\Users\<you>\.claude.json` |
| macOS | `pwsh ./install.ps1` | `/Users/<you>/.watchdog` | `/Users/<you>/.claude.json` |
| Linux | `pwsh ./install.ps1` | `/home/<you>/.watchdog` | `/home/<you>/.claude.json` |

Important:

1. PowerShell 7 (`pwsh`) is the safe assumption for monitored-project hooks on every platform.
2. Windows users should still install `pwsh`, because the project hook writer emits `pwsh` commands into Claude hook settings.
3. `install.ps1` registers the MCP server in user scope, not repo scope.

## Read Order

If you want the shortest correct path, read in this order:

1. [installation-and-platforms.md](installation-and-platforms.md)
2. [claude-commands.md](claude-commands.md)
3. [runtime-architecture.md](runtime-architecture.md)
