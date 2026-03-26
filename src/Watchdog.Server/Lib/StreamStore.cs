using System.Text.Json;
using Watchdog.Server.Models;

namespace Watchdog.Server.Lib;

public static class StreamStore
{
    public static StreamSlice ReadSince(string project, int cursor, int limit = 100)
    {
        var path = Paths.Stream(project);
        if (!File.Exists(path)) return new StreamSlice([], cursor);

        var lines = File.ReadAllLines(path)
                        .Skip(cursor)
                        .Take(limit)
                        .ToList();

        var events = lines
            .Select(line =>
            {
                try { return JsonSerializer.Deserialize<StreamEvent>(line, JsonOptions.Default); }
                catch { return null; }
            })
            .Where(e => e is not null)
            .Cast<StreamEvent>()
            .ToList();

        return new StreamSlice(events, cursor + events.Count);
    }

    public static int LineCount(string project)
    {
        var path = Paths.Stream(project);
        return File.Exists(path) ? File.ReadAllLines(path).Length : 0;
    }

    public static StreamEvent? LastEvent(string project)
    {
        var count = LineCount(project);
        if (count == 0) return null;
        return ReadSince(project, count - 1, 1).Events.FirstOrDefault();
    }

    public static void Append(string project, StreamEvent ev)
    {
        Directory.CreateDirectory(Paths.StreamDir(project));
        File.AppendAllText(Paths.Stream(project),
            JsonSerializer.Serialize(ev, JsonOptions.Default) + "\n");
    }
}
