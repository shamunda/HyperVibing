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

$watchdogHome  = Join-Path $HOME '.watchdog'
$inboxDir      = Join-Path $watchdogHome 'mailboxes' $projectName 'inbox'
$outboxDir     = Join-Path $watchdogHome 'mailboxes' $projectName 'outbox'
$deadLetterDir = Join-Path $watchdogHome 'mailboxes' $projectName 'dead-letter'

if (-not (Test-Path $inboxDir)) { exit 0 }

# Files are named <priority-prefix>-<guid>.json — alphabetic sort == priority order
$files = Get-ChildItem -Path $inboxDir -Filter '*.json' -ErrorAction SilentlyContinue |
         Sort-Object Name

if (-not $files) { exit 0 }

foreach ($file in $files) {
    try {
        $msg = Get-Content $file.FullName -Raw | ConvertFrom-Json
    } catch {
        continue
    }

    # Expire stale messages
    $expiresAt = [DateTimeOffset]::Parse($msg.expires_at)
    if ($expiresAt -lt [DateTimeOffset]::UtcNow) {
        try {
            if (-not (Test-Path $deadLetterDir)) { New-Item -ItemType Directory -Path $deadLetterDir -Force | Out-Null }
            Move-Item $file.FullName -Destination (Join-Path $deadLetterDir $file.Name) -Force
        } catch {}
        continue
    }

    # Ack: move to outbox
    try {
        if (-not (Test-Path $outboxDir)) { New-Item -ItemType Directory -Path $outboxDir -Force | Out-Null }
        Move-Item $file.FullName -Destination (Join-Path $outboxDir $file.Name) -Force
    } catch {}

    # Emit the injection — Claude Code shows this to the agent as a reminder
    $tone = if ($msg.tone) { "[$($msg.tone.ToUpper())] " } else { '' }
    Write-Output "[WATCHDOG] $tone$($msg.content)"
    exit 0
}

exit 0
