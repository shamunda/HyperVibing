<#
.SYNOPSIS
    Watchdog PostToolUse Hook
.DESCRIPTION
    Appends a tool-use event to the project's Watchdog event stream (stream.jsonl).
    Performs fast inline safety rule checks and writes alert flag files for server pickup.
    Runs silently after every tool call in a monitored project.
    Any failure exits 0 — never disrupts the project agent.
#>
# ──────────────────────────────────────────────────────────────────
# Watchdog — Claude Code Supervisor Agent
# Author: Dennis "Shamunda" Ross
# Date:   March 2026
# MCP server that monitors and supervises Claude Code sessions.
# ──────────────────────────────────────────────────────────────────


$ErrorActionPreference = 'SilentlyContinue'

try {
    $raw = [Console]::In.ReadToEnd()
    $serverDll = Join-Path $HOME '.watchdog\server\Watchdog.Server.dll'
    if (-not (Test-Path $serverDll)) { exit 0 }
    $raw | & dotnet $serverDll hook-post-tool-use | Out-Null
} catch {
}

exit 0
