# Runtime Architecture

This document explains how the HyperVibing MCP server is wired and where its data lives.

## High-Level Model

HyperVibing has three runtime roles:

1. The MCP server process itself
2. The monitored Claude Code project session
3. The supervisor Claude Code session

The MCP server is the control plane.

The monitored project session produces tool-use events.

The supervisor session reads status and sends interventions.

## Server Startup Model

The server uses .NET hosting and stdio MCP transport.

At startup it registers:

1. Data store access
2. Hook command handling
3. Business services such as status, safety, nudging, reporting, deliberation, patterns, cross-project logic, and subagent jobs
4. The three MCP tool classes: observe, intervene, and loop

## Normal MCP Mode

In normal MCP mode, Claude Code talks to the server over stdio.

The server exposes 21 MCP tools.

Those tools are grouped into:

1. Observe tools
2. Intervene tools
3. Loop tools

## Hook CLI Mode

The published server also has a hook CLI mode.

It supports these process arguments:

1. `hook-pre-tool-use`
2. `hook-post-tool-use`

Those modes are invoked by the PowerShell scripts in `~/.watchdog/hooks/`.

## What The Project Hooks Do

When you install hooks for a monitored project, HyperVibing writes Claude hook configuration to `<project>/.claude/settings.json`.

That configuration runs two PowerShell scripts:

1. `pre-tool-use.ps1`
2. `post-tool-use.ps1`

### PreToolUse flow

1. Claude is about to run a tool
2. The pre-hook passes raw event JSON to the server
3. The server reads `<cwd>/.watchdog` to identify the project
4. The server reads the next queued mailbox message for that project
5. If a message exists, it returns `[WATCHDOG] ...` text for Claude to inject into context

### PostToolUse flow

1. Claude finishes a tool call
2. The post-hook passes raw event JSON to the server
3. The server resolves the project from `<cwd>/.watchdog`
4. The server appends the structured event to the project stream store
5. The server runs safety evaluation on that event
6. If a critical safety alert is found, HyperVibing auto-escalates by sending a nudge

## Project Registration Flow

Project registration is a two-step backend flow.

### Raw MCP flow

1. `watchdog_add_project`
2. `watchdog_install_hooks`

### Claude slash-command flow

1. `/watchdog.add`
2. That command internally performs both backend calls

Project registration writes:

1. A project record in the control-plane data store
2. A `.watchdog` identity file in the project root
3. `<project>/.claude/settings.json`
4. Git hooks in `<project>/.git/hooks/` when the project is a git repo

## Storage Model

HyperVibing uses a hybrid storage model.

### SQLite-backed control plane

The primary runtime database is:

1. `~/.watchdog/watchdog.db`

The latest backup path is:

1. `~/.watchdog/backups/watchdog-latest.db`

The control plane stores project, stream, alert, job, policy, and related runtime state through `WatchdogDataStore`.

### On-disk artifacts and runtime directories

Large job artifacts remain on disk under:

1. `~/.watchdog/jobs/<project>/artifacts/`

HyperVibing also maintains these runtime locations:

1. `~/.watchdog/server/`
2. `~/.watchdog/hooks/`
3. `~/.watchdog/config/`
4. `~/.watchdog/backups/`

## Platform Paths

### Windows

1. Runtime root: `C:\Users\<you>\.watchdog`
2. Database: `C:\Users\<you>\.watchdog\watchdog.db`
3. Hooks: `C:\Users\<you>\.watchdog\hooks`
4. Config: `C:\Users\<you>\.watchdog\config`
5. Artifacts: `C:\Users\<you>\.watchdog\jobs\<project>\artifacts`

### macOS

1. Runtime root: `/Users/<you>/.watchdog`
2. Database: `/Users/<you>/.watchdog/watchdog.db`
3. Hooks: `/Users/<you>/.watchdog/hooks`
4. Config: `/Users/<you>/.watchdog/config`
5. Artifacts: `/Users/<you>/.watchdog/jobs/<project>/artifacts`

### Linux

1. Runtime root: `/home/<you>/.watchdog`
2. Database: `/home/<you>/.watchdog/watchdog.db`
3. Hooks: `/home/<you>/.watchdog/hooks`
4. Config: `/home/<you>/.watchdog/config`
5. Artifacts: `/home/<you>/.watchdog/jobs/<project>/artifacts`

## Worker Jobs

HyperVibing supports two worker backend styles.

### Command backend

Use this when you want bounded local commands.

Examples:

1. `dotnet test`
2. `dotnet build`
3. `git diff --stat --no-ext-diff`

By default, command task specs are restricted to allowed commands from config.

Current default allow-list:

1. `dotnet`
2. `git`

### Claude backend

Use this when you need a real bounded Claude worker job.

Policy controls:

1. Model
2. Effort
3. Agent
4. Permission mode
5. Allowed tools
6. Additional directories
7. Max budget
8. Optional appended system prompt

The worker is launched through the Claude CLI with `--print` and receives its prompt through standard input.

## Review Evidence And Policy

Each project has an effective workflow policy.

That policy can enforce:

1. Fresh tests before review
2. Fresh builds before review
3. No unacknowledged critical alerts before review
4. No unacknowledged warning alerts before review
5. Evidence freshness window
6. Default worker backend

## Safety Model

The safety layer evaluates structured tool-use events.

Default safety rules are:

1. `destructive_command`
2. `secret_in_code`
3. `force_push`

Examples of what HyperVibing looks for:

1. `git reset --hard`
2. `git clean -f`
3. `git push --force`
4. Recursive delete operations
5. Secret-like writes in file mutation tools

Critical alerts are recorded and can trigger automatic escalation nudges.

## Configuration

Global config is stored in `~/.watchdog/config/settings.json`.

Current keys include:

1. `stall_threshold_seconds`
2. `urgency_threshold`
3. `session_budget`
4. `subagent_timeout_seconds`
5. `evidence_freshness_minutes`
6. `allowed_subagent_commands`
7. `reflection_window_minutes`
8. `relay_baton_max_age_hours`
9. `safety_rules`
10. `default_preset`
11. `auto_loop_enabled`
12. `auto_loop_interval_seconds`

## Deliberation Loop

The loop layer exists so the supervisor can assess all projects and decide whether nudges should be sent.

Relevant tools:

1. `watchdog_deliberate`
2. `watchdog_reset_budget`
3. `watchdog_act_on_decision`

Use `watchdog_deliberate` when you want recommendations without acting.

Use `watchdog_act_on_decision` when you want HyperVibing to deliberate and immediately send nudges for projects that need them.
