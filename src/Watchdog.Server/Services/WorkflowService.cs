// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Server.Services;

/// <summary>
/// Projects the current workflow stage from health and evidence signals.
/// This keeps deliberation policy separate from raw status collection.
/// </summary>
public class WorkflowService
{
    public WorkflowAssessment Assess(ProjectStatus status, EvidenceSummary evidence)
    {
        var policy = ProjectRegistry.Get(status.Name)?.EffectivePolicy ?? ProjectWorkflowPolicy.Default;

        if (evidence.CriticalAlerts > 0)
        {
            return new WorkflowAssessment(
                Stage:          WorkflowStage.Escalate,
                Summary:        "Critical safety alerts require intervention.",
                NeedsAttention: true,
                NeedsEvidence:  false,
                BlockingReason: "Unacknowledged critical alerts are open.");
        }

        if (evidence.RunningJobs > 0 || evidence.PendingJobs > 0)
        {
            return new WorkflowAssessment(
                Stage:          WorkflowStage.Validate,
                Summary:        "Verification work is currently running.",
                NeedsAttention: false,
                NeedsEvidence:  false,
                BlockingReason: null);
        }

        if (!string.IsNullOrWhiteSpace(evidence.LatestFailure))
        {
            return new WorkflowAssessment(
                Stage:          WorkflowStage.Refine,
                Summary:        "Latest verification failed and needs correction.",
                NeedsAttention: true,
                NeedsEvidence:  true,
                BlockingReason: evidence.LatestFailure);
        }

        if (status.EventCount == 0 && evidence.CompletedJobs == 0)
        {
            return new WorkflowAssessment(
                Stage:          WorkflowStage.Observe,
                Summary:        "No meaningful activity observed yet.",
                NeedsAttention: false,
                NeedsEvidence:  false,
                BlockingReason: null);
        }

        if (evidence.HasFreshVerification)
        {
            return new WorkflowAssessment(
                Stage:          WorkflowStage.Review,
                Summary:        evidence.NeedsVerification
                    ? "Verification exists but the project's review policy is not yet satisfied."
                    : "Fresh verification evidence is available for review.",
                NeedsAttention: evidence.NeedsVerification,
                NeedsEvidence:  evidence.NeedsVerification,
                BlockingReason: null);
        }

        return new WorkflowAssessment(
            Stage:          WorkflowStage.Implement,
            Summary:        "Coding activity has outpaced verification evidence.",
            NeedsAttention: status.IsStalled,
            NeedsEvidence:  true,
            BlockingReason: (policy.ReviewEvidence.RequireNoWarnings && evidence.WarningAlerts > 0)
                ? "Warnings are open and fresh verification evidence is missing."
                : null);
    }
}