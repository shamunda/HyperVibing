---
description: Read a worker job artifact log for a monitored project
---

If the user did not provide both a project name and a job id, ask for the missing arguments.

Call `watchdog_read_job_artifact` with the provided project and job id.

Summarize the artifact first:
- whether it looks successful or failed
- the key lines
- any obvious errors or next actions

Then include the raw artifact content in a fenced text block if it is short enough to be useful.