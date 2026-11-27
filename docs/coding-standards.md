# ApplyVault coding standards

ApplyVault code should let a maintainer find a feature, understand its rules, and
change its behavior without tracing unrelated parts of the application. These
standards govern new and changed application code in `api/`, `frontend/`,
`extension/`, and `shared/`. They also provide the standard for code review.
The Architect library under `core/` and generated fleets under `agent-system/`
retain their own workflows.

**Must** means a requirement. **Prefer** means a default that may be set aside
when a concrete tradeoff makes the alternative clearer. Explain exceptions in
the change summary. Existing violations do not require a repository-wide cleanup;
address those that affect the requested change and record substantial follow-up
work separately.

## Before implementation

1. Read `AGENTS.md`, `CONTEXT.md`, relevant `docs/adr/` decisions, and the task's
   acceptance criteria. Use the domain vocabulary in names, tests, and UI copy.
   Read [C# practices](standards/csharp.md) for backend work and
   [TypeScript practices](standards/typescript.md) for dashboard or extension work.
2. Trace the active route or entry point through its callers, state, persistence,
   and tests. Check that similar code is active before using it as an example.
3. Identify the feature that owns the change and the rules it must preserve.
   For a change spanning several layers, state the intended responsibility of
   each layer in a short implementation update.
4. Extend the existing feature unless a separate responsibility requires a new
   module. Do not introduce frameworks or dependencies for work the existing
   stack can reasonably handle.

## Module design and readability

A module has an interface and an implementation. Its interface includes inputs,
outputs, invariants, error behavior, and ordering constraints. Prefer a small
interface that hides substantial implementation detail and gives callers a clear
operation, such as importing a CV or saving a job.

- **Must:** give each module one coherent responsibility. Keep business rules
  out of HTTP controllers, Angular templates, and browser event handlers.
- **Must:** keep each business rule in one authoritative place. Both browser
  clients may provide validation feedback, but the API enforces persisted rules.
  Use `shared/cv-section-catalog/cv-section-catalog.json` for the section schema;
  do not edit generated copies as a separate source of truth.
- **Prefer:** explicit control flow, early returns, descriptive domain names,
  and functions whose inputs and results are understandable without reading
  their callers. Avoid boolean switches that select unrelated operations.
- **Must:** name helpers for their behavior, such as `normalizeJobSearchFilters`.
  Do not add generic dumping grounds named `helpers`, `common`, or `manager`.
- **Prefer:** pure functions for calculations, normalization, comparisons, and
  state transitions. Supply external dependencies through the existing injection
  mechanism or function parameters.
- **Prefer:** extract a module when it owns a rule, hides an external dependency,
  or groups logic that changes together. Avoid pass-through layers, generic base
  classes, and factories added solely for possible future requirements.
- **Must:** keep feature-specific code within its feature. Move code to shared
  locations when actual callers need the same behavior and meaning.
- **Must:** avoid circular dependencies and imports into another feature's
  implementation details. Expose a deliberate interface for collaboration.
- **Prefer:** one primary concept per file; closely related private types may
  stay together. Comments explain invariants and decisions that code cannot
  express clearly. Remove dead code and stale comments in the changed area.

Review a production source file above roughly 300 lines or a function above
roughly 50 lines for mixed responsibilities. These are review prompts, not hard
limits. Split by responsibility when it improves understanding; explain retaining
a large file if the change substantially extends it. Do not fragment cohesive
code or compress formatting to meet a line count. Generated files and declarative
fixtures are outside these size prompts.

For example, a CV builder page should coordinate editing and display. Section
normalization belongs in a tested module; saving belongs in data access; export
layout belongs in the export implementation. Moving all page methods into a
generic helper file does not establish useful responsibilities.

## Named values and domain types

**Must:** give values that control business behavior or a shared contract a
named, typed owner. This includes workflow states, provider identifiers, error
codes, storage keys, and meaningful limits. Do not scatter unchecked string or
numeric literals through branches and call sites.

Choose the representation by meaning:

| Value | Representation |
| --- | --- |
| Closed set of domain choices | C# enum or validated value type; TypeScript literal union derived from a canonical definition when runtime values are needed |
| Stable wire or persistence identifier | Named definition and explicit serialization or mapping at the owning integration |
| Related choices with labels or metadata | Feature-owned typed catalog or registry |
| Business limit or default | Descriptively named constant or policy, with units explicit |
| Setting that varies by deployment | Typed configuration, validated at startup where appropriate |
| Value whose validity has meaningful rules | A validated domain type when it removes repeated checks or prevents invalid construction |
| Ordinary local UI copy, log template, or self-explanatory literal | Keep local unless reuse, localization, or contract semantics require an owner |

**Must:** centralize within the smallest owning feature or integration. Do not
create an application-wide `Constants` class or file containing unrelated values.
Equal text does not imply equal meaning: a display label and a stored identifier
may need to evolve independently. Framework enum values and named protocol
constants should be reused where available; use `nameof` for code-symbol names,
not for externally stable identifiers that must survive a rename.

**Must:** preserve existing serialized and stored values when introducing types.
Reject or explicitly handle unknown external values at the adapter; do not
silently substitute a valid business value. A C# definition and a TypeScript
definition do not share a type system: use an existing schema, generated contract,
or contract verification where consumers need to agree. Do not duplicate a
catalog already owned under `shared/`.

For example, repeated untyped comparisons against a provider ID need a provider
definition. A timeout should express its unit, such as `TimeSpan.FromSeconds(...)`
or `REQUEST_TIMEOUT_MS`. A single button label can stay in its template. Typing
an identifier or naming a limit should make its meaning clearer to the caller.

## TypeScript and Angular

Keep compatibility with the versions in the package manifests. Apply the
feature-oriented organization described in the [Angular style guide](https://angular.dev/style-guide),
while retaining this repository's existing file suffixes and feature subfolders.

| Location | Responsibility |
| --- | --- |
| `features/<feature>/pages/` | Route composition, user actions, and connecting UI to feature state |
| `features/<feature>/presentation/` or existing `components/` | Focused rendering and interaction through explicit inputs and outputs |
| `features/<feature>/data-access/` | HTTP adapters and feature state orchestration |
| `features/<feature>/models/` | Feature contracts and types |
| `features/<feature>/utils/` | Named pure transformations and rules |
| `src/app/core/` | Application infrastructure such as auth, configuration, and sanitization |
| `src/app/shared/` | UI or layout used across features |

- **Must:** keep strict TypeScript and Angular template checking enabled. Use
  concrete types; receive untrusted values as `unknown` and validate them before
  use. Do not introduce `any`, non-null assertions, or unchecked casts to silence
  errors. Narrow types or validate the actual invariant.
- **Must:** keep HTTP calls in data-access modules. Presentation modules must
  not fetch data or own persistence rules. A page uses feature state orchestration
  when loading, saving, and concurrency require coordination.
- **Prefer:** the existing standalone Angular style, `inject`, and signals for
  synchronous UI state. Use computed values for derived state. Use RxJS for
  asynchronous composition where it simplifies cancellation and coordination.
  Do not introduce a second state library for one feature.
- **Must:** give each state value one owner. Do not maintain independently
  mutable copies of derived state. Release subscriptions, event listeners, and
  timers when their owning view is destroyed.
- **Must:** prevent stale asynchronous results from overwriting newer state.
  Represent loading, empty, success, and failure behavior explicitly. Keep
  templates free of business algorithms and expensive repeated computation.
- **Must:** reuse existing styles and UI controls. Use semantic elements,
  accessible labels, keyboard interaction, and visible focus for new controls.
  Retain the dashboard style described in `md/angular-ui-style-guidance.md`.
- **Prefer:** Angular kebab-case filenames with the existing `.component.ts`,
  `.service.ts`, and `.model.ts` suffixes. Colocate `.spec.ts` files with the
  behavior they test. Follow the surrounding file's quote and import style.

The routed `job-search` feature owns current search work. The README identifies
`features/eures-jobs` as legacy; do not copy fixes into inactive implementations
unless their use is established by tracing callers.

## ASP.NET Core and persistence

- **Must:** keep controllers focused on HTTP validation, resolving the signed-in
  user, invoking an application operation, and translating its result. Put
  business rules and multi-step orchestration in the owning service module.
- **Must:** enforce ownership on every operation involving user data, including
  reads, updates, deletes, and background work. Resolve identity server-side;
  never trust a user ID supplied by a browser. Follow ADR-0010.
- **Must:** keep EF entities inside server implementation details and map them
  to deliberate request and response contracts. Validate external input at its
  entry point and enforce domain invariants before persistence.
- **Prefer:** feature folders within the existing `Services/`, `Models/`, and
  `Controllers/` layout. Keep composition in startup registration modules.
  Avoid a folder or namespace migration during unrelated feature work.
- **Prefer:** direct EF Core usage within the owning persistence implementation.
  Do not add a generic repository or an interface for every class. Introduce an
  interface where it hides a meaningful capability or external dependency.
- **Must:** use asynchronous I/O where available, pass cancellation tokens
  through the operation, and avoid `.Result`, `.Wait()`, or unowned fire-and-forget
  tasks. These follow [ASP.NET Core performance guidance](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/best-practices?view=aspnetcore-10.0).
- **Must:** preserve cancellation rather than swallowing it in fallback catches.
  Catch failures where recovery or translation is meaningful. Do not return an
  empty success result after an unexpected failure.
- **Must:** bound externally supplied collection sizes and expensive operations.
  Keep database queries scoped to the required user and data. Use a transaction
  when a business operation requires several writes to succeed together.
- **Must:** add an EF migration for persisted schema changes. Preserve already
  applied migrations; include compatibility and data migration considerations
  when changing stored representations or published contracts.
- **Prefer:** PascalCase C# types and public members, camelCase locals and
  parameters, file-scoped namespaces, and `Async` suffixes for task-returning
  operations. Keep nullable reference checking enabled.

## Chrome extension

- **Must:** keep DOM extraction in `content/`, Chrome and HTTP integration in
  `infrastructure/`, orchestration in `application/`, and popup interaction in
  `popup/`. Browser entry points wire these responsibilities together.
- **Must:** place new application-facing dependency contracts in
  `application/ports/` or appropriate shared contracts. Application rules must
  not import concrete Chrome or HTTP implementations. Existing exceptions can
  be migrated when those dependency contracts change.
- **Must:** keep cross-context messages typed in `shared/contracts/`; validate
  messages and page-derived values at runtime before acting on them. Treat
  captured text as untrusted input.
- **Prefer:** pure extraction functions operating on supplied DOM or text so
  extraction can be tested without a live Chrome session. Match existing
  camelCase filenames rather than renaming extension files for Angular style.

## Errors and external integrations

- **Must:** distinguish an empty result from a failed operation. Provide an
  actionable user message and enough structured diagnostic context to debug
  the failure. Never expose credentials, tokens, CV content, or raw provider
  responses containing personal data in logs or browser error messages.
- **Must:** keep credentials server-side where the architecture requires it.
  Use the existing auth, HTTP configuration, and HTML sanitization modules.
  Do not bypass sanitization to display captured or model-generated content.
- **Must:** bound provider timeouts and retries; retry only when replay is safe.
  Validate AI output against the expected schema before saving or displaying
  it, and preserve existing user review and approval behavior.
- **Must:** keep configuration in the existing configuration mechanism. New
  settings need safe example values and an explanation of required behavior.

## Tests and delivery checks

Test observable behavior through the module's interface. A refactor that
preserves behavior should normally preserve its tests. Prefer real pure logic
and fake external adapters over mocks of every internal call.

- **Must:** add or update meaningful tests for changed business rules and
  regressions. Cover relevant success, failure, invalid input, and cancellation
  or stale-result cases. Do not add tests that only mirror implementation.
- **Must:** include HTTP integration coverage for changes to authorization,
  tenancy, or persisted request behavior. Use relational verification when
  correctness depends on transactions, constraints, or SQL behavior; EF's
  in-memory provider cannot prove those properties.
- **Must:** use deterministic fixtures and controlled external dependencies.
  Tests must not depend on live AI, personal accounts, or production data.
- **Must:** run the applicable checks below. Report commands and actual results;
  identify environmental blockers and distinguish them from test failures.
  Never claim a command passed when it was not run.

Run commands from the stated directory. Install locked JavaScript dependencies
with `npm ci` when needed. API commands require the .NET 10 SDK; frontend
`test:ci` requires a compatible Chrome installation.

| Area and working directory | Checks |
| --- | --- |
| API, repository root | `dotnet build api/ApplyVault.Api/ApplyVault.Api.sln --configuration Release`; `dotnet test api/ApplyVault.Api.Tests/ApplyVault.Api.Tests.csproj --configuration Release`; `dotnet test api/ApplyVault.Api.IntegrationTests/ApplyVault.Api.IntegrationTests.csproj --configuration Release` |
| Dashboard, `frontend/applyvault-jobs-ui/` | `npm run build`; `npm run test:ci` |
| Extension, `extension/` | `npm run typecheck`; `npm test`; `npm run build` |
| Shared contracts or catalog | Checks for every affected consumer, including generated-copy consistency |
| Documentation only | Check links, paths, and `git diff --check` |

Use focused tests during development, then run the affected area's full checks
before handing off code. For UI behavior, also inspect the affected flow at
desktop and phone widths when applicable. Avoid unrelated formatting churn.

## Review and enforcement

Before finishing an application change, review the diff against these questions:

- Does it implement the requested behavior and preserve relevant ADR decisions?
- Can a maintainer find the owning feature and follow the operation locally?
- Does each module own a coherent responsibility with explicit dependencies?
- Are rules and mutable state owned once, with external input validated?
- Are ownership, failure, and concurrency behavior covered where applicable?
- Do the relevant checks pass, and are exceptions explained?

The root `AGENTS.md`, `CLAUDE.md`, and Copilot instructions link here so agents
receive the same guidance. Use these rules as standards sources for `/code-review`.
Review findings should cite the relevant section and explain the consequence.
Distinguish a requirement breach from a design preference or size review prompt.

Enforcement has three parts:

| Mechanism | What it enforces |
| --- | --- |
| `.editorconfig` | Editor defaults for whitespace and indentation; it does not run a formatter |
| Existing strict compilers and GitHub CI | API build and tests, dashboard build and tests, extension typecheck, tests, and build |
| Agent and human review | Responsibility, locality, useful abstractions, naming, and test relevance |

ESLint, Prettier, C# style diagnostics, and automated import restrictions are
future enforcement work. When adding them, establish a baseline and adopt rules
in focused changes so existing debt does not obscure new violations. Formatting
tools alone do not establish module responsibilities or guarantee compliance.

Improve existing code incrementally: add behavior coverage where needed, extract
one coherent responsibility behind a clear interface, switch its callers, and
remove the replaced implementation. Keep behavior changes separate enough to
review. Record architecture decisions in `docs/adr/` when a new decision changes
dependencies, persistence, or a shared contract; routine implementation choices
do not need an ADR.
