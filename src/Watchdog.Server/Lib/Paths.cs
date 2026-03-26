// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
namespace Watchdog.Server.Lib;

public static class Paths
{
    private static string _home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".watchdog");

    public static string Home               => _home;
    public static string Config             => Path.Combine(_home, "config");
    public static string Settings           => Path.Combine(_home, "config", "settings.json");
    public static string Projects           => Path.Combine(_home, "config", "projects.json");
    public static string Lock               => Path.Combine(_home, "relay.lock");
    public static string Hooks              => Path.Combine(_home, "hooks");
    public static string Profile            => Path.Combine(_home, "config", "profile.json");
    public static string PendingReflections => Path.Combine(_home, "config", "pending-reflections.json");
    public static string Patterns           => Path.Combine(_home, "config", "patterns.json");

    public static string Stream     (string project)             => Path.Combine(_home, "streams",  project, "stream.jsonl");
    public static string StreamDir  (string project)             => Path.Combine(_home, "streams",  project);
    public static string Episodes   (string project)             => Path.Combine(_home, "episodes", project);
    public static string EpisodeFile(string project, DateOnly d) => Path.Combine(_home, "episodes", project, $"{d:yyyy-MM-dd}.jsonl");

    public static string AlertDir (string project)              => Path.Combine(_home, "alerts", project);
    public static string AlertFile(string project, DateOnly d)  => Path.Combine(_home, "alerts", project, $"{d:yyyy-MM-dd}.jsonl");

    public static string JobDir (string project) => Path.Combine(_home, "jobs", project);
    public static string JobFile(string project) => Path.Combine(_home, "jobs", project, "jobs.jsonl");

    /// <summary>Redirect all paths to a custom root — used by tests to isolate I/O.</summary>
    internal static void SetRoot(string root) => _home = root;

    /// <summary>Reset to the default home directory.</summary>
    internal static void ResetRoot() =>
        _home = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".watchdog");

    public static class Mailbox
    {
        public static string Inbox     (string project) => Path.Combine(_home, "mailboxes", project, "inbox");
        public static string Outbox    (string project) => Path.Combine(_home, "mailboxes", project, "outbox");
        public static string DeadLetter(string project) => Path.Combine(_home, "mailboxes", project, "dead-letter");
    }
}
