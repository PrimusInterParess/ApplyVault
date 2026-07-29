# Update Context Mapping (Non-Standard Locations)

Use this when you already ran discovery/scan (and maybe generated a prompt pack),
but later you moved/created the orchestration context files in a different place
or changed filenames/layout.

This workflow refreshes the mapping so `/operate` can reliably find:

- the approved project specification,
- governance/shared context and contract artifacts,
- agent prompt files,
- protocols needed for delegation/handoffs.

## Mission

Detect the current locations of agent-system context artifacts anywhere in the
repository (generic heuristics), resolve ambiguities, and write an index so the
Architect can use it.

## Primary behavior

1. Prefer an existing mapping file if present:
   - `agent-system/context-index.yaml`
2. Otherwise, try canonical paths:
   - `agent-system/`
3. Otherwise, scan the repository read-only for likely artifacts by filename:
   - project spec: `project-specification.md` or `*-specification*.md`
   - governance: `governance/shared-context*`, `contract-registry*`, `ownership-matrix*`
   - agent prompts: `agents/*.md`
   - protocols: `protocols/*`
4. If there are multiple candidates for a category, ask the user to pick which
   path to use (one question at a time).
5. Write the refreshed mapping to:
   `agent-system/context-index.yaml`

## Context index format

Write YAML with:

```yaml
generated_at: "<ISO-8601>"
source_priority:
  - canonical_if_exists
  - repo_scanned_fallback
  - user_provided_paths_if_ambiguous
artifacts:
  project_specification: "<path>"
  shared_context: "<path>"
  contract_registry: "<path>"
  ownership_matrix: "<path>"
  approval_gates: "<path>"
  quality_gates: "<path>"
  integration_policy: "<path>"
  conflict_resolution: "<path>"
  change_management: "<path>"
  risk_register: "<path>"
  decision_register: "<path>"
  agent_prompts_dir: "<path-to-agents-dir>"
  protocols_dir: "<path-to-protocols-dir>"
```

## Output

Return:

1. The selected artifact paths (from the refreshed index).
2. Whether any required artifact is missing.
3. The exact next command: `/operate`.

If required artifacts are missing, block and ask the user for the missing path(s).

