---
description: Update a monitored project's review policy or default worker backend
---

If the user did not specify a project, ask for it.

Collect enough detail to call `watchdog_set_project_policy` sensibly. At minimum determine:
- project
- whether fresh tests are required
- whether fresh builds are required
- whether warnings block review
- whether default backend should be `Command` or `Claude`

If the backend is `Claude`, also ask for or infer:
- model
- effort
- permission mode
- allowed tools when relevant

After calling the tool, summarize the effective policy and the practical impact on review gates and worker execution.