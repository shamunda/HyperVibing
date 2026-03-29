# HyperVibing

HyperVibing is an MCP server for Claude Code that monitors active project sessions and gives you a separate supervisor session for visibility and intervention.

Use it when you want to:

1. Detect stalled Claude Code sessions.
2. See what a project session actually did.
3. Send nudges back into a project session.
4. Run bounded worker jobs without cluttering the main project session.

Important naming note:

1. The repository is named `HyperVibing`.
2. The current MCP tools and slash commands use the `watchdog_*` prefix.
3. In Claude Code, you will use `/watchdog.*` commands.

## Requirements

1. [.NET 9 SDK](https://dotnet.microsoft.com/download)
2. [Claude Code CLI](https://docs.anthropic.com/claude-code)
3. Git
4. PowerShell

## Detailed Docs

If you want the full MCP and runtime reference, start here:

1. [docs/README.md](docs/README.md)
2. [docs/installation-and-platforms.md](docs/installation-and-platforms.md)
3. [docs/mcp-tools.md](docs/mcp-tools.md)
4. [docs/claude-commands.md](docs/claude-commands.md)
5. [docs/runtime-architecture.md](docs/runtime-architecture.md)

## What Installation Does

Running `install.ps1` does all of the following:

1. Builds and publishes the server to `~/.watchdog/server/`
2. Copies hook scripts to `~/.watchdog/hooks/`
3. Copies default config to `~/.watchdog/config/`
4. Creates `~/.watchdog/backups/`
5. Registers the MCP server with Claude Code using `claude mcp add --scope user`
6. Installs git safety hooks into this repository

## Quick Start

Follow these steps exactly.

### 1. Clone the repository

```powershell
git clone https://github.com/shamunda/HyperVibing.git
cd HyperVibing
```

### 2. Run the installer

```powershell
.\install.ps1
```

### 3. Start Claude Code once so it loads HyperVibing

```powershell
claude --dangerously-skip-permissions
```

### 4. Register a project

Inside Claude, run:

```text
/watchdog.add
```

Provide:

1. A short project name such as `demo-app`
2. The absolute path to the project folder

### 5. Close that Claude session

This is required.

Hooks only activate when a Claude session starts.

### 6. Open a new Claude session inside the project folder

```powershell
cd C:\full\path\to\your\project
claude --dangerously-skip-permissions
```

That session is now monitored.

### 7. Open a supervisor session

In another terminal:

```powershell
cd C:\full\path\to\HyperVibing\supervisor
claude --dangerously-skip-permissions
```

Inside the supervisor session, run:

```text
/watchdog.status
```

You should now see your project.

## First Demo From Scratch

This section is for a brand-new user who wants a safe test drive.

### Step 1. Create a demo folder

```powershell
mkdir C:\HyperVibingDemo
cd C:\HyperVibingDemo
```

### Step 2. Create a small example project

```powershell
dotnet new console -n DemoApp
cd DemoApp
git init
git add .
git commit -m "initial commit"
```

Your example project now lives at:

```text
C:\HyperVibingDemo\DemoApp
```

### Step 3. Install HyperVibing

In a separate terminal:

```powershell
cd C:\full\path\to\HyperVibing
.\install.ps1
```

### Step 4. Open Claude once outside the demo project

```powershell
claude --dangerously-skip-permissions
```

Inside Claude, run:

```text
/watchdog.add
```

Then provide:

1. Project name: `demo-app`
2. Absolute path: `C:\HyperVibingDemo\DemoApp`

### Step 5. Close that Claude session

Do not skip this step.

### Step 6. Start the monitored project session

```powershell
cd C:\HyperVibingDemo\DemoApp
claude --dangerously-skip-permissions
```

Inside Claude, give it a real task:

```text
Create a Greeter class, update Program.cs to use it, and run dotnet build.
```

### Step 7. Start the supervisor session

```powershell
cd C:\full\path\to\HyperVibing\supervisor
claude --dangerously-skip-permissions
```

Inside the supervisor session, run these commands in order:

```text
/watchdog.status
/watchdog.stream demo-app
/watchdog.jobs demo-app
```

You now have a working monitored project and a working supervisor session.

## What You Should See In Practice

Use the demo above and verify these benefits.

### 1. Stall detection

1. Leave the project session idle for about 60 seconds.
2. In the supervisor session, run:

```text
/watchdog.status
```

1. The project should be reported as stalled.

### 2. Event visibility

1. Go back to the project session.
2. Ask Claude to do one more task:

```text
Add a second method to Greeter and run dotnet build again.
```

1. In the supervisor session, run:

```text
/watchdog.stream demo-app
```

1. You should see the recent tool activity.

### 3. Supervisor intervention

In the supervisor session, run:

```text
/watchdog.nudge demo-app Please run dotnet build before continuing.
```

That nudge is injected into the monitored project session on its next tool call.

### 4. Worker job execution

In the supervisor session, run:

```text
/watchdog.smoke-worker demo-app
/watchdog.jobs demo-app
```

If a job artifact exists, read it with:

```text
/watchdog.artifact demo-app <job-id>
```

## Normal Day-To-Day Flow

Once installed, this is the standard workflow.

### Install once

1. Clone the repository.
2. Run `.\install.ps1`.
3. Confirm `claude mcp list` shows `watchdog`.

### Register each project once

1. Start Claude anywhere.
2. Run `/watchdog.add`.
3. Give the project name and absolute path.
4. Close that Claude session.

### Start monitored work each time

1. Open Claude inside the project folder.
2. Work normally.
3. Open another Claude session in `supervisor/` when you want oversight.

## Commands Most Users Need

### Register a project

```text
/watchdog.add
```

### See project status

```text
/watchdog.status
```

### Read project activity

```text
/watchdog.stream <project>
```

### Send a nudge

```text
/watchdog.nudge <project> <message>
```

### List jobs

```text
/watchdog.jobs <project>
```

### Read a job artifact

```text
/watchdog.artifact <project> <job-id>
```

### Read alerts

```text
/watchdog.alerts <project>
```

### Read learned patterns

```text
/watchdog.patterns <project>
```

### Read project policy

```text
/watchdog.policy <project>
```

### Run a worker smoke test

```text
/watchdog.smoke-worker <project>
```

## Files HyperVibing Writes

### In your home directory

```text
~/.watchdog/server/
~/.watchdog/hooks/
~/.watchdog/config/
~/.watchdog/backups/
~/.watchdog/watchdog.db
```

### In Claude config

```text
~/.claude.json
```

### In each monitored project

```text
<project>/.watchdog
<project>/.claude/settings.json
<project>/.git/hooks/pre-commit
<project>/.git/hooks/pre-push
```

## Runtime Storage

HyperVibing uses a hybrid runtime model.

1. Control-plane state is stored in SQLite at `~/.watchdog/watchdog.db`
2. Large worker logs are stored under `~/.watchdog/jobs/<project>/artifacts/`

If the main database is damaged, HyperVibing attempts to recover from the latest backup.

## Common Problems

### Hooks were installed, but nothing happens

Cause:

You are still using the old Claude session.

Fix:

1. Close that session.
2. Open a new Claude session in the project folder.

### Claude does not show HyperVibing tools

Fix:

1. Run `.\install.ps1` again.
2. Run `claude mcp list`.
3. Confirm `watchdog` appears.
4. Restart Claude Code.

### Git hooks were not installed

Cause:

The target project was not a git repository.

Fix:

1. Run `git init` in the project.
2. Register the project again.

### `dotnet test` fails with workload errors on this machine

Use this fallback:

1. Run `dotnet build Watchdog.sln --nologo`
2. Run `dotnet vstest tests\Watchdog.Tests\bin\Debug\net9.0\Watchdog.Tests.dll`

## Advanced Use

You can tighten review policy per project.

Examples:

1. Require fresh tests before review.
2. Require a fresh build before review.
3. Block review when critical alerts exist.
4. Switch the default worker backend from command jobs to Claude jobs.

Use:

```text
/watchdog.policy <project>
/watchdog.set-policy <project> ...
```

## Validation

Current repository baseline:

1. `dotnet build Watchdog.sln --nologo`
2. `dotnet vstest tests\Watchdog.Tests\bin\Debug\net9.0\Watchdog.Tests.dll`

Current result:

1. 61 tests passed
2. 0 tests failed

## License

MIT. See [LICENSE](LICENSE).
