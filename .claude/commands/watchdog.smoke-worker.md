---
description: Launch a live bounded Claude worker smoke test for a monitored project
---

If the user did not provide a project name, ask for it.

Call `watchdog_smoke_test_worker` with the project.

If the smoke test completes in the wait window:
- report success or failure clearly
- summarize the result
- mention the job id
- if an artifact preview is present, summarize what it proves

If the smoke test is still running:
- report that it was launched
- mention the job id
- tell the user to run `/watchdog.jobs <project>` or `/watchdog.artifact <project> <job-id>` next

If the smoke test fails immediately, surface the failure plainly and recommend inspecting the artifact.