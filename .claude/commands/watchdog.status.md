---
description: Show a formatted Watchdog status summary for one or all monitored projects
---

# Watchdog Status

If the user supplied a project name, call `watchdog_get_status` for that project.
If they did not, call `watchdog_get_status` with no project.

Output a concise summary with:

- project name
- workflow stage
- stalled or active state
- evidence state
- open alerts
- inbox/outbox counts

If multiple projects are returned, group them into:

- needs attention
- healthy or review-ready

If any project is stalled and missing evidence, explicitly recommend either `/watchdog.smoke-worker <project>` or `/watchdog.jobs <project>`.
