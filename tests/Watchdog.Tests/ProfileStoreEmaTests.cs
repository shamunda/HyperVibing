using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Tests;

public class ProfileStoreEmaTests
{
    [Fact]
    public void ApplyEma_Effective_IncreasesScore()
    {
        var profile = StrategyProfile.Empty();
        var updated = ProfileStore.ApplyEma(profile, "proj", "reminder", effective: true);
        Assert.True(updated.Global.Reminder > 0.5);
    }

    [Fact]
    public void ApplyEma_Ineffective_DecreasesScore()
    {
        var profile = StrategyProfile.Empty();
        var updated = ProfileStore.ApplyEma(profile, "proj", "reminder", effective: false);
        Assert.True(updated.Global.Reminder < 0.5);
    }

    [Fact]
    public void ApplyEma_UpdatesPerProject()
    {
        var profile = StrategyProfile.Empty();
        var updated = ProfileStore.ApplyEma(profile, "proj-a", "redirect", effective: true);

        Assert.True(updated.PerProject.ContainsKey("proj-a"));
        Assert.True(updated.PerProject["proj-a"].Redirect > 0.5);
    }

    [Fact]
    public void ApplyEma_DoesNotAffectOtherTones()
    {
        var profile = StrategyProfile.Empty();
        var updated = ProfileStore.ApplyEma(profile, "proj", "escalation", effective: true);

        Assert.Equal(0.5, updated.Global.Reminder);
        Assert.Equal(0.5, updated.Global.Redirect);
        Assert.True(updated.Global.Escalation > 0.5);
    }

    [Fact]
    public void ApplyEma_RepeatedIneffective_ApproachesZero()
    {
        var profile = StrategyProfile.Empty();
        for (var i = 0; i < 20; i++)
            profile = ProfileStore.ApplyEma(profile, "proj", "reminder", effective: false);

        Assert.True(profile.Global.Reminder < 0.05);
    }

    [Fact]
    public void ApplyEma_UnknownTone_NoChange()
    {
        var profile = StrategyProfile.Empty();
        var updated = ProfileStore.ApplyEma(profile, "proj", "unknown_tone", effective: true);
        Assert.Equal(profile.Global, updated.Global);
    }
}
