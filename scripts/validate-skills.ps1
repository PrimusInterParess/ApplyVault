#Requires -Version 5.1
<#
.SYNOPSIS
  Validate The Architect library: core workflows, slash commands, Cursor adapters, schemas.
#>
$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $PSScriptRoot
$SkillsDir = Join-Path $Root ".cursor\skills"
$CommandsDir = Join-Path $Root ".cursor\commands"
$RulesDir = Join-Path $Root ".cursor\rules"
$CoreDir = Join-Path $Root "core\workflows"
$SchemasDir = Join-Path $Root "schemas"
$MaxSkillLines = 500
$Errors = New-Object System.Collections.Generic.List[string]

function Add-Err([string]$Message) { $Errors.Add($Message) | Out-Null }

function Test-MarkdownLinks {
  param([string]$FilePath, [string]$Label)
  $text = Get-Content -Raw -LiteralPath $FilePath
  $matches = [regex]::Matches($text, '\[[^\]]*\]\(([^)]+)\)')
  foreach ($m in $matches) {
    $link = $m.Groups[1].Value
    if ($link -match '^(https?://|mailto:|#)') { continue }
    $target = [System.IO.Path]::GetFullPath((Join-Path (Split-Path -Parent $FilePath) $link))
    if (-not (Test-Path -LiteralPath $target)) {
      Add-Err "$Label`: broken link '$link'"
    }
  }
}

$requiredCore = @(
  "agent-system-builder.md",
  "project-discovery.md",
  "project-discovery.reference.md",
  "brownfield-research.md",
  "brownfield-research.reference.md",
  "existing-operating-procedures.md",
  "existing-operating-procedures.reference.md",
  "generate-prompt-pack.md",
  "create-agent.md",
  "extend-fleet.md",
  "audit-prompts.md",
  "operate-agent-system.md",
  "upgrade-architect.md",
  "update-context-mapping.md",
  "architect-review.md",
  "update-ownership.md"
)

$requiredCommands = @(
  "architect.md", "discover.md", "brownfield.md", "hybrid.md",
  "generate-prompt-pack.md", "create-agent.md", "extend-fleet.md",
  "audit.md", "operate.md", "update-context.md",
  "architect-review.md", "update-ownership.md"
)

# Upgrade is skill-only so the slash menu can show a YAML description.
$requiredSkills = @(
  "upgrade-architect"
)

if (-not (Test-Path -LiteralPath $CoreDir)) {
  Add-Err "core/workflows missing"
}
else {
  foreach ($name in $requiredCore) {
    $path = Join-Path $CoreDir $name
    if (-not (Test-Path -LiteralPath $path)) {
      Add-Err "missing core workflow: $name"
      continue
    }
    Test-MarkdownLinks -FilePath $path -Label "core/$name"
  }
}

$slashMap = Join-Path $Root "core\slash-commands.md"
if (-not (Test-Path -LiteralPath $slashMap)) {
  Add-Err "core/slash-commands.md missing"
}
else {
  Test-MarkdownLinks -FilePath $slashMap -Label "core/slash-commands.md"
}

if (-not (Test-Path -LiteralPath $CommandsDir)) {
  Add-Err ".cursor/commands missing"
}
else {
  foreach ($name in $requiredCommands) {
    $path = Join-Path $CommandsDir $name
    if (-not (Test-Path -LiteralPath $path)) {
      Add-Err "missing Cursor command: $name"
    }
  }
}

$requiredRules = @(
  "operate-native-subagents.mdc"
)
if (-not (Test-Path -LiteralPath $RulesDir)) {
  Add-Err ".cursor/rules missing"
}
else {
  foreach ($name in $requiredRules) {
    $path = Join-Path $RulesDir $name
    if (-not (Test-Path -LiteralPath $path)) {
      Add-Err "missing Cursor rule: $name"
    }
  }
}

if (-not (Test-Path -LiteralPath $SkillsDir)) {
  Add-Err ".cursor/skills missing"
}
else {
  $skillDirs = Get-ChildItem -LiteralPath $SkillsDir -Directory | Sort-Object Name
  $foundSkills = @($skillDirs | ForEach-Object { $_.Name })
  foreach ($name in $requiredSkills) {
    if ($foundSkills -notcontains $name) {
      Add-Err "missing Cursor skill: $name"
    }
  }
  foreach ($dir in $skillDirs) {
    $skillMd = Join-Path $dir.FullName "SKILL.md"
    if (-not (Test-Path -LiteralPath $skillMd)) {
      Add-Err "$($dir.Name): SKILL.md missing"
      continue
    }
    $lines = (Get-Content -LiteralPath $skillMd).Count
    if ($lines -gt $MaxSkillLines) {
      Add-Err "$($dir.Name): SKILL.md has $lines lines (max $MaxSkillLines)"
    }
    $raw = Get-Content -Raw -LiteralPath $skillMd
    if ($raw -notmatch '(?s)^---\r?\n(.*?)\r?\n---\r?\n') {
      Add-Err "$($dir.Name): missing YAML frontmatter"
    }
    else {
      $fm = $Matches[1]
      if ($fm -notmatch '(?m)^name:\s*(.+)$') {
        Add-Err "$($dir.Name): frontmatter missing name"
      }
      else {
        $name = $Matches[1].Trim().Trim('"').Trim("'")
        if ($name -ne $dir.Name) {
          Add-Err "$($dir.Name): name '$name' does not match directory"
        }
      }
      if ($fm -notmatch '(?m)^description:') {
        Add-Err "$($dir.Name): frontmatter missing description"
      }
    }
    if ($raw -notmatch 'core/workflows/') {
      Add-Err "$($dir.Name): Cursor adapter must link to core/workflows/"
    }
    Test-MarkdownLinks -FilePath $skillMd -Label $dir.Name
  }
}

$expected = @(
  "project-context.schema.json",
  "capability-matrix.schema.json",
  "discovery-ledger.schema.json",
  "requirements-spec.schema.json",
  "architect-install.schema.json",
  "manifest.schema.json",
  "agent-registry.schema.json",
  "task-delegation.schema.json",
  "agent-handoff.schema.json",
  "validation-report.schema.json",
  "context-index.schema.json"
)
if (-not (Test-Path -LiteralPath $SchemasDir)) {
  Add-Err "schemas/ directory missing"
}
else {
  foreach ($name in $expected) {
    $path = Join-Path $SchemasDir $name
    if (-not (Test-Path -LiteralPath $path)) {
      Add-Err "missing schema: $name"
      continue
    }
    try { $null = Get-Content -Raw -LiteralPath $path | ConvertFrom-Json }
    catch { Add-Err "${name}: invalid JSON ($($_.Exception.Message))" }
  }
}

foreach ($required in @("AGENTS.md", "LICENSE", "INSTALL.md", "CONTRIBUTING.md")) {
  if (-not (Test-Path -LiteralPath (Join-Path $Root $required))) {
    Add-Err "$required missing"
  }
}

if ($Errors.Count -gt 0) {
  Write-Host "FAIL:"
  foreach ($e in $Errors) { Write-Host "  - $e" }
  exit 1
}

$skillCount = @(Get-ChildItem -LiteralPath $SkillsDir -Directory).Count
$cmdCount = @(Get-ChildItem -LiteralPath $CommandsDir -Filter "*.md").Count
Write-Host "OK: core workflows, $skillCount skills, $cmdCount slash commands, schemas valid"
exit 0
