Follow core/slash-commands.md for command `/generate-prompt-pack`.

Read and execute: core/workflows/generate-prompt-pack.md

Require an approved PROJECT REQUIREMENTS AND CURRENT-STATE SPECIFICATION. If missing, stop and say discovery approval is required. Generate the prompt pack only; do not implement the application.

Default to `SAVE` mode: create every prompt and governance artifact as a real,
separate file under `agent-system/`. Do not print the full pack into chat. After
writing, verify every manifest and agent-registry path exists before reporting
`PROMPT_PACK_READY`.
