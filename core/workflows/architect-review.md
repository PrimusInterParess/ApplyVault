# Architect Review

On-demand code review for the current branch versus a **user-required** base
ref. Ships with The Architect library as `/architect-review`.

This workflow **extends the library command surface**. It does not require a
local `agent-system/` pack to run (library default reviewer). When a project
fleet includes a reviewer agent, prefer that agent.

## Mission

1. Diff `HEAD` (current branch) against an explicit base branch/ref.
2. Produce a structured, high-signal review (intent, correctness, architecture,
   security, tests, error handling — not pure lint/format).
3. Optionally enrich from a **user-provided** ticket/issue.
4. Optionally prepare GitHub/GitLab PR/MR comments — only when instructed —
   after discussing what to post, showing a draft, and getting confirmation.
5. Post via `gh` (GitHub) or `glab` (GitLab) when available; never store tokens
   in the library.

## When to use

- User runs `/architect-review` or equivalent phrases (see
  [../slash-commands.md](../slash-commands.md)).
- User asks for an Architect-managed branch/PR review with optional comment
  publishing.

## Non-goals (v1)

- CI/webhook auto-review bots
- Guessing the base branch
- Auto-detecting PR/MR for publish without an explicit target
- Requiring a ticket
- Posting without draft confirmation
- Bare command name `/review` (collision risk — use `/architect-review`)

## Inputs

| Input | Required? | Notes |
|---|---|---|
| Base branch/ref | **Yes** | e.g. `main`, `origin/develop`, commit SHA. If missing → ask once; do not guess. |
| Ticket/issue key or URL | No | Include only when the user provides it. |
| Comment mode | No | Off by default (report-only). On only when the user instructs posting. |
| PR/MR URL or number (+ host if ambiguous) | Only in comment mode | Ask once if comment mode is on and target is missing. |
| Uncommitted working tree | No | **Exclude** unless the user explicitly asks to include it (PD-001). |

## Reviewer resolution

1. If `agent-system/` exists and registry/ownership names a reviewer such as
   `code-review-engineer` (type REVIEWER) with a prompt file → use that prompt
   (project override).
2. Else use the library default:
   [../templates/code-review-engineer.md](../templates/code-review-engineer.md).
3. In Cursor (and hosts with isolated subagents): run the reviewer via **Task**
   with the reviewer prompt + a filled review delegation envelope. Parent chat
   orchestrates only (same pattern as `/operate` delegation).
4. If Task is unavailable, the parent may perform the review using the same
   prompt and output contract — still no VCS publish without the publish steps
   below.

## Execution steps

### 1. Parse invocation

- Extract base ref from the user message (e.g. `/architect-review main`).
- Detect comment intent (“leave comments”, “post to PR”, `--comment`, etc.).
- Detect optional ticket URL/key.
- If base is missing → ask once and stop until provided.

### 2. Collect change evidence (read-only git)

Run from the repository root (adjust if the app root differs):

1. Confirm clean intent: current branch name and `HEAD`.
2. Resolve base ref; fail clearly if unknown.
3. Diff range: prefer `git diff <base>...HEAD` (three-dot / merge-base aware).
4. List changed files: `git diff --name-status <base>...HEAD`.
5. Do **not** include unstaged/uncommitted changes unless the user asked.
6. If the diff is huge, summarize by file and note truncation; still cover
   high-risk areas (auth, data, secrets, public APIs).

Never invent file contents. Do not claim tests ran unless executed.

### 3. Optional ticket context

If the user provided a ticket/issue:

- Fetch or read what the host allows (e.g. `gh issue view`, Jira/Atlassian MCP,
  pasted text).
- Use it for intent / acceptance checks.
- If fetch fails, continue with diff-only and note the limitation.

If no ticket was provided, do not block; review from the diff alone.

### 4. Run the review

Apply the resolved reviewer prompt. Require findings with:

| Field | Purpose |
|---|---|
| `severity` | `blocker` \| `should-fix` \| `nit` \| `question` \| `praise` |
| `path` / hunk | Evidence location when known |
| `rationale` | Why it matters (AC, security, maintainability, etc.) |
| `confidence` | Prefer `question` when unsure — do not invent certainty |

**Ignore** pure formatting, import order, trailing whitespace, and other issues
owned by linters/formatters/CI.

**Focus** on: requirement/intent mismatch, correctness bugs, API/contract
breaks, architecture/coupling, security (secrets, injection, authz), missing
tests for new behavior, weak/absent error handling, obvious performance
footguns (e.g. N+1).

### 5. Report (always)

Return a structured report in chat (and optionally a handoff under
`agent-system/` only when a pack exists and the user wants it persisted):

1. Scope: base → HEAD, branch name, file count
2. Ticket context used or `none`
3. Findings grouped by severity
4. Summary recommendation: `REQUEST CHANGES` | `APPROVE WITH NITS` | `APPROVE` |
   `NEEDS DISCUSSION`
5. Publish status: `NOT_REQUESTED` | `AWAITING_USER` | `POSTED` | `FAILED`

Default publish status is `NOT_REQUESTED`.

### 6. Comment mode (only if instructed)

1. Require explicit PR/MR target (URL or number + GitHub/GitLab). Ask once if
   missing.
2. **Discuss** with the user what to post (e.g. blockers only /
   blockers+should-fix / all / custom subset). Do not assume a fixed filter.
3. Build a **draft** of proposed comments (prefer inline when line mapping is
   reliable; else general PR/MR comment — PD-002).
4. Show the draft; **wait for explicit confirmation** before any write to the
   host.
5. Publish:
   - GitHub: `gh` (e.g. review comments / PR comment APIs via CLI)
   - GitLab: `glab` equivalently
6. If CLI missing or unauthenticated: keep the review report; fail publish
   clearly with remediation (`gh auth login` / `glab auth login`). Do not
   invent that comments were posted.
7. Never post secrets found in the diff; keep those as `blocker` findings in
   the report instead.

### 7. Close

State final status honestly. Offer next steps (fix findings, re-run after
changes, or `/operate` if implementation work follows).

## Quality bar

- No lint-only noise
- No guessed base or PR target
- No publish without draft confirmation
- No secrets in comments or logs
- Provider-neutral until the publish step (`gh` / `glab`)

## Related

- Default reviewer: [../templates/code-review-engineer.md](../templates/code-review-engineer.md)
- Fleet recommendation: [generate-prompt-pack.md](generate-prompt-pack.md)
  (`code-review-engineer`)
- Delegation pattern: [operate-agent-system.md](operate-agent-system.md)
  (§ Delegation runtime)
