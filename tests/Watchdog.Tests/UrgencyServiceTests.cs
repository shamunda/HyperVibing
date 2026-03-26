using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class UrgencyServiceTests
{
    private readonly UrgencyService _sut = new();

    [Fact]
    public void Compute_NoStall_ReturnsZero()
    {
        var result = _sut.Compute(stallSeconds: 0, budgetRemaining: 5, sessionBudget: 5, stallThresholdSeconds: 60);
        Assert.Equal(0.0, result);
    }

    [Fact]
    public void Compute_FullStall_FullBudget_Returns_0_7()
    {
        // stallComponent = 1.0 * 0.7 = 0.7, budgetComponent = 0.0 * 0.3 = 0.0
        var result = _sut.Compute(stallSeconds: 120, budgetRemaining: 5, sessionBudget: 5, stallThresholdSeconds: 60);
        Assert.Equal(0.7, result, precision: 2);
    }

    [Fact]
    public void Compute_NoStall_NoBudget_Returns_0_3()
    {
        // stallComponent = 0.0, budgetComponent = 1.0 * 0.3 = 0.3
        var result = _sut.Compute(stallSeconds: 0, budgetRemaining: 0, sessionBudget: 5, stallThresholdSeconds: 60);
        Assert.Equal(0.3, result, precision: 2);
    }

    [Fact]
    public void Compute_FullStall_NoBudget_Returns_1_0()
    {
        var result = _sut.Compute(stallSeconds: 120, budgetRemaining: 0, sessionBudget: 5, stallThresholdSeconds: 60);
        Assert.Equal(1.0, result, precision: 2);
    }

    [Fact]
    public void Compute_HalfStall_HalfBudget_Returns_0_5()
    {
        // stallComponent = 0.5 * 0.7 = 0.35, budgetComponent = 0.5 * 0.3 = 0.15
        var result = _sut.Compute(stallSeconds: 30, budgetRemaining: 3, sessionBudget: 6, stallThresholdSeconds: 60);
        Assert.Equal(0.5, result, precision: 2);
    }

    [Fact]
    public void Compute_ZeroThreshold_StallComponentIsZero()
    {
        var result = _sut.Compute(stallSeconds: 100, budgetRemaining: 0, sessionBudget: 5, stallThresholdSeconds: 0);
        Assert.Equal(0.3, result, precision: 2);
    }

    [Fact]
    public void Compute_ZeroBudget_BudgetComponentIsZero()
    {
        var result = _sut.Compute(stallSeconds: 60, budgetRemaining: 0, sessionBudget: 0, stallThresholdSeconds: 60);
        Assert.Equal(0.7, result, precision: 2);
    }
}
