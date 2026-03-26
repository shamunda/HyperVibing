// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;
using Watchdog.Server.Models;
using Watchdog.Server.Services;

namespace Watchdog.Tests;

public class BudgetServiceTests
{
    [Fact]
    public void HasBudget_Initially_True()
    {
        var sut = new BudgetService();
        // Default settings: SessionBudget = 5
        Assert.True(sut.HasBudget);
    }

    [Fact]
    public void TryConsume_DecrementsBudget()
    {
        var sut = new BudgetService();
        var initial = sut.Remaining;
        Assert.True(sut.TryConsume());
        Assert.Equal(initial - 1, sut.Remaining);
    }

    [Fact]
    public void TryConsume_WhenExhausted_ReturnsFalse()
    {
        var sut = new BudgetService();
        while (sut.TryConsume()) { } // exhaust budget
        Assert.False(sut.TryConsume());
        Assert.False(sut.HasBudget);
    }

    [Fact]
    public void Reset_RestoresBudget()
    {
        var sut = new BudgetService();
        while (sut.TryConsume()) { }
        Assert.False(sut.HasBudget);

        sut.Reset();
        Assert.True(sut.HasBudget);
        Assert.True(sut.Remaining > 0);
    }
}
