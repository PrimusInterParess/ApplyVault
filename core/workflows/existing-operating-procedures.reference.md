# Existing Operating Procedures — Reference

Neutral pattern catalog for detecting and adapting to project-local agent
workflows. Author names and repo URLs below are **illustrative examples only** —
never hardcode a specific skills library as required.

## Signal catalog (look for these)

Search incrementally. Stop when the operating model is clear.

### 1. Host agent instruction files

| Signal | Typical paths |
|---|---|
| Portable agent instructions | `AGENTS.md`, `agents.md` |
| Claude / Anthropic host file | `CLAUDE.md` |
| Copilot instructions | `.github/copilot-instructions.md` |
| Cursor rules | `.cursor/rules/**/*.mdc`, `.cursorrules` |
| Generic agent dirs | `.agents/`, `docs/agents/`, `agent-system/` |

**What to extract:** mandatory rules, allowed tools, doc locations, “always
read X before coding”, links to skills sections.

### 2. Skills / workflow packs

| Signal | Typical paths |
|---|---|
| Agent Skills standard | `**/SKILL.md` (often under `skills/`, `.cursor/skills/`, `.claude/skills/`, `.agents/skills/`) |
| Frontmatter metadata | YAML `name`, `description`, `disable-model-invocation` |
| User-invoked vs model-invoked | Skills that say “only when user types /…” vs “agent may reach for…” |
| Setup / bootstrap skill | Once-per-repo configurator for tracker, labels, doc layout |
| Router skill | “Which skill fits?” entrypoint |

**What to extract:** skill names, invocation mode, prerequisites, ordered flows
they compose, files they read/write.

### 3. Domain memory (shared language)

| Signal | Typical paths |
|---|---|
| Project glossary / context | `CONTEXT.md`, `CONTEXT-MAP.md`, `docs/context.md`, `docs/glossary.md` |
| Architecture Decision Records | `docs/adr/`, `adr/`, `docs/decisions/` |
| Domain consumer rules | `docs/agents/domain.md` or equivalent |

**What to extract:** where agents must read terminology; whether they must
update docs **inline** as decisions crystallize (vs batch at end).

### 4. Agent configuration docs

| Signal | Typical paths |
|---|---|
| Issue / work tracker binding | `docs/agents/issue-tracker.md`, tracker sections in `AGENTS.md` |
| Triage / label vocabulary | `docs/agents/triage-labels.md`, label maps in rules |
| Local markdown trackers | `.scratch/`, `docs/tickets/`, `TODO/` conventions |
| Skills index block | `## Agent skills` (or similar) inside `AGENTS.md` / `CLAUDE.md` |

**What to extract:** tracker type (GitHub, GitLab, Linear, Jira, local files,
other), label vocabulary, doc layout (single-context vs multi-context/monorepo).

### 5. Delivery workflow chain

Look for an ordered engineering loop already prescribed by the project, for
example (generic stages — names vary):

1. **Align / grill** — interview until decisions are resolved  
2. **Document** — write glossary terms and ADRs as answers land  
3. **Specify** — synthesize a durable spec / PRD into the tracker  
4. **Ticketize** — tracer-bullet tasks with explicit blockers  
5. **Implement** — often with TDD or vertical slices  
6. **Review** — standards axis + spec-conformance axis  
7. **Diagnose** — reproduce → minimise → fix → regression when broken  
8. **Handoff / multi-session** — compact state for the next agent session  

**What to extract:** the project's actual command or skill names for each
stage that exists; mark missing stages `NOT_PRESENT` (do not invent them).

### 6. Quality and design disciplines

| Signal | Meaning for the fleet |
|---|---|
| TDD / red-green-refactor skill | Implementation agents must follow it at agreed seams |
| Code-review skill | QA / Architect closeout must run project review criteria |
| Architecture-improvement skill | Periodic design hygiene is part of operate loop |
| Diagnose / debug skill | Prefer project's debug loop over ad-hoc fixing |
| Prototype skill | Design questions may require throwaway prototypes first |

## Neutral example: composable engineering skills pack

The following is a **genericized** illustration of a real public pattern
(skills libraries that install `SKILL.md` workflows into a repo). Use it only
to recognize *shape*, not as a required dependency.

**Shape to recognize:**

1. **Once-per-repo setup** asks: issue tracker, triage labels, where domain
   docs live — then writes small config files agents read later.
2. **Host file edit rule:** prefer editing the existing `CLAUDE.md` *or*
   `AGENTS.md`; do not create a second competing host file.
3. **Domain memory:** a root context/glossary file plus ADR directory;
   grilling/planning skills update them **as decisions happen**.
4. **Composable chain:** router → clarify-with-docs → to-spec → to-tickets →
   implement (with TDD) → code-review; plus diagnose and architecture-improve
   as side loops.
5. **Invocation split:** user-invoked orchestrators may call model-invoked
   discipline skills, but not other user-invoked orchestrators.
6. **Tracker abstraction:** same skills work with GitHub/GitLab CLI, Linear,
   Jira, or local markdown — configuration lives in-repo.

If a repository shows this shape (regardless of author), adaptation mode is
usually `FOLLOW` or `BRIDGE`, not `NONE`.

## Inventory table template

Use in the specification:

| ID | Path | Type | Purpose | Invocation | Confidence |
|---|---|---|---|---|---|
| PROC-001 | `AGENTS.md` | host-instructions | Agent entry rules | always-on | HIGH |
| PROC-002 | `skills/.../SKILL.md` | skill | … | user / model | HIGH |
| PROC-003 | `CONTEXT.md` | domain-memory | Shared language | read+update | HIGH |
| PROC-004 | `docs/adr/` | adr | Decision records | read+update | MEDIUM |

Types: `host-instructions` | `skill` | `command` | `rule` | `domain-memory` |
`adr` | `agent-config` | `tracker-binding` | `prompt-pack` | `other`

## Mapping Architect phases ↔ project chain

When generating `protocols/execution-workflow.md`, align rather than duplicate:

| Architect concern | Prefer project step when present |
|---|---|
| Discovery / clarification | Align / grill / domain-modeling skills |
| Spec approval | Project “to-spec” (or equivalent) + Architect approval gate |
| Task breakdown | Project “to-tickets” / tracer bullets |
| Implementation | Project implement + TDD skills |
| Validation | Project code-review / QA skills + Architect quality gates |
| Defects | Project diagnose skill |
| Multi-session continue | Project handoff / wayfinding skill |

If both systems define the same phase, record a `BRIDGE` rule:

- **Owner:** which system is authoritative for that phase  
- **Input:** what Architect must pass in  
- **Output:** what must exist before the next phase  

## Conflict resolution examples

| Conflict | Default resolution |
|---|---|
| Project forbids implementing without its clarify skill | Architect `/operate` must run or require that skill first |
| Project writes tickets to local markdown; Architect wants GitHub issues | Follow project tracker binding |
| Project updates `CONTEXT.md` inline; Architect only has `shared-context.yaml` | Agents update project domain memory; mirror summaries into shared context |
| Project has TDD skill; user rules say “no automatic tests” | Session/user rules win for *creating* tests; still follow project TDD when user asks to implement with tests |
| Two host files disagree | Prefer the file the setup skill or README designates; else ask once |

## Prompt-pack binding checklist

- [ ] `existing_operating_procedures.status` set  
- [ ] `adaptation_mode` set  
- [ ] Inventory paths listed (not full skill dumps)  
- [ ] Workflow chain recorded with project-native names  
- [ ] Every agent Operating Principles references adopted procedures  
- [ ] Execution workflow maps phases without dual ownership  
- [ ] Principal Architect required to re-resolve on `/operate`  
- [ ] No overwrite of project skills / CONTEXT / ADRs during SAVE  

## Anti-patterns

- Hardcoding one public skills author as the only supported pack  
- Copying entire third-party `SKILL.md` bodies into Architect agent prompts  
- Generating a second competing clarify→spec→ticket loop beside an active one  
- Ignoring `docs/agents/*` after detecting a setup skill ran  
- Treating “skills folder present” as proof the team uses every skill — only
  adopt skills referenced by host instructions, README, or setup output unless
  the user confirms the full pack is in force  
