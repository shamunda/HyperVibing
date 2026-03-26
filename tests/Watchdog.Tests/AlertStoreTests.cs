using Watchdog.Server.Lib;
using Watchdog.Server.Models;

namespace Watchdog.Tests;

public class AlertStoreTests : TestFixture
{
    [Fact]
    public void Append_And_ReadRecent_RoundTrips()
    {
        RegisterProject("proj");
        var alert = new SafetyAlert(
            AlertId:     "a1",
            Project:     "proj",
            ToolName:    "bash",
            RuleMatched: "destructive_command",
            Severity:    AlertSeverity.Critical,
            Detail:      "rm -rf matched",
            DetectedAt:  DateTimeOffset.UtcNow);

        AlertStore.Append("proj", alert);
        var result = AlertStore.ReadRecent("proj", maxDays: 1);

        Assert.Single(result);
        Assert.Equal("a1", result[0].AlertId);
        Assert.False(result[0].Acknowledged);
    }

    [Fact]
    public void Acknowledge_SetsFlag()
    {
        RegisterProject("proj");
        var alert = new SafetyAlert(
            AlertId:     "a2",
            Project:     "proj",
            ToolName:    "bash",
            RuleMatched: "force_push",
            Severity:    AlertSeverity.Critical,
            Detail:      "test",
            DetectedAt:  DateTimeOffset.UtcNow);

        AlertStore.Append("proj", alert);
        var acked = AlertStore.Acknowledge("proj", "a2");

        Assert.True(acked);
        var result = AlertStore.ReadUnacknowledged("proj");
        Assert.Empty(result);
    }

    [Fact]
    public void ReadUnacknowledged_FiltersAcknowledged()
    {
        RegisterProject("proj");
        AlertStore.Append("proj", new SafetyAlert("a3", "proj", "bash", "test", AlertSeverity.Warning, "d", DateTimeOffset.UtcNow));
        AlertStore.Append("proj", new SafetyAlert("a4", "proj", "bash", "test", AlertSeverity.Warning, "d", DateTimeOffset.UtcNow));
        AlertStore.Acknowledge("proj", "a3");

        var unacked = AlertStore.ReadUnacknowledged("proj");
        Assert.Single(unacked);
        Assert.Equal("a4", unacked[0].AlertId);
    }
}
