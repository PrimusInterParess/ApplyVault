Follow core/slash-commands.md for command `/operate`.

Read and execute: core/workflows/operate-agent-system.md

Operate an existing prompt pack through Architect delegation, handoffs, validation, and integration review.

If no concrete task follows `/operate`, display the workflow's A-E entry menu
and explain each option. Show a recommended option only when the saved project
state supports it.

Use Cursor's native `AskQuestion` tool so the options are clickable. Do not
render the A-E menu as plain Markdown unless the tool is unavailable.

For options C and D: the Architect must delegate specialist work via Cursor
`Task`, injecting each selected agent's prompt file plus the task-delegation
envelope. Do not implement other agents' owned work in the parent chat while
Task is available.
