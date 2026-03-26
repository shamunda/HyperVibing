<#
.SYNOPSIS
    Watchdog PostToolUse Hook
.DESCRIPTION
    Appends a tool-use event to the project's Watchdog event stream (stream.jsonl).
    Performs fast inline safety rule checks and writes alert flag files for server pickup.
    Runs silently after every tool call in a monitored project.
    Any failure exits 0 — never disrupts the project agent.
#>

$ErrorActionPreference = 'SilentlyContinue'

try {
    $raw = [Console]::In.ReadToEnd()
    $event = $raw | ConvertFrom-Json
} catch {
    exit 0
}

$cwd = if ($event.cwd) { $event.cwd } else { (Get-Location).Path }

# Resolve project name from .watchdog identity file
$identityFile = Join-Path $cwd '.watchdog'
if (-not (Test-Path $identityFile)) { exit 0 }

$projectName = (Get-Content $identityFile -Raw -ErrorAction SilentlyContinue).Trim()
if (-not $projectName) { exit 0 }

# Build the stream event record
$streamEvent = [ordered]@{
    ts            = (Get-Date -AsUTC).ToString('o')
    session_id    = if ($event.session_id)  { $event.session_id }  else { 'unknown' }
    project       = $projectName
    tool_name     = if ($event.tool_name)   { $event.tool_name }   else { 'unknown' }
    tool_input    = if ($event.tool_input)  { $event.tool_input }  else { @{} }
    tool_response = if ($event.tool_response) { $event.tool_response } else { @{} }
    outcome       = if ($event.tool_response.error) { 'error' } else { 'success' }
}

# Append to ~/.watchdog/streams/<project>/stream.jsonl
$watchdogHome = Join-Path $HOME '.watchdog'
$streamDir    = Join-Path $watchdogHome 'streams' $projectName
$streamFile   = Join-Path $streamDir 'stream.jsonl'

try {
    if (-not (Test-Path $streamDir)) { New-Item -ItemType Directory -Path $streamDir -Force | Out-Null }
    $line = $streamEvent | ConvertTo-Json -Compress -Depth 10
    Add-Content -Path $streamFile -Value $line -Encoding UTF8
} catch {
    # Silently fail — never disrupt the project agent
}

# ── Safety rule quick-check (fast path — regex on tool name + input) ──────
# Writes a flag file so the MCP server picks up alerts on next tick.
# Full evaluation happens server-side; this is a fast pre-filter.

$toolName  = $streamEvent.tool_name
$inputText = ($streamEvent.tool_input | ConvertTo-Json -Compress -Depth 5) 2>$null

$safetyRules = @(
    @{ Name = 'destructive_command'; Pattern = 'rm\s+-rf\s|DROP\s+TABLE|DELETE\s+FROM|git\s+clean\s+-[a-z]*f|rmdir\s+/s|Remove-Item.*-Recurse.*-Force' }
    @{ Name = 'force_push';          Pattern = 'git\s+push\s+.*--force|git\s+push\s+-f\b|git\s+reset\s+--hard\s+origin' }
    @{ Name = 'secret_in_code';      Pattern = '(?i)(api[_-]?key|api[_-]?secret|password|token|secret)\s*[:=]\s*[\"''][^\"'']{8,}' }
)

foreach ($rule in $safetyRules) {
    if ($inputText -match $rule.Pattern) {
        $alertDir = Join-Path $watchdogHome 'alerts' $projectName
        try {
            if (-not (Test-Path $alertDir)) { New-Item -ItemType Directory -Path $alertDir -Force | Out-Null }
            $flag = [ordered]@{
                rule      = $rule.Name
                tool_name = $toolName
                ts        = (Get-Date -AsUTC).ToString('o')
            }
            $flagFile = Join-Path $alertDir ("flag-" + [guid]::NewGuid().ToString('N') + ".json")
            $flag | ConvertTo-Json -Compress | Set-Content $flagFile -Encoding UTF8
        } catch {}
        break  # one flag per event is sufficient
    }
}

exit 0
