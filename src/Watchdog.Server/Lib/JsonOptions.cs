using System.Text.Json;

namespace Watchdog.Server.Lib;

/// <summary>
/// Shared JSON serializer options. One definition, used everywhere.
/// Default: camelCase, case-insensitive (Web defaults).
/// Indented: same + pretty-printed for MCP tool responses.
/// </summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default =
        new(JsonSerializerDefaults.Web);

    public static readonly JsonSerializerOptions Indented =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };
}
