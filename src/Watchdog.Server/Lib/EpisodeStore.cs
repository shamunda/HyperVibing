// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class EpisodeStore
{
    public static void Append(string project, Episode episode) => WatchdogDataStore.Current.AppendEpisode(project, episode);

    /// <summary>Reads episodes from the last <paramref name="maxDays"/> day files, newest first.</summary>
    public static List<Episode> ReadRecent(string project, int maxDays = 7) =>
        WatchdogDataStore.Current.ReadRecentEpisodes(project, maxDays);

    /// <summary>
    /// Finds the episode by ID in any day file and rewrites that line with the
    /// updated outcome. JSONL files are small (bounded by session budget) so
    /// full rewrite is safe.
    /// </summary>
    public static bool PatchOutcome(string project, string episodeId,
        EpisodeOutcome outcome, DateTimeOffset reflectedAt) =>
        WatchdogDataStore.Current.PatchEpisodeOutcome(project, episodeId, outcome, reflectedAt);
}
