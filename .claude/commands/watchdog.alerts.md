---
description: Show recent safety alerts for a monitored project
---

If the user did not provide a project name, ask for it.

Call `watchdog_get_alerts` for the project.

Output a concise alert summary with:
- total alerts
- critical vs warning mix when present
- whether any are still unacknowledged
- the most important recent alert reasons

If there are no alerts, say the project is currently clean.