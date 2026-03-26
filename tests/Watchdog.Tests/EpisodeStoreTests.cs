// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Tests;

public class EpisodeStoreTests : TestFixture
{
    [Fact]
    public void Append_And_ReadRecent_RoundTrips()
    {
        RegisterProject("proj");
        var episode = new Episode(
            EpisodeId:           "ep-1",
            Project:             "proj",
            NudgedAt:            DateTimeOffset.UtcNow,
            Trigger:             new EpisodeTrigger("stall", 0.5, 60, 4),
            DeliberationSummary: "Test",
            Action:              new EpisodeAction("reminder", "msg-1", "hello"),
            Outcome:             null,
            ReflectedAt:         null);

        EpisodeStore.Append("proj", episode);
        var result = EpisodeStore.ReadRecent("proj", maxDays: 1);

        Assert.Single(result);
        Assert.Equal("ep-1", result[0].EpisodeId);
    }

    [Fact]
    public void PatchOutcome_UpdatesExistingEpisode()
    {
        RegisterProject("proj");
        var episode = new Episode(
            EpisodeId:           "ep-2",
            Project:             "proj",
            NudgedAt:            DateTimeOffset.UtcNow,
            Trigger:             new EpisodeTrigger("stall", 0.6, 90, 3),
            DeliberationSummary: "Test",
            Action:              new EpisodeAction("redirect", "msg-2", "nudge content"),
            Outcome:             null,
            ReflectedAt:         null);

        EpisodeStore.Append("proj", episode);

        var outcome = new EpisodeOutcome(ActivityResumed: true, Effective: true, CursorDelta: 3);
        var patched = EpisodeStore.PatchOutcome("proj", "ep-2", outcome, DateTimeOffset.UtcNow);

        Assert.True(patched);
        var result = EpisodeStore.ReadRecent("proj", maxDays: 1);
        Assert.NotNull(result[0].Outcome);
        Assert.True(result[0].Outcome!.Effective);
    }

    [Fact]
    public void ReadRecent_NoEpisodes_ReturnsEmpty()
    {
        RegisterProject("proj");
        var result = EpisodeStore.ReadRecent("proj", maxDays: 1);
        Assert.Empty(result);
    }
}
