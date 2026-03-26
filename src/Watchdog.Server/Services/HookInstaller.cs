using System.Text.Json;
using Watchdog.Server.Lib;

namespace Watchdog.Server.Services;

/// <summary>
/// Business logic for installing Watchdog hooks into a project's Claude Code config.
/// Writes hook command entries into .claude/settings.json.
/// </summary>
public class HookInstaller
{
    private const string PsExecutable = "pwsh";

    public string Install(string projectName)
    {
        var project = ProjectRegistry.Get(projectName)
            ?? throw new InvalidOperationException($"Project \"{projectName}\" is not registered.");

        var preHook  = Path.Combine(Paths.Hooks, "pre-tool-use.ps1");
        var postHook = Path.Combine(Paths.Hooks, "post-tool-use.ps1");

        if (!File.Exists(preHook) || !File.Exists(postHook))
            throw new FileNotFoundException($"Hook scripts not found in {Paths.Hooks}. Run the Watchdog installer first.");

        var settingsPath = Path.Combine(project.Path, ".claude", "settings.json");
        Directory.CreateDirectory(Path.GetDirectoryName(settingsPath)!);

        var existing = LoadExistingSettings(settingsPath);
        existing["hooks"] = BuildHooksConfig(preHook, postHook);

        File.WriteAllText(settingsPath, JsonSerializer.Serialize(existing, JsonOptions.Indented));
        ProjectRegistry.MarkHooksInstalled(projectName);

        return settingsPath;
    }

    private static Dictionary<string, object> LoadExistingSettings(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object>>(
                File.ReadAllText(path), JsonOptions.Default) ?? [];
        }
        catch { return []; }
    }

    private static object BuildHooksConfig(string preHook, string postHook) => new
    {
        PreToolUse = new[]
        {
            new { matcher = ".*", hooks = new[] { new { type = "command", command = $"{PsExecutable} -ExecutionPolicy Bypass -File \"{preHook}\"" } } }
        },
        PostToolUse = new[]
        {
            new { matcher = ".*", hooks = new[] { new { type = "command", command = $"{PsExecutable} -ExecutionPolicy Bypass -File \"{postHook}\"" } } }
        },
    };
}
