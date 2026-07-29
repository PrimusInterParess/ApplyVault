#Requires -Version 5.1
<#
.SYNOPSIS
  Update a previously installed The Architect library copy in a target project.

.DESCRIPTION
  Refreshes library-owned paths from this repo into -TargetPath.
  Never modifies agent-system/ (generated fleets stay intact).
  Prefer git submodule / clone+pull when possible; use this for Option B installs.

.EXAMPLE
  powershell -File scripts/update-into-project.ps1 -TargetPath "C:\path\to\your\app"
#>
param(
  [Parameter(Mandatory = $true)]
  [string]$TargetPath,
  [switch]$SkipCursorAdapters,
  [switch]$SkipCopilot,
  [switch]$SkipClaude,
  [switch]$SkipAgentsMd,
  [switch]$DryRun,
  # Skip app-root .architect/ stamp (also: ARCHITECT_NO_APP_STAMP=1 or
  # agent-system/architect-install.yaml write_app_root_stamp: false)
  [switch]$NoStamp
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Target = Resolve-Path -LiteralPath $TargetPath -ErrorAction Stop
$Target = $Target.Path

$versionFile = Join-Path $Root "VERSION"
$version = if (Test-Path -LiteralPath $versionFile) {
  (Get-Content -LiteralPath $versionFile -Raw).Trim()
} else {
  "unknown"
}

function Test-ShouldWriteAppStamp {
  if ($NoStamp) { return $false }
  if ($env:ARCHITECT_NO_APP_STAMP -eq "1") { return $false }
  $policy = Join-Path $Target "agent-system\architect-install.yaml"
  if (Test-Path -LiteralPath $policy) {
    $raw = Get-Content -LiteralPath $policy -Raw
    if ($raw -match '(?m)^\s*write_app_root_stamp\s*:\s*false\s*$') { return $false }
  }
  return $true
}

function Write-Stamp {
  if (-not (Test-ShouldWriteAppStamp)) {
    Write-Host "STAMP skipped (NoStamp / ARCHITECT_NO_APP_STAMP / write_app_root_stamp: false)"
    return
  }
  $stampDir = Join-Path $Target ".architect"
  $stamp = Join-Path $stampDir "library-version"
  if ($DryRun) {
    Write-Host "DRY-RUN would write: .architect/library-version = $version"
    return
  }
  New-Item -ItemType Directory -Force -Path $stampDir | Out-Null
  Set-Content -LiteralPath $stamp -Value $version -NoNewline
  Write-Host "STAMP: .architect/library-version = $version"
}

function Update-Tree {
  param(
    [string]$Relative,
    [switch]$Optional
  )
  $src = Join-Path $Root $Relative
  $dst = Join-Path $Target $Relative
  if (-not (Test-Path -LiteralPath $src)) {
    if ($Optional) { return }
    throw "Missing source: $Relative"
  }
  if ($DryRun) {
    $action = if (Test-Path -LiteralPath $dst) { "REPLACE" } else { "ADD" }
    Write-Host "DRY-RUN $action`: $Relative"
    return
  }
  $parent = Split-Path -Parent $dst
  if ($parent) { New-Item -ItemType Directory -Force -Path $parent | Out-Null }
  if (Test-Path -LiteralPath $src -PathType Container) {
    if (Test-Path -LiteralPath $dst) { Remove-Item -Recurse -Force -LiteralPath $dst }
    Copy-Item -Recurse -Force -LiteralPath $src -Destination $dst
  }
  else {
    Copy-Item -Force -LiteralPath $src -Destination $dst
  }
  Write-Host "UPDATED: $Relative"
}

Write-Host "Updating The Architect ($version) into: $Target"
Write-Host "Preserved: agent-system/ (not touched)"
Write-Host ""

Update-Tree "core"
Update-Tree "schemas"
Update-Tree "references/source-prompts"
Update-Tree "scripts"
Update-Tree "examples" -Optional
Update-Tree "INSTALL.md" -Optional
Update-Tree "VERSION" -Optional

if (-not $SkipAgentsMd) {
  Update-Tree "AGENTS.md"
}

if (-not $SkipCursorAdapters) {
  Update-Tree ".cursor\skills"
  Update-Tree ".cursor\commands"
  Update-Tree ".cursor\rules"
}

if (-not $SkipClaude) {
  $src = Join-Path $Root "adapters\claude-code\CLAUDE.md"
  $dst = Join-Path $Target "CLAUDE.md"
  if (Test-Path -LiteralPath $src) {
    if ($DryRun) {
      Write-Host "DRY-RUN REPLACE: CLAUDE.md"
    }
    else {
      Copy-Item -Force -LiteralPath $src -Destination $dst
      Write-Host "UPDATED: CLAUDE.md"
    }
  }
}

if (-not $SkipCopilot) {
  $src = Join-Path $Root "adapters\copilot\copilot-instructions.md"
  $dstDir = Join-Path $Target ".github"
  $dst = Join-Path $dstDir "copilot-instructions.md"
  if (Test-Path -LiteralPath $src) {
    if ($DryRun) {
      Write-Host "DRY-RUN REPLACE: .github/copilot-instructions.md"
    }
    else {
      New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
      Copy-Item -Force -LiteralPath $src -Destination $dst
      Write-Host "UPDATED: .github/copilot-instructions.md"
    }
  }
}

Write-Stamp

Write-Host ""
Write-Host "Update complete. Review git diff in the target project, then reload the IDE window if Cursor adapters changed."
Write-Host "Your generated fleet under agent-system/ was left unchanged."
Write-Host "Next: in the target project, run /upgrade-architect to regenerate agent docs from the approved spec."
