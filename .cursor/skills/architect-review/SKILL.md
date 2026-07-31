---
name: architect-review
description: >-
  Runs an on-demand Architect code review of the current branch versus a
  user-required base ref, with optional ticket context and optional GitHub/GitLab
  PR/MR comment publishing after draft confirmation. Use when the user runs
  /architect-review or asks for an Architect-managed PR/diff review.
disable-model-invocation: true
---

# Cursor adapter: architect-review

**Read and follow:** [core/workflows/architect-review.md](../../../core/workflows/architect-review.md)

## Cursor-specific

1. Prefer **AskQuestion** for base-ref prompts, publish severity choice, and
   draft confirmation when the tool is available.
2. Run the resolved reviewer via **Task** when available (inject reviewer prompt
   + review scope). Parent session orchestrates and owns any `gh`/`glab` publish
   step after explicit user confirmation.
3. Do not post PR/MR comments unless the user instructed comment mode and
   confirmed the draft.
