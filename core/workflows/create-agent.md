# Create Agent

Create one specialized agent prompt for an existing or in-progress fleet and
**save** it into the prompt pack when authorized.

## Prerequisites

| Check | If missing |
|---|---|
| Approved `agent-system/project-specification.md` (`APPROVED` or `APPROVED WITH CHANGES`) | Stop; finish discovery/approval. Explicit CREATE_AGENT without an approved spec is allowed only as **PREVIEW** (chat output), not pack writes, unless the user records an override assumption. |
| Clear capability the agent must own | Ask once |
| Provider status: verified, required, preferred, proposed, undecided, or not applicable | Classify before specializing |
| Existing `agent-system/manifest.yaml` for this project | Stop before SAVE if another project's pack would be overwritten |

## Modes

- `SAVE` (default when prerequisites met and user authorizes pack changes): write
  agent + governance updates under `agent-system/`.
- `PREVIEW`: show copy-ready prompt and update blocks; write no pack files.

## Steps

1. Check whether an existing agent already owns the capability.
2. If overlap exists, recommend `EXTEND_FLEET` / ownership split instead of a duplicate.
3. Decide generic vs provider-specialized.
4. Inherit adopted project operating procedures from shared context /
   [existing-operating-procedures.md](existing-operating-procedures.md); do not
   introduce a conflicting workflow loop.
5. Generate a complete agent prompt with all 18 required sections (see
   [generate-prompt-pack.md](generate-prompt-pack.md)), including the
   **§6 / §11** style–patterns–skills–knowledge binding rules (paths only;
   no invented house style).
6. Produce registry / ownership / contract / handoff updates (Architect update
   block).
7. In `SAVE` mode:
   - Write `agents/<file>.md` (do not overwrite unrelated project packs).
   - Update `governance/agent-registry.yaml`, ownership matrix, contracts,
     Architect prompt as needed.
   - Record both **agent id** and **prompt_file** (see [../glossary.md](../glossary.md)).
   - Verify 18 section headings and non-empty files.
   - When PowerShell is available, run
     `powershell -NoProfile -File scripts/validate-agent-system.ps1`.
8. Recommend semantic version bump for the pack when ownership changes.

## Output

1. Necessity assessment
2. Mode (`SAVE` / `PREVIEW`) and files written or proposed
3. Generated agent prompt path or preview
4. Architect / registry update summary
5. Handoff table
6. Verification result
7. Next step (`/operate` or `/extend-fleet` if more agents needed)

Do not invent credentials, repo files, or test results.
