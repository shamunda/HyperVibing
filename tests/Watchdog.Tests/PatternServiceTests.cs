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

public class PatternServiceTests : TestFixture
{
    private readonly PatternService _sut = new();

    [Fact]
    public void Crystallize_InsufficientSamples_ReturnsEmpty()
    {
        RegisterProject("proj");
        // No episodes at all
        var patterns = _sut.Crystallize("proj");
        Assert.Empty(patterns);
    }

    [Fact]
    public void Crystallize_EnoughSamples_HighDelta_ReturnsPattern()
    {
        RegisterProject("proj");

        // Write 6 episodes: all effective for "stall" trigger + "reminder" tone
        // This should create a delta since EMA starts at 0.5 but empirical rate is 1.0 (delta = +0.5)
        for (var i = 0; i < 6; i++)
        {
            var episode = new Episode(
                EpisodeId:           Guid.NewGuid().ToString("N"),
                Project:             "proj",
                NudgedAt:            DateTimeOffset.UtcNow.AddMinutes(-i),
                Trigger:             new EpisodeTrigger("stall", 0.6, 90, 3),
                DeliberationSummary: "Test",
                Action:              new EpisodeAction("reminder", $"msg-{i}", "test nudge"),
                Outcome:             new EpisodeOutcome(true, true, 5),
                ReflectedAt:         DateTimeOffset.UtcNow);
            EpisodeStore.Append("proj", episode);
        }

        var patterns = _sut.Crystallize("proj");
        Assert.NotEmpty(patterns);
        Assert.All(patterns, p =>
        {
            Assert.Equal("proj", p.Project);
            Assert.True(Math.Abs(p.Delta) >= 0.20);
        });
    }

    [Fact]
    public void GetPatterns_ReturnsPersistedPatterns()
    {
        RegisterProject("proj");
        var pattern = new CrystallizedPattern(
            PatternId:   "p1",
            Project:     "proj",
            TriggerType: "stall",
            Tone:        "reminder",
            SampleSize:  5,
            SuccessRate: 0.8,
            CurrentEma:  0.5,
            Delta:       0.3,
            DiscoveredAt: DateTimeOffset.UtcNow,
            Description: "Test pattern");

        PatternStore.Append(pattern);
        var result = _sut.GetPatterns("proj");

        Assert.Single(result);
        Assert.Equal("p1", result[0].PatternId);
    }
}
