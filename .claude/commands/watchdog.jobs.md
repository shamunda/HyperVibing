---
description: List recent worker jobs for a monitored project
---

If the user did not provide a project name, ask for it.

Call `watchdog_list_jobs` for the project.

Output a compact job table-like summary with:
- job id
- task kind
- status
- created/start/completed times if present
- short result

If any job failed, tell the user they can inspect it with `/watchdog.artifact <project> <job-id>`.