# Claude Commands

This document describes the Claude Code slash commands shipped in `.claude/commands/`.

These commands are convenience wrappers around the raw MCP tools.

Use them when you are already inside Claude Code.

## What Exists

| Slash command | Wraps tool(s) | What it is for |
| --- | --- | --- |
| `/watchdog.add` | `watchdog_add_project`, `watchdog_install_hooks` | Register a project and install its hooks |
| `/watchdog.status` | `watchdog_get_status` | Show current health for one or all projects |
| `/watchdog.stream` | `watchdog_read_stream` | Show recent tool-use events |
| `/watchdog.nudge` | `watchdog_send_nudge` | Inject a message into the monitored project session |
| `/watchdog.jobs` | `watchdog_list_jobs` | Show recent worker jobs |
| `/watchdog.artifact` | `watchdog_read_job_artifact` | Read saved output from one job |
| `/watchdog.policy` | `watchdog_get_project_policy` | Show review and worker policy |
| `/watchdog.set-policy` | `watchdog_set_project_policy` | Update review gates or worker backend |
| `/watchdog.alerts` | `watchdog_get_alerts` | Show safety alerts |
| `/watchdog.patterns` | `watchdog_get_patterns` | Show learned patterns |
| `/watchdog.self-report` | `watchdog_self_report` | Show a session summary |
| `/watchdog.smoke-worker` | `watchdog_smoke_test_worker` | Launch a live worker smoke test |

## The Commands Most Users Need

### `/watchdog.add`

Use this first for every new project.

What it does:

1. Registers the project with HyperVibing
2. Installs Claude hooks into `<project>/.claude/settings.json`
3. Installs git hooks if the project has `.git/hooks`

What you must do next:

1. Close that Claude session
2. Open a new Claude session in the project directory

### `/watchdog.status`

Use this from the supervisor session.

It summarizes:

1. Which projects are active or stalled
2. Workflow stage
3. Evidence state
4. Open alerts
5. Inbox and outbox counts

### `/watchdog.stream <project>`

Use this when status is not enough and you need to see the recent tool-use record.

### `/watchdog.nudge <project> <message>`

Use this when you need to intervene.

The nudge is delivered by the PreToolUse hook on the monitored session's next tool call.

### `/watchdog.jobs <project>` and `/watchdog.artifact <project> <job-id>`

Use these together.

1. `/watchdog.jobs` shows the recent jobs
2. `/watchdog.artifact` shows the saved output of one job

### `/watchdog.policy <project>` and `/watchdog.set-policy <project> ...`

Use these when you want to change what counts as review-ready or which worker backend a project uses.

## What Each Command Is Good For

### Project setup

1. `/watchdog.add`

### Daily monitoring

1. `/watchdog.status`
2. `/watchdog.stream <project>`
3. `/watchdog.alerts <project>`

### Intervention

1. `/watchdog.nudge <project> <message>`
2. `/watchdog.smoke-worker <project>`

### Worker inspection

1. `/watchdog.jobs <project>`
2. `/watchdog.artifact <project> <job-id>`

### Policy inspection and changes

1. `/watchdog.policy <project>`
2. `/watchdog.set-policy <project> ...`

### Learning and reporting

1. `/watchdog.patterns [project]`
2. `/watchdog.self-report`

## Exact Usage Notes

### `/watchdog.add` asks for missing values

If you do not provide both a project name and an absolute path, the command will ask for them.

### `/watchdog.status` can be global or project-specific

If you provide no project, it summarizes all projects.

If you provide one project, it focuses on that project.

### `/watchdog.set-policy` is intentionally strict

At minimum, it needs:

1. Project name
2. Whether fresh tests are required
3. Whether fresh builds are required
4. Whether critical alerts block review
5. Whether warnings block review
6. Which worker backend to use: `Command` or `Claude`

If you choose the `Claude` backend, expect follow-up questions about:

1. Model
2. Effort
3. Permission mode
4. Allowed tools when relevant

### `/watchdog.smoke-worker` is a real execution path

This command does not just preview configuration.

It launches a bounded worker job and either:

1. Returns a completed result if it finishes in the wait window
2. Returns a running job id so you can inspect it later

## Recommended Operator Flow

For a normal supervisor session, use this order:

1. `/watchdog.status`
2. `/watchdog.stream <project>` if the project needs inspection
3. `/watchdog.jobs <project>` if worker output matters
4. `/watchdog.nudge <project> <message>` if you need to intervene
5. `/watchdog.policy <project>` if the problem is a policy issue rather than a one-off issue
