namespace Watchdog.Server.Services;

/// <summary>
/// Pure computation — converts raw signals into a normalized urgency score [0.0–1.0].
/// No I/O; no state. Safe to call many times per deliberation cycle.
/// </summary>
/// <remarks>
/// Formula: 0.7 × stallComponent + 0.3 × budgetComponent
///
/// stallComponent  = elapsed / threshold, clamped to [0, 1]
/// budgetComponent = 1 − (remaining / total),  clamped to [0, 1]
///
/// The 70/30 weighting means a stalled agent dominates urgency, but
/// dwindling budget also raises the score even before a full stall.
/// </remarks>
public class UrgencyService
{
    /// <param name="stallSeconds">Seconds since the last stream event.</param>
    /// <param name="budgetRemaining">Messages left in the session budget.</param>
    /// <param name="sessionBudget">Total session budget (from settings).</param>
    /// <param name="stallThresholdSeconds">Stall threshold (from settings).</param>
    /// <returns>Urgency score in [0.0, 1.0]. Higher = more urgent.</returns>
    public double Compute(double stallSeconds, int budgetRemaining, int sessionBudget, int stallThresholdSeconds)
    {
        var stallComponent  = stallThresholdSeconds > 0
            ? Math.Clamp(stallSeconds / stallThresholdSeconds, 0.0, 1.0)
            : 0.0;

        var budgetComponent = sessionBudget > 0
            ? Math.Clamp(1.0 - (double)budgetRemaining / sessionBudget, 0.0, 1.0)
            : 0.0;

        return 0.7 * stallComponent + 0.3 * budgetComponent;
    }
}
