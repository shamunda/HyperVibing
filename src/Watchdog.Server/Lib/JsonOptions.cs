// ─────────────────────────────────────────────────────────────────
// Watchdog — Claude Code Supervisor Agent
// Author: Dennis "Shamunda" Ross
// Date:   March 2026
// MCP server that monitors and supervises Claude Code sessions.
// ─────────────────────────────────────────────────────────────────
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Watchdog.Server.Lib;

/// <summary>
/// Shared JSON serializer options. One definition, used everywhere.
/// Default: camelCase, case-insensitive (Web defaults).
/// Indented: same + pretty-printed for MCP tool responses.
/// </summary>
public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default =
        Create(writeIndented: false);

    public static readonly JsonSerializerOptions Indented =
        Create(writeIndented: true);

    private static JsonSerializerOptions Create(bool writeIndented)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            WriteIndented = writeIndented
        };
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
