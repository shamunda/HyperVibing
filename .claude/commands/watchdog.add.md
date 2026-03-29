---
description: Register a project with Watchdog and install its hooks
---

# Watchdog Add

If the user did not provide both a project name and an absolute path, ask for the missing value.

Call `watchdog_add_project` with the provided name and path.

If registration succeeds, immediately call `watchdog_install_hooks` for the same project.

Output a compact summary with:

- project name
- resolved path
- whether the project already existed
- whether hooks were installed

Always end with this exact instruction to the user:

> Hooks are written but only activate on session start. **Close this Claude session, then open a new one in `<path>` to begin monitored work.**
