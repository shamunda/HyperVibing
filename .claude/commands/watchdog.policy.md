---
description: Inspect the workflow and worker-backend policy for a monitored project
---

If the user did not provide a project name, ask for it.

Call `watchdog_get_project_policy`.

Explain the project's current policy in operator language:
- what evidence is required before review
- whether warnings block review
- whether workers default to Command or Claude
- Claude model, effort, and allowed tools if the backend is Claude

If the policy looks too strict or too loose, suggest a concrete `/watchdog.set-policy` invocation.