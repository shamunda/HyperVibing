# MCP Tools

This document describes the raw MCP tools exposed by the HyperVibing server.

All tool names currently use the `watchdog_` prefix.

All tools return JSON text.

## Tool Groups

HyperVibing exposes three tool groups:

1. Observe tools: read status, streams, alerts, jobs, patterns, and reports
2. Intervene tools: register projects, install hooks, send nudges, manage policy, and launch worker jobs
3. Loop tools: run or apply the deliberation loop

## Observe Tools

| Tool | Use it when | Required input | Key output | Slash wrapper |
| --- | --- | --- | --- | --- |
| `watchdog_get_status` | You want health for one project or all projects | Optional `project` | Stall state, workflow, evidence, mailbox counts | `/watchdog.status` |
| `watchdog_read_stream` | You want recent tool-use events | `project`; optional `cursor`, `limit` | Event list and `next_cursor` | `/watchdog.stream` |
| `watchdog_list_projects` | You want the registered project list | None | All registered projects | None |
| `watchdog_get_project_policy` | You want the current review or worker policy | `project` | Effective project policy | `/watchdog.policy` |
| `watchdog_list_jobs` | You want recent worker jobs | `project`; optional `limit` | Job list with status and timestamps | `/watchdog.jobs` |
| `watchdog_read_job_artifact` | You want the saved output of one job | `project`, `jobId`; optional `maxLines` | Artifact contents | `/watchdog.artifact` |
| `watchdog_get_alerts` | You want safety alerts | `project`; optional `unacknowledgedOnly` | Recent or open alerts | `/watchdog.alerts` |
| `watchdog_self_report` | You want a session-level summary | None | Report on nudges, tone, stalls, safety | `/watchdog.self-report` |
| `watchdog_get_cross_alerts` | You want multi-project warnings | None | Cross-project alerts | None |
| `watchdog_get_patterns` | You want learned strategy patterns | Optional `project` | Pattern list and counts | `/watchdog.patterns` |

## Intervene Tools

| Tool | Use it when | Required input | Key output | Slash wrapper |
| --- | --- | --- | --- | --- |
| `watchdog_send_nudge` | You want to inject a message into the monitored session | `project`, `content` | Message id, priority, tone, expiry | `/watchdog.nudge` |
| `watchdog_add_project` | You want to register a project | `name`, `path` | Project registration result | Wrapped by `/watchdog.add` |
| `watchdog_install_hooks` | You want to write project Claude hooks | `project` | Settings path and hook install result | Wrapped by `/watchdog.add` |
| `watchdog_set_project_policy` | You want to change review gates or worker backend | `project`; other fields optional | Updated effective policy | `/watchdog.set-policy` |
| `watchdog_remove_project` | You want to unregister a project | `project` | Removal result | None |
| `watchdog_spawn_subagent` | You want a bounded worker job | `project`, `task` | Job id and initial status | None |
| `watchdog_get_job` | You want the current state of one worker job | `project`, `jobId` | Full tracked job record | None |
| `watchdog_smoke_test_worker` | You want to validate the live Claude worker backend | `project`; optional `waitForCompletionSeconds` | Smoke-test result and optional artifact preview | `/watchdog.smoke-worker` |

## Loop Tools

| Tool | Use it when | Required input | Key output | Slash wrapper |
| --- | --- | --- | --- | --- |
| `watchdog_deliberate` | You want recommendations without sending nudges yet | None | Per-project recommendation list | None |
| `watchdog_reset_budget` | You want to reset the session nudge budget | None | Remaining budget after reset | None |
| `watchdog_act_on_decision` | You want the loop to deliberate and send nudges automatically | None | Per-project action results | None |

## Important Distinctions

### `watchdog_add_project` is not the same as `/watchdog.add`

These are different entry points.

1. Raw MCP `watchdog_add_project` only registers the project.
2. Raw MCP `watchdog_install_hooks` must be called after that.
3. Claude slash command `/watchdog.add` does both in sequence.

### `watchdog_get_status` vs `watchdog_read_stream`

Use `watchdog_get_status` when you want a summary.

Use `watchdog_read_stream` when you want the actual recorded tool-use events.

### `watchdog_list_jobs` vs `watchdog_read_job_artifact`

Use `watchdog_list_jobs` to discover jobs.

Use `watchdog_read_job_artifact` after you already know the target `jobId`.

## `watchdog_spawn_subagent` Inputs

`watchdog_spawn_subagent` accepts either a simple task string or a JSON task spec string.

### Supported simple task strings

1. `run tests`
2. `check build`
3. `diff analysis`

If the project's default backend is `Claude`, any other task string is treated as a Claude worker prompt.

### JSON task spec fields

The JSON task spec supports these fields:

| Field | Meaning |
| --- | --- |
| `kind` | Task kind: `Command`, `RunTests`, `Build`, `DiffAnalysis`, or `ClaudeWorker` |
| `command` | Executable name |
| `args` | Command arguments |
| `timeoutSeconds` | Timeout in seconds |
| `prompt` | Claude worker prompt |
| `model` | Claude model override |
| `effort` | Claude effort override |
| `agent` | Claude agent override |
| `allowedTools` | Allowed Claude tools |
| `addDirs` | Extra directories granted to the worker |
| `maxBudgetUsd` | Optional worker budget |
| `appendSystemPrompt` | Extra system prompt text |

### Command allow-list

Command-based task specs are currently restricted by config.

Default allowed commands:

1. `dotnet`
2. `git`

## Typical Raw MCP Flows

### Register a project from a raw MCP client

1. Call `watchdog_add_project`
2. Call `watchdog_install_hooks`
3. Close the current Claude session in that project
4. Open a new Claude session in the project directory

### Investigate a stalled project

1. Call `watchdog_get_status`
2. Call `watchdog_read_stream`
3. Call `watchdog_list_jobs`
4. If needed, call `watchdog_send_nudge`

### Validate a worker backend

1. Call `watchdog_smoke_test_worker`
2. If it does not finish immediately, call `watchdog_list_jobs`
3. Then call `watchdog_read_job_artifact`
