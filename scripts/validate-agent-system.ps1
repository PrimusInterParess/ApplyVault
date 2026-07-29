#Requires -Version 5.1
param(
  [string]$AgentSystemPath = "agent-system"
)

$ErrorActionPreference = "Stop"
$Root = Resolve-Path -LiteralPath $AgentSystemPath -ErrorAction SilentlyContinue
if (-not $Root) {
  Write-Host "FAIL: agent-system directory not found: $AgentSystemPath"
  exit 1
}
$Root = $Root.Path
$Errors = New-Object System.Collections.Generic.List[string]

function Add-Err([string]$Message) {
  $Errors.Add($Message) | Out-Null
}

$requiredFiles = @(
  "project-specification.md",
  "README.md",
  "manifest.yaml",
  "governance/shared-context.yaml",
  "governance/agent-registry.yaml",
  "governance/ownership-matrix.md",
  "governance/contract-registry.yaml",
  "governance/approval-gates.md",
  "governance/quality-gates.md",
  "governance/integration-policy.md",
  "governance/conflict-resolution.md",
  "governance/change-management.md",
  "governance/risk-register.yaml",
  "governance/decision-register.yaml",
  "protocols/task-delegation.yaml",
  "protocols/agent-handoff.yaml",
  "protocols/execution-workflow.md",
  "protocols/validation-report.yaml",
  "agents/00-principal-architect.md",
  "examples/project-invocation.md",
  "examples/delegation-examples.md",
  "examples/handoff-example.yaml"
)

foreach ($relative in $requiredFiles) {
  $path = Join-Path $Root $relative
  if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
    Add-Err "missing file: $relative"
    continue
  }
  if ((Get-Item -LiteralPath $path).Length -eq 0) {
    Add-Err "empty file: $relative"
  }
}

$specPath = Join-Path $Root "project-specification.md"
if (Test-Path -LiteralPath $specPath) {
  $spec = Get-Content -Raw -LiteralPath $specPath
  if ($spec -notmatch '(?m)^\*\*Status:\*\*\s+(APPROVED|APPROVED WITH CHANGES)') {
    Add-Err "project specification is not approved"
  }
}

$registryPath = Join-Path $Root "governance/agent-registry.yaml"
$agentPromptPaths = New-Object System.Collections.Generic.List[string]
if (Test-Path -LiteralPath $registryPath) {
  $registry = Get-Content -Raw -LiteralPath $registryPath
  $matches = [regex]::Matches(
    $registry,
    '(?m)^\s*prompt_file:\s*["'']?([^"''\r\n]+)["'']?\s*$'
  )
  foreach ($match in $matches) {
    $relative = $match.Groups[1].Value.Trim()
    $agentPromptPaths.Add($relative) | Out-Null
    $path = Join-Path $Root $relative
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
      Add-Err "registered agent prompt missing: $relative"
      continue
    }
    $content = Get-Content -Raw -LiteralPath $path
    if ([string]::IsNullOrWhiteSpace($content)) {
      Add-Err "registered agent prompt empty: $relative"
      continue
    }
    for ($section = 1; $section -le 18; $section++) {
      if ($content -notmatch "(?m)^#{1,6}\s+$section\.\s+") {
        Add-Err "$relative missing section heading $section"
      }
    }
  }
}

if ($agentPromptPaths.Count -eq 0) {
  Add-Err "agent registry contains no prompt_file entries"
}

if ($Errors.Count -gt 0) {
  Write-Host "FAIL:"
  foreach ($errorMessage in $Errors) {
    Write-Host "  - $errorMessage"
  }
  exit 1
}

Write-Host "OK: agent system has $($agentPromptPaths.Count) registered agents and all required files"
exit 0
