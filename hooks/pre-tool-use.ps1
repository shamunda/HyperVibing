<#
.SYNOPSIS
    Watchdog PreToolUse Hook
.DESCRIPTION
    Reads the next pending message from the project's mailbox inbox and writes
    it to stdout so Claude Code injects it into the agent's context before the
    next tool call. Acks the message by moving it to the outbox.

    Output is plain text — Claude Code displays this to the agent as a
    system reminder before executing the tool.
    Exits 0 always (never blocks tools).
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
    $raw | & dotnet $serverDll hook-pre-tool-use
} catch {
}

exit 0
