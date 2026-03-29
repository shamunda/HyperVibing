<#
.SYNOPSIS
    Watchdog Installer
.DESCRIPTION
    Installs the Watchdog Claude Code supervisor agent.
    Run from the repository root: .\install.ps1

    What this does:
      1. Checks prerequisites (dotnet >= 9, claude CLI, pwsh)
      2. Builds and publishes the .NET MCP server to ~/.watchdog/server/
      3. Copies hooks to ~/.watchdog/hooks/
      4. Copies config defaults to ~/.watchdog/config/ (preserves existing)
      5. Registers the MCP server via "claude mcp add --scope user"
      6. Prints next steps
#>
# ──────────────────────────────────────────────────────────────────
# Watchdog — Claude Code Supervisor Agent
# Author: Dennis "Shamunda" Ross
# Date:   March 2026
# MCP server that monitors and supervises Claude Code sessions.
# ──────────────────────────────────────────────────────────────────

#Requires -Version 5.1

$ErrorActionPreference = 'Stop'

$WatchdogHome = Join-Path $HOME '.watchdog'
$ClaudeDir    = Join-Path $HOME '.claude'
$PkgRoot      = $PSScriptRoot

function Write-Step  ($msg) { Write-Host "  $msg" }
function Write-Ok    ($msg) { Write-Host "  [OK] $msg" -ForegroundColor Green }
function Write-Warn  ($msg) { Write-Host "  [WARN] $msg" -ForegroundColor Yellow }
function Write-Fail  ($msg) { Write-Host "  [FAIL] $msg" -ForegroundColor Red; exit 1 }
function Write-Hr          { Write-Host '' }

# ── Step 1: Prerequisites ──────────────────────────────────────────────────

function Test-Prerequisites {
    Write-Step 'Checking prerequisites...'

    # .NET 9+
    try {
        $dotnetVersion = (dotnet --version 2>&1).Trim()
        $major = [int]($dotnetVersion -split '\.')[0]
        if ($major -lt 9) { Write-Fail ".NET 9+ required. You have $dotnetVersion. Download: https://dotnet.microsoft.com/download" }
        Write-Ok ".NET $dotnetVersion"
    } catch {
        Write-Fail '.NET SDK not found. Download: https://dotnet.microsoft.com/download'
    }

    # Claude CLI
    if (-not (Get-Command 'claude' -ErrorAction SilentlyContinue)) {
        Write-Fail 'Claude Code CLI not found. Install: https://docs.anthropic.com/claude-code'
    }
    Write-Ok 'Claude Code CLI found'

    # pwsh (PowerShell Core — used for hooks)
    if (-not (Get-Command 'pwsh' -ErrorAction SilentlyContinue)) {
        Write-Warn 'pwsh (PowerShell Core) not found. Hooks will fall back to powershell.exe (Windows only).'
        return 'powershell'
    }
    Write-Ok 'pwsh (PowerShell Core) found'
    return 'pwsh'
}

# ── Step 2: Build and publish .NET server ─────────────────────────────────

function Publish-Server {
    Write-Step 'Building Watchdog MCP server...'

    $projectPath = Join-Path $PkgRoot 'src' 'Watchdog.Server'
    $outputPath  = Join-Path $WatchdogHome 'server'

    & dotnet publish $projectPath -c Release -o $outputPath --nologo 2>&1 | ForEach-Object {
        if ($_ -match 'error') { Write-Host "  $_" -ForegroundColor Red }
    }

    if ($LASTEXITCODE -ne 0) { Write-Fail 'Build failed. See output above.' }
    Write-Ok "MCP server published to $outputPath"
}

# ── Step 3: Copy hooks ─────────────────────────────────────────────────────

function Copy-Hooks ($psExe) {
    Write-Step 'Installing hook scripts...'

    $hooksSource = Join-Path $PkgRoot 'hooks'
    $hooksDest   = Join-Path $WatchdogHome 'hooks'

    New-Item -ItemType Directory -Path $hooksDest -Force | Out-Null
    Copy-Item "$hooksSource\*.ps1" -Destination $hooksDest -Force

    Write-Ok "Hooks installed to $hooksDest (using $psExe)"
}

# ── Step 3b: Install git hooks (pre-commit, pre-push) ──────────────────────

function Install-GitHooks {
    Write-Step 'Installing git safety hooks...'

    $gitDir = Join-Path $PkgRoot '.git'
    if (-not (Test-Path $gitDir)) {
        Write-Warn 'Not a git repository — skipping git hook install.'
        Write-Warn 'Run "git init" then re-run install.ps1 to enable secret scanning.'
        return
    }

    $gitHooksDir = Join-Path $gitDir 'hooks'
    New-Item -ItemType Directory -Path $gitHooksDir -Force | Out-Null

    $hooksSource = Join-Path $PkgRoot 'hooks'

    foreach ($hook in @('pre-commit', 'pre-push')) {
        $src = Join-Path $hooksSource $hook
        $dst = Join-Path $gitHooksDir $hook
        if (Test-Path $src) {
            Copy-Item $src -Destination $dst -Force
            Write-Ok "Git $hook hook installed"
        }
    }
}

# ── Step 4: Copy default config (preserve existing) ───────────────────────

function Copy-Config {
    Write-Step 'Setting up config...'

    $configDest = Join-Path $WatchdogHome 'config'
    New-Item -ItemType Directory -Path $configDest -Force | Out-Null

    $settingsDest = Join-Path $configDest 'settings.json'
    if (-not (Test-Path $settingsDest)) {
        Copy-Item (Join-Path $PkgRoot 'config' 'settings.json') -Destination $settingsDest
        Write-Ok 'Default settings.json written'
    } else {
        Write-Step '  Existing settings.json preserved'
    }

    $projectsDest = Join-Path $configDest 'projects.json'
    if (-not (Test-Path $projectsDest)) {
        Copy-Item (Join-Path $PkgRoot 'config' 'projects.json') -Destination $projectsDest
    }

    # Create runtime directories
    foreach ($dir in @('backups')) {
        New-Item -ItemType Directory -Path (Join-Path $WatchdogHome $dir) -Force | Out-Null
    }
}

# ── Step 5: Register MCP server ────────────────────────────────────────────

function Register-McpServer {
    Write-Step 'Registering Watchdog MCP server (user scope)...'

    $serverDll = Join-Path $WatchdogHome 'server' 'Watchdog.Server.dll'

    # Check if already registered (claude mcp list parses cleanly)
    $existing = & claude mcp list 2>&1 | Select-String 'watchdog'
    if ($existing) {
        Write-Step '  Already registered — updating entry...'
        & claude mcp remove watchdog 2>&1 | Out-Null
    }

    & claude mcp add --transport stdio --scope user watchdog -- dotnet $serverDll
    if ($LASTEXITCODE -ne 0) { Write-Fail 'Failed to register MCP server. Is Claude Code CLI installed?' }
    Write-Ok 'MCP server registered in ~/.claude.json (user scope)'
}

# ── Step 6: Next steps ─────────────────────────────────────────────────────

function Write-NextSteps {
    Write-Hr
    Write-Host '  Watchdog installed successfully.' -ForegroundColor Cyan
    Write-Hr
    Write-Host '  Next steps:'
    Write-Hr
    Write-Host '  1. Start a Claude session anywhere: claude --dangerously-skip-permissions'
    Write-Host '  2. Register your project and install hooks (say this to Claude):'
    Write-Hr
    Write-Host '       /watchdog.add'
    Write-Hr
    Write-Host '  3. Exit that Claude session.'
    Write-Host '  4. Open a new Claude session inside your project directory.'
    Write-Host '  5. Begin working — Watchdog monitoring is active from session start.'
    Write-Hr
    Write-Host "  Watchdog home : $WatchdogHome"
    Write-Host "  MCP config    : $env:USERPROFILE\.claude.json (user scope)"
    Write-Hr
}

# ── Main ───────────────────────────────────────────────────────────────────

Write-Hr
Write-Host '  Watchdog — Claude Code Supervisor Agent' -ForegroundColor Cyan
Write-Host '  ─────────────────────────────────────────'
Write-Hr

$psExe = Test-Prerequisites
Write-Hr
Publish-Server
Write-Hr
Copy-Hooks $psExe
Write-Hr
Install-GitHooks
Write-Hr
Copy-Config
Write-Hr
Register-McpServer
Write-NextSteps
