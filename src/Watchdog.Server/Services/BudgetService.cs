using Watchdog.Server.Lib;

namespace Watchdog.Server.Services;

/// <summary>
/// In-process nudge budget. Limits how many nudges can be sent per session
/// so the supervisor does not spam a stalled agent endlessly.
///
/// Initialized from <see cref="WatchdogSettings.SessionBudget"/> on first use.
/// Call <see cref="Reset"/> when a new Claude session is detected.
/// </summary>
public class BudgetService
{
    private readonly object _lock = new();
    private int _remaining;
    private bool _initialized;

    /// <summary>True when at least one nudge credit remains.</summary>
    public bool HasBudget
    {
        get
        {
            EnsureInitialized();
            lock (_lock) return _remaining > 0;
        }
    }

    /// <summary>Current number of nudges still available.</summary>
    public int Remaining
    {
        get
        {
            EnsureInitialized();
            lock (_lock) return _remaining;
        }
    }

    /// <summary>
    /// Attempts to consume one nudge credit.
    /// </summary>
    /// <returns><c>true</c> if a credit was available and consumed; <c>false</c> if budget is exhausted.</returns>
    public bool TryConsume()
    {
        EnsureInitialized();
        lock (_lock)
        {
            if (_remaining <= 0) return false;
            _remaining--;
            return true;
        }
    }

    /// <summary>Resets the budget to the configured session budget.</summary>
    public void Reset()
    {
        var budget = SettingsLoader.Get().SessionBudget;
        lock (_lock)
        {
            _remaining    = budget;
            _initialized  = true;
        }
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void EnsureInitialized()
    {
        lock (_lock)
        {
            if (!_initialized) Reset();
        }
    }
}
