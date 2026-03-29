# Installation And Platforms

This document explains exactly how to install HyperVibing and what differs by platform.

## Supported Platforms

HyperVibing is designed for:

1. Windows
2. macOS
3. Linux

## Required Software

Install all four before using HyperVibing.

| Requirement | Windows | macOS | Linux | Why it is needed |
| --- | --- | --- | --- | --- |
| .NET 9 SDK | Required | Required | Required | Runs the MCP server |
| Claude Code CLI | Required | Required | Required | Hosts the MCP client session |
| Git | Required | Required | Required | Needed for repo work and git hooks |
| PowerShell 7 (`pwsh`) | Strongly recommended and effectively required for project hooks | Required | Required | Runs install script and Claude hook scripts |

## Verify Prerequisites

Run these commands before installation.

### Windows prerequisite check

```powershell
dotnet --version
claude --version
git --version
pwsh --version
```

### macOS prerequisite check

```powershell
dotnet --version
claude --version
git --version
pwsh --version
```

### Linux prerequisite check

```powershell
dotnet --version
claude --version
git --version
pwsh --version
```

If any command fails, install that dependency first.

## Exact Install Steps

### Windows install

```powershell
git clone https://github.com/shamunda/HyperVibing.git
cd HyperVibing
.\install.ps1
```

### macOS install

```powershell
git clone https://github.com/shamunda/HyperVibing.git
cd HyperVibing
pwsh ./install.ps1
```

### Linux install

```powershell
git clone https://github.com/shamunda/HyperVibing.git
cd HyperVibing
pwsh ./install.ps1
```

## What The Installer Writes

Running the installer does these things:

1. Publishes the .NET server to `~/.watchdog/server/`
2. Copies PowerShell hook scripts to `~/.watchdog/hooks/`
3. Creates `~/.watchdog/config/settings.json` if it does not already exist
4. Creates `~/.watchdog/config/projects.json` if it does not already exist
5. Creates `~/.watchdog/backups/`
6. Registers the MCP server with `claude mcp add --transport stdio --scope user watchdog -- dotnet <server-dll>`
7. Installs `pre-commit` and `pre-push` hooks into this repository if `.git/` exists

## Where Files End Up

### Windows paths

1. Runtime home: `C:\Users\<you>\.watchdog`
2. MCP config: `C:\Users\<you>\.claude.json`
3. Published server DLL: `C:\Users\<you>\.watchdog\server\Watchdog.Server.dll`

### macOS paths

1. Runtime home: `/Users/<you>/.watchdog`
2. MCP config: `/Users/<you>/.claude.json`
3. Published server DLL: `/Users/<you>/.watchdog/server/Watchdog.Server.dll`

### Linux paths

1. Runtime home: `/home/<you>/.watchdog`
2. MCP config: `/home/<you>/.claude.json`
3. Published server DLL: `/home/<you>/.watchdog/server/Watchdog.Server.dll`

## First Project Activation

Installing the MCP server is not enough. You must also register a project and install that project's Claude hooks.

### Step 1. Start Claude Code once

Run this anywhere:

```powershell
claude --dangerously-skip-permissions
```

### Step 2. Register the project

Inside Claude Code, run:

```text
/watchdog.add
```

That command:

1. Registers the project in HyperVibing
2. Installs the project's Claude hooks
3. Installs git hooks in the project if `.git/hooks` exists

### Step 3. Close that Claude session

This step is mandatory.

The project hooks only affect new Claude sessions.

### Step 4. Open Claude Code in the project folder

Example:

```powershell
cd C:\path\to\your\project
claude --dangerously-skip-permissions
```

That new session is the monitored project session.

### Step 5. Open a supervisor session

Example:

```powershell
cd C:\path\to\HyperVibing\supervisor
claude --dangerously-skip-permissions
```

Inside the supervisor session, use:

```text
/watchdog.status
```

## Platform Notes You Should Not Miss

### Windows notes

1. The installer script can run in Windows PowerShell.
2. The monitored project hook writer uses `pwsh` in `.claude/settings.json`.
3. Install PowerShell 7 on Windows to avoid hook failures.

### macOS notes

1. Use `pwsh ./install.ps1`.
2. Make sure `pwsh` is on your `PATH` before project hooks are installed.

### Linux notes

1. Use `pwsh ./install.ps1`.
2. Make sure `pwsh` is on your `PATH` before project hooks are installed.

## Verify Installation

After installation, run:

```powershell
claude mcp list
```

You should see a `watchdog` MCP entry.

After project registration, verify these files exist.

### Inside the runtime home

1. `~/.watchdog/server/Watchdog.Server.dll`
2. `~/.watchdog/hooks/pre-tool-use.ps1`
3. `~/.watchdog/hooks/post-tool-use.ps1`
4. `~/.watchdog/config/settings.json`

### Inside the monitored project

1. `<project>/.watchdog`
2. `<project>/.claude/settings.json`

## Common Installation Failures

### `claude` command not found

Cause:

Claude Code CLI is not installed or not on `PATH`.

### `pwsh` command not found

Cause:

PowerShell 7 is not installed or not on `PATH`.

Impact:

Project hooks may fail even if installation partly succeeds.

### `watchdog` does not appear in `claude mcp list`

Cause:

The MCP registration step failed.

Fix:

1. Re-run the installer.
2. Confirm the server DLL exists under `~/.watchdog/server/`.
3. Confirm `claude` is available in the same shell.
