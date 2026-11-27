# ApplyVault C# practices

Apply alongside [the coding standards](../coding-standards.md). These are project
conventions for the ASP.NET Core backend; they do not require a new architecture
or a rewrite of existing code.

## Types and contracts

- **Must:** use the narrowest useful access level. Keep implementation types
  `internal` and members `private` unless a caller requires broader access.
- **Prefer:** sealed concrete implementation classes unless inheritance is an
  intentional extension mechanism. Use composition for reuse.
- **Prefer:** records for data contracts with value semantics and classes for
  modules with identity, lifecycle, or mutable state. Records and `init` do not
  make nested objects immutable. Retain EF-compatible entity models.
- **Must:** preserve nullable checking. Represent optional values deliberately;
  do not use `null!`, `default!`, or empty strings to conceal missing required
  initialization. Validate required external input.
- **Prefer:** enums for truly closed internal choices, and validated value types
  where identity or invariants need more protection than a primitive provides.
  Do not wrap every string or ID without a concrete benefit. Parsing an enum
  must reject undefined values when the domain does not allow them.
- **Must:** explicitly define enum serialization and persistence mappings before
  exposing an enum in a contract. Reordering members must not change stored or
  wire values. An external provider's open vocabulary is not automatically a
  closed internal enum.
- **Prefer:** `IReadOnlyList<T>` for ordered read-only results and
  `IReadOnlyCollection<T>` when ordering is irrelevant. Do not leak a mutable
  backing collection or a deferred `IQueryable` through an application interface.
  Materialize queries within the owning persistence operation.

## Constants and static code

- **Prefer:** narrowly scoped `const` for compile-time values whose meaning is
  stable. Use `static readonly` for runtime-created shared values. Public
  constants are embedded into consuming assemblies; consider that before making
  a value part of a reusable assembly's interface. See the
  [C# constant reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/const).
- **Must:** do not treat `readonly` as deep immutability. A `static readonly`
  dictionary or array can still be modified. Keep shared data immutable or
  protected from mutation, using immutable or frozen collections where suitable.
  See the [readonly reference](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/readonly).
- **Prefer:** static methods for deterministic transformations whose dependencies
  are explicit arguments, and static classes for cohesive immutable definitions
  or pure algorithms. Do not introduce an injected class solely to avoid static.
- **Must:** use injected instance modules for application operations that own
  external dependencies or lifecycle. Do not hide a database, HTTP client,
  current user, filesystem catalog load, or service locator in static state.
  Startup may load a catalog and supply it to operations explicitly.
- **Must:** keep request and user state out of statics. Shared caches need
  explicit ownership, tenant-aware keys, concurrency behavior, and expiration.
  A DI singleton also shares state and must satisfy those requirements.
- **Prefer:** supply time through `TimeProvider` when decisions depend on the
  clock. A pure rule can instead accept the current time as an argument.

## Member order and formatting

Use this order in new or substantially changed classes:

1. Constants and static fields.
2. Instance fields and injected dependencies.
3. Explicit constructors, when used. A primary constructor stays on the type.
4. Properties, events, and other exposed state.
5. Public operations and interface implementations.
6. Internal and protected operations, when present.
7. Private helpers, followed by small private nested types.

**Prefer:** order operations by the main use case and its related operations.
Keep overloads adjacent. Order helpers in the sequence the operation uses them,
with a shared helper after the helpers that call it. For a single-operation
module, this produces a straightforward top-to-bottom reading path.

**Must:** use one convention within a class. Do not alphabetize methods, sort
by static versus instance within each operation group, or add `#region` blocks
merely to hide a class that has several responsibilities. This ordering is a
project default; framework constraints or generated code may require exceptions.
Avoid reordering untouched classes in unrelated changes.

Use four-space indentation, braces on separate lines, braces for control-flow
bodies, explicit access modifiers, and one declaration per line. Use `_camelCase`
for new explicit private instance fields; keep primary-constructor parameters
camelCase and do not rename unrelated existing fields. Prefer `var` when the
type is evident locally; spell it out when that improves understanding.

## Operations and failures

- **Must:** await asynchronous work, propagate cancellation, and dispose owned
  disposable resources with `using` or `await using`. Do not dispose dependencies
  owned by the DI container. Avoid `async void` except framework event handlers.
- **Prefer:** `Task` over `ValueTask` unless measured behavior justifies the
  additional constraints. Do not add `Task.Run` around request I/O.
- **Must:** make expected validation or domain failures explicit using the
  feature's existing result or exception convention. Never use exception text
  as a branch condition. Preserve the public error contract and keep unexpected
  internal details out of responses. Use `throw;` when rethrowing an exception.
- **Must:** use structured logging templates rather than interpolated log
  strings, and observe configured data-redaction rules. Use explicit comparison
  semantics for identifiers, normally ordinal or ordinal-ignore-case according
  to their contract; reserve culture-aware comparison for human language.
- **Prefer:** named arguments for otherwise ambiguous booleans or repeated
  primitive parameters. Replace growing flag combinations with explicit
  operations or a typed request when they represent distinct behaviors.
