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
/// Aggregates job and alert history into a compact evidence summary for a project.
/// Evidence is used by status reporting and the deliberation loop.
/// </summary>
public class EvidenceService
{
    public EvidenceSummary Summarize(string project)
    {
        var settings = SettingsLoader.Get();
        var policy   = ProjectRegistry.Get(project)?.EffectivePolicy ?? ProjectWorkflowPolicy.Default;
        var jobs     = JobStore.ListAll(project);
        var alerts   = AlertStore.ReadUnacknowledged(project);
        var now      = DateTimeOffset.UtcNow;

        var lastBuild = jobs
            .Where(j => j.TaskKind == SubagentTaskKind.Build && j.CompletedAt is not null)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefault();

        var lastTest = jobs
            .Where(j => j.TaskKind == SubagentTaskKind.RunTests && j.CompletedAt is not null)
            .OrderByDescending(j => j.CompletedAt)
            .FirstOrDefault();

        var lastVerification = new[] { lastBuild, lastTest }
            .Where(j => j?.CompletedAt is not null)
            .OrderByDescending(j => j!.CompletedAt)
            .FirstOrDefault();

        var lastVerificationAt = lastVerification?.CompletedAt;
        var freshnessMinutes   = policy.ReviewEvidence.EvidenceFreshnessMinutes ?? settings.EvidenceFreshnessMinutes;
        var freshnessWindow    = TimeSpan.FromMinutes(Math.Max(1, freshnessMinutes));
        var hasFreshVerification = lastVerificationAt is not null &&
                                   now - lastVerificationAt.Value <= freshnessWindow &&
                                   lastVerification?.Status == JobStatus.Completed;

        var meetsReviewEvidence = MeetsReviewEvidence(policy, hasFreshVerification, lastBuild, lastTest, alerts);

        var failedVerification = new[] { lastBuild, lastTest }
            .Where(j => j is not null && j.Status == JobStatus.Failed)
            .OrderByDescending(j => j!.CompletedAt)
            .FirstOrDefault();

        var findings = BuildFindings(policy, lastBuild, lastTest, alerts, hasFreshVerification, meetsReviewEvidence);

        return new EvidenceSummary(
            RunningJobs:          jobs.Count(j => j.Status == JobStatus.Running),
            PendingJobs:          jobs.Count(j => j.Status == JobStatus.Pending),
            FailedJobs:           jobs.Count(j => j.Status == JobStatus.Failed),
            CompletedJobs:        jobs.Count(j => j.Status == JobStatus.Completed),
            CriticalAlerts:       alerts.Count(a => a.Severity == AlertSeverity.Critical),
            WarningAlerts:        alerts.Count(a => a.Severity == AlertSeverity.Warning),
            LastBuildAt:          lastBuild?.CompletedAt,
            LastBuildSucceeded:   ToSucceeded(lastBuild),
            LastTestAt:           lastTest?.CompletedAt,
            LastTestSucceeded:    ToSucceeded(lastTest),
            LastVerificationAt:   lastVerificationAt,
            HasFreshVerification: hasFreshVerification,
                NeedsVerification:    !meetsReviewEvidence,
            LatestFailure:        failedVerification?.Result,
            Findings:             findings);
    }

    private static bool? ToSucceeded(SubagentJob? job) => job?.Status switch
    {
        JobStatus.Completed => true,
        JobStatus.Failed    => false,
        _                   => null
    };

    private static bool MeetsReviewEvidence(ProjectWorkflowPolicy policy, bool hasFreshVerification,
        SubagentJob? lastBuild, SubagentJob? lastTest, List<SafetyAlert> alerts)
    {
        if (!hasFreshVerification) return false;

        if (policy.ReviewEvidence.RequireFreshTests && lastTest?.Status != JobStatus.Completed)
            return false;

        if (policy.ReviewEvidence.RequireFreshBuild && lastBuild?.Status != JobStatus.Completed)
            return false;

        if (policy.ReviewEvidence.RequireNoCriticalAlerts && alerts.Any(a => a.Severity == AlertSeverity.Critical))
            return false;

        if (policy.ReviewEvidence.RequireNoWarnings && alerts.Any(a => a.Severity == AlertSeverity.Warning))
            return false;

        return true;
    }

    private static string[] BuildFindings(ProjectWorkflowPolicy policy, SubagentJob? lastBuild, SubagentJob? lastTest,
        List<SafetyAlert> alerts, bool hasFreshVerification, bool meetsReviewEvidence)
    {
        var findings = new List<string>();

        if (lastBuild is { Status: JobStatus.Failed, CompletedAt: not null })
            findings.Add($"Build failed at {lastBuild.CompletedAt:O}.");

        if (lastTest is { Status: JobStatus.Failed, CompletedAt: not null })
            findings.Add($"Tests failed at {lastTest.CompletedAt:O}.");

        if (alerts.Count > 0)
            findings.Add($"{alerts.Count} unacknowledged safety alert(s) remain open.");

        if (!hasFreshVerification)
            findings.Add("Verification evidence is stale or missing.");

        if (policy.ReviewEvidence.RequireFreshTests && lastTest?.Status != JobStatus.Completed)
            findings.Add("Project policy requires a fresh successful test run before review.");

        if (policy.ReviewEvidence.RequireFreshBuild && lastBuild?.Status != JobStatus.Completed)
            findings.Add("Project policy requires a fresh successful build before review.");

        if (!meetsReviewEvidence)
            findings.Add("Current evidence does not satisfy the project's review policy.");

        return findings.ToArray();
    }
}