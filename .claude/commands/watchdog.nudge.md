---
description: Manually send a Watchdog nudge to a monitored project
---

# Watchdog Nudge

If the user did not provide a project and message, ask for the missing values.

Default to:

- priority: `normal`
- tone: `reminder`
- expiresInMinutes: `30`

Only override those defaults when the user asks.

Call `watchdog_send_nudge`.

Output a concise confirmation with:

- project
- tone
- priority
- expiry
- short message preview
