---
description: Read recent event stream entries for a monitored project
---

# Watchdog Stream

If the user did not provide a project name, ask for it.

Use the supplied cursor when present. Otherwise start at `0` unless the user asked for only recent entries, in which case you may choose a reasonable higher cursor after checking status.

Call `watchdog_read_stream`.

Summarize the result with:

- project
- returned event count
- next cursor
- a compact list of the most relevant recent events

If the stream is empty or stale, say so directly.
