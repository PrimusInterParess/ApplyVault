# Claude Code / Claude project instructions

Follow The Architect portable library in this repository.

1. Treat `AGENTS.md` and `core/` as source of truth.
2. For slash commands listed in `core/slash-commands.md` — including
   `/architect`, `/discover`, `/brownfield`, `/hybrid`, `/generate-prompt-pack`,
   `/create-agent`, `/extend-fleet`, `/audit`, `/operate`, `/update-context`,
   `/upgrade-architect`, `/update-ownership`, `/architect-review` — load the matching workflow.
3. Default entry: `core/workflows/agent-system-builder.md`.
4. Provider-neutral. Capability before provider.
5. Discovery ends at `APPROVAL REQUIRED`.
6. After approval, prompt-pack generation runs in `SAVE` mode automatically.
7. After a library update: update files, then `/upgrade-architect`.
8. Do not invent secrets, files, tests, or deployments.
9. If a deep playbook under `references/` conflicts with `core/`, `core/` wins.

If this file was installed into another project via `scripts/install-into-project.*`, the same relative paths apply from that project root.
