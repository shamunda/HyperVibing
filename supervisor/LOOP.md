# Watchdog Deliberative Loop — Phase 2 Reference

This document describes the **REFLECT → PERCEIVE → TRIAGE → DELIBERATE → ACT** cycle
that the Watchdog supervisor follows autonomously.

Phase 2 adds outcome tracking, EMA-based strategy scoring, and tone selection
informed by historical effectiveness. The loop remains conservative: observe more,
nudge less.

---

## Shortcut — Automated Cycle

Call `watchdog_act_on_decision` to run the full cycle (including reflection) and
auto-send nudges for every project with a `Nudge` recommendation.

Use `watchdog_deliberate` when you want to review recommendations **before** acting.

---

## Step 0 — REFLECT (handled automatically)

`watchdog_deliberate` and `watchdog_act_on_decision` both begin by processing all
pending reflections whose `reflect_after` timestamp has passed.

For each due reflection the server:
1. Checks whether the project's stream cursor advanced since the nudge was sent.
2. Records the outcome (`activity_resumed`, `cursor_delta`) in the episode file.
3. Updates the strategy profile via EMA (α = 0.2).
4. Removes the reflection from the pending queue.

You do not need to call anything manually for this step.

---

## Step 1 — PERCEIVE

`watchdog_deliberate` calls `watchdog_get_status` internally. The response
includes per-project:
- `is_stalled` — true if idle longer than `stall_threshold_seconds`
- `seconds_since_last_event`
- `inbox_count` — messages already waiting

---

## Step 2 — TRIAGE

For each stalled project the server computes an **urgency score** [0.0–1.0]:

```
urgency = 0.7 × stallComponent + 0.3 × budgetComponent

stallComponent  = stallSeconds / stallThresholdSeconds   (clamped 0–1)
budgetComponent = 1 − budgetRemaining / sessionBudget    (clamped 0–1)
```

**Recommendation is `Skip` if any of:**
- Not stalled
- `urgency < urgency_threshold` (default: 0.40)
- `inbox_count > 0` — nudge already queued → `AlreadyQueued`
- Budget exhausted → `BudgetExhausted`

**Recommendation is `Nudge` if:**
- Stalled AND urgency above threshold AND inbox empty AND budget remains

---

## Step 3 — DELIBERATE (Tone Selection)

The server selects the tone with the **highest EMA effectiveness score** for the
project (falls back to global scores if no per-project history yet):

| Tone | Default score | When preferred |
|---|---|---|
| `reminder` | 0.5 | First nudge; short stalls |
| `redirect` | 0.5 | Agent going in the wrong direction |
| `escalation` | 0.5 | Long stall; repeated misses |

All three start at 0.5. Scores evolve based on whether activity resumed after
each nudge. The strategy profile is persisted in `~/.watchdog/config/profile.json`.

---

## Step 4 — ACT

When calling `watchdog_send_nudge` based on a deliberation result, pass:
- `urgency_score` — from the decision's `urgency_score` field
- `deliberation_summary` — from the decision's `reason` field

This ensures the episode record is complete for future reflection.

**Nudge writing guide:**
- Be direct and specific. Reference what the agent was last doing if you know it.
- Don't be preachy. One nudge says one thing.
- Good: *"You were reading auth.ts. If you're unclear on the next step, try listing
  the functions in that file and identifying which one needs to change."*
- Bad: *"Remember to keep making progress! You should continue working on the task."*

---

## Observation Cadence

The loop runs on demand:
- User commands (`/status`, `/nudge`, etc.)
- Explicit user request to "run the loop"
- `watchdog_act_on_decision` for fully automated single-call execution

Phase 3 will add proactive triggering from the event stream.

---

## Budget Exhaustion

When the budget reaches 0, the server returns `BudgetExhausted` for all projects.
Log it and switch to observe-only:

> "Session nudge budget exhausted. Switching to observe-only. Use /nudge to send
> manual messages, or call `watchdog_reset_budget` to restore the budget."

Reset: `watchdog_reset_budget` — call this at the start of each new Claude session.
