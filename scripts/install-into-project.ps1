#Requires -Version 5.1
param(
  [Parameter(Mandatory = $true)]
  [string]$TargetPath,
  [switch]$Force,
  [switch]$SkipCursorAdapters,
  [switch]$SkipCopilot,
  [switch]$SkipClaude,
  # Skip app-root .architect/ stamp (also: ARCHITECT_NO_APP_STAMP=1 or
  # agent-system/architect-install.yaml write_app_root_stamp: false)
  [switch]$NoStamp
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$Target = Resolve-Path -LiteralPath $TargetPath -ErrorAction SilentlyContinue
if (-not $Target) {
  New-Item -ItemType Directory -Force -Path $TargetPath | Out-Null
  $Target = Resolve-Path -LiteralPath $TargetPath
}
$Target = $Target.Path

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

function Copy-Tree {
  param([string]$Relative, [switch]$Optional)
  $src = Join-Path $Root $Relative
  $dst = Join-Path $Target $Relative
  if (-not (Test-Path -LiteralPath $src)) {
    if ($Optional) { return }
    throw "Missing source: $Relative"
  }
  if ((Test-Path -LiteralPath $dst) -and -not $Force) {
    Write-Host "SKIP (exists): $Relative"
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
  Write-Host "COPIED: $Relative"
}

Copy-Tree "AGENTS.md"
Copy-Tree "core"
Copy-Tree "schemas"
Copy-Tree "examples"
Copy-Tree "scripts"
Copy-Tree "references/source-prompts"
Copy-Tree "INSTALL.md" -Optional
Copy-Tree "VERSION" -Optional
Copy-Tree "core\slash-commands.md" -Optional

$versionFile = Join-Path $Root "VERSION"
$version = if (Test-Path -LiteralPath $versionFile) {
  (Get-Content -LiteralPath $versionFile -Raw).Trim()
} else {
  "unknown"
}
if (Test-ShouldWriteAppStamp) {
  $stampDir = Join-Path $Target ".architect"
  New-Item -ItemType Directory -Force -Path $stampDir | Out-Null
  Set-Content -LiteralPath (Join-Path $stampDir "library-version") -Value $version -NoNewline
  Write-Host "STAMP: .architect/library-version = $version"
}
else {
  Write-Host "STAMP skipped (NoStamp / ARCHITECT_NO_APP_STAMP / write_app_root_stamp: false)"
}

if (-not $SkipCursorAdapters) {
  Copy-Tree ".cursor\skills"
  Copy-Tree ".cursor\commands"
  Copy-Tree ".cursor\rules"
}

if (-not $SkipClaude) {
  $src = Join-Path $Root "adapters\claude-code\CLAUDE.md"
  $dst = Join-Path $Target "CLAUDE.md"
  if ((Test-Path -LiteralPath $dst) -and -not $Force) {
    Write-Host "SKIP (exists): CLAUDE.md"
  }
  elseif (Test-Path -LiteralPath $src) {
    Copy-Item -Force -LiteralPath $src -Destination $dst
    Write-Host "COPIED: CLAUDE.md"
  }
}

if (-not $SkipCopilot) {
  $src = Join-Path $Root "adapters\copilot\copilot-instructions.md"
  $dstDir = Join-Path $Target ".github"
  $dst = Join-Path $dstDir "copilot-instructions.md"
  if ((Test-Path -LiteralPath $dst) -and -not $Force) {
    Write-Host "SKIP (exists): .github/copilot-instructions.md"
  }
  elseif (Test-Path -LiteralPath $src) {
    New-Item -ItemType Directory -Force -Path $dstDir | Out-Null
    Copy-Item -Force -LiteralPath $src -Destination $dst
    Write-Host "COPIED: .github/copilot-instructions.md"
  }
}

Write-Host ""
Write-Host "Install complete into: $Target"
Write-Host "Next: open the project and run /architect or say 'Start agent system discovery'"
Write-Host "Later updates: powershell -File scripts/update-into-project.ps1 -TargetPath `"$Target`""
