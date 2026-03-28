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
/// Implements the deliberative loop: PERCEIVE → TRIAGE → DELIBERATE.
///
/// Returns a recommendation per project — it does NOT send nudges.
/// The supervisor (Claude via MCP) or <see cref="NudgeService"/> acts on the result,
/// keeping decision logic separate from side-effectful messaging.
/// </summary>
public class DeliberationService(
    StatusService    statusService,
    UrgencyService   urgencyService,
    BudgetService    budgetService,
    ReflectionService reflectionService)
{
    /// <summary>
    /// Runs one full deliberation cycle across all registered projects.
    /// Processes due reflections first so the strategy profile is current.
    /// Includes a meta-cognition step: if recent effectiveness is low,
    /// the supervisor is advised to favor observation over intervention.
    /// </summary>
    public List<DeliberationDecision> RunLoop()
    {
        // REFLECT — close feedback loops before computing urgency
        reflectionService.ProcessDue();

        // META-COGNITION — self-assess recent performance
        var meta = ComputeMetaAssessment();

        var settings  = SettingsLoader.Get();
        var projects  = statusService.GetAll();
        var decisions = new List<DeliberationDecision>(projects.Count);

        foreach (var status in projects)
        {
            var decision = Deliberate(status, settings);
            if (meta is not null)
                decision = decision with { MetaAssessment = meta };
            decisions.Add(decision);
        }

        return decisions;
    }

    // ── Private ───────────────────────────────────────────────────────────

    private DeliberationDecision Deliberate(ProjectStatus status, WatchdogSettings settings)
    {
        if (status.Workflow is { Stage: WorkflowStage.Escalate } workflowEscalate)
        {
            return new DeliberationDecision(
                Project:         status.Name,
                Action:          DeliberationAction.Nudge,
                Reason:          workflowEscalate.Summary,
                UrgencyScore:    1.0,
                SuggestedTone:   "escalation",
                BudgetRemaining: budgetService.Remaining,
                StrategyScore:   null,
                StallSeconds:    status.SecondsSinceLastEvent ?? 0,
                WorkflowStage:   workflowEscalate.Stage,
                WorkflowSummary: workflowEscalate.Summary,
                NeedsEvidence:   workflowEscalate.NeedsEvidence);
        }

        if (status.Workflow is { Stage: WorkflowStage.Validate } workflowValidate &&
            status.Evidence is not null &&
            (status.Evidence.RunningJobs > 0 || status.Evidence.PendingJobs > 0))
        {
            return Decision(status.Name, DeliberationAction.Skip,
                "Verification is already in progress — observe until the current job completes.",
                urgencyScore: null,
                suggestedTone: null,
                status,
                workflowValidate);
        }

        if (status.Workflow is { Stage: WorkflowStage.Review } workflowReview &&
            status.Evidence is { HasFreshVerification: true })
        {
            return Decision(status.Name, DeliberationAction.Skip,
                "Fresh verification evidence exists — project is ready for review rather than another nudge.",
                urgencyScore: null,
                suggestedTone: null,
                status,
                workflowReview);
        }

        // PERCEIVE — is the project stalled?
        if (!status.IsStalled || status.SecondsSinceLastEvent is null)
        {
            return Decision(status.Name, DeliberationAction.Skip,
                "Project is active — no stall detected.",
                urgencyScore: null, suggestedTone: null, status, status.Workflow);
        }

        // TRIAGE — compute urgency score
        var urgency = urgencyService.Compute(
            stallSeconds:         status.SecondsSinceLastEvent.Value,
            budgetRemaining:      budgetService.Remaining,
            sessionBudget:        settings.SessionBudget,
            stallThresholdSeconds: settings.StallThresholdSeconds);

        if (urgency < settings.UrgencyThreshold)
        {
            return Decision(status.Name, DeliberationAction.Skip,
                $"Urgency {urgency:F2} below threshold {settings.UrgencyThreshold:F2}.",
                urgency, suggestedTone: null, status, status.Workflow);
        }

        // DELIBERATE — check for blocking conditions
        if (status.InboxCount > 0)
        {
            return Decision(status.Name, DeliberationAction.AlreadyQueued,
                "A nudge is already waiting in the project inbox.",
                urgency, suggestedTone: null, status, status.Workflow);
        }

        if (!budgetService.HasBudget)
        {
            return Decision(status.Name, DeliberationAction.BudgetExhausted,
                "Session nudge budget is exhausted.",
                urgency, suggestedTone: null, status, status.Workflow);
        }

        // SELECT TONE — pick the highest-scoring tone for this project
        var scores = ProfileStore.GetScores(status.Name);
        var (tone, strategyScore) = SelectTone(scores);

        if (status.Workflow is { Stage: WorkflowStage.Refine })
            tone = "redirect";

        if (status.Workflow is { Stage: WorkflowStage.Implement } && status.Evidence?.NeedsVerification == true)
            tone = "redirect";

        var reason = status.Workflow is { Summary: not null } workflow
            ? $"{workflow.Summary} Stall detected ({status.SecondsSinceLastEvent.Value:F0}s). Urgency: {urgency:F2}."
            : $"Stall detected ({status.SecondsSinceLastEvent.Value:F0}s). Urgency: {urgency:F2}.";

        return new DeliberationDecision(
            Project:         status.Name,
            Action:          DeliberationAction.Nudge,
            Reason:          reason,
            UrgencyScore:    urgency,
            SuggestedTone:   tone,
            BudgetRemaining: budgetService.Remaining,
            StrategyScore:   strategyScore,
            StallSeconds:    status.SecondsSinceLastEvent.Value,
            WorkflowStage:   status.Workflow?.Stage,
            WorkflowSummary: status.Workflow?.Summary,
            NeedsEvidence:   status.Workflow?.NeedsEvidence ?? false);
    }

    /// <summary>
    /// Returns the tone name and its effectiveness score, picking the highest scorer.
    /// Falls back to "reminder" when all scores are equal.
    /// </summary>
    private static (string Tone, double Score) SelectTone(ToneScores scores)
    {
        var candidates = new[]
        {
            ("reminder",   scores.Reminder),
            ("redirect",   scores.Redirect),
            ("escalation", scores.Escalation)
        };

        var best = candidates.MaxBy(c => c.Item2);
        return (best.Item1, best.Item2);
    }

    private DeliberationDecision Decision(
        string project, DeliberationAction action, string reason,
        double? urgencyScore, string? suggestedTone, ProjectStatus status, WorkflowAssessment? workflow,
        double stallSeconds = 0) =>
        new(project, action, reason, urgencyScore, suggestedTone,
            BudgetRemaining: budgetService.Remaining, StrategyScore: null,
            StallSeconds: stallSeconds,
            WorkflowStage: workflow?.Stage,
            WorkflowSummary: workflow?.Summary,
            NeedsEvidence: workflow?.NeedsEvidence ?? false);

    /// <summary>
    /// Self-assessment: reviews recent episode outcomes across all projects.
    /// If the overall win rate is below 30%, recommends observation over intervention
    /// so the supervisor does not waste budget on ineffective nudges.
    /// </summary>
    private static string? ComputeMetaAssessment()
    {
        var allProjects = ProjectRegistry.Load().Projects;
        var totalEpisodes   = 0;
        var effectiveCount  = 0;

        foreach (var project in allProjects)
        {
            var episodes = EpisodeStore.ReadRecent(project.Name, maxDays: 1);
            foreach (var ep in episodes.Where(e => e.Outcome is not null))
            {
                totalEpisodes++;
                if (ep.Outcome!.Effective) effectiveCount++;
            }
        }

        if (totalEpisodes < 3) return null; // not enough data for self-assessment

        var winRate = (double)effectiveCount / totalEpisodes;
        if (winRate < 0.30)
            return $"Recent effectiveness low ({winRate:P0} win rate over {totalEpisodes} nudges). " +
                   "Favoring observation over intervention.";

        return null;
    }
}
