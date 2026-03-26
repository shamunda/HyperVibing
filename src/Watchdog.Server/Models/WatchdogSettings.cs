namespace Watchdog.Server.Models;

public record WatchdogSettings
{
    public int      StallThresholdSeconds   { get; init; } = 60;
    public double   UrgencyThreshold        { get; init; } = 0.40;
    public int      SessionBudget           { get; init; } = 5;
    public int      ReflectionWindowMinutes { get; init; } = 5;
    public int      RelayBatonMaxAgeHours   { get; init; } = 8;
    public string[] SafetyRules             { get; init; } = ["destructive_command", "secret_in_code", "force_push"];
    public string   DefaultPreset           { get; init; } = "solo-coder";
    public bool     AutoLoopEnabled         { get; init; } = true;
    public int      AutoLoopIntervalSeconds { get; init; } = 30;
}
