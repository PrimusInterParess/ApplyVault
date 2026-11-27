# ApplyVault TypeScript practices

Apply alongside [the coding standards](../coding-standards.md) for both the
Angular dashboard and Chrome extension. Angular-specific lifecycle and state
rules remain in the main standards.

## Domain values and type safety

- **Prefer:** literal unions for closed choices. When runtime values are also
  needed, derive the union from one `as const` tuple or object rather than
  maintaining independent lists. Use `satisfies` to check a catalog's shape
  without unnecessarily widening its values.
- **Must:** treat `as const` as literal inference, not validation or runtime
  freezing. Ordinary `as SomeType` assertions do not validate external input.
  Validate HTTP, storage, and message data before trusting its type.
- **Prefer:** discriminated unions for mutually exclusive operation states
  instead of combinations of booleans and optional values that allow invalid
  states. Handle all variants; use an exhaustive `never` check where omissions
  would be a bug. This follows the
  [TypeScript narrowing guidance](https://www.typescriptlang.org/docs/handbook/2/narrowing.html).
- **Prefer:** interfaces for object contracts and type aliases for unions and
  derived types. Either can describe objects; keep consistency within a feature
  rather than converting existing declarations for style alone.
- **Must:** give exported functions and public nontrivial methods explicit
  return types. Let obvious locals infer their types. Preserve precise types
  rather than using `object`, `Function`, `any`, or broad dictionaries to avoid
  defining a real contract.
- **Prefer:** `readonly` properties and `ReadonlyArray<T>` or `readonly T[]`
  where callers should not mutate data. Protect shared values from mutation;
  `const` prevents reassignment of a binding, not changes inside its object.

For example, a runtime set and its type can share a definition:

```typescript
export const JOB_SEARCH_SOURCES = ['eures', 'jobnet'] as const;
export type JobSearchSource = (typeof JOB_SEARCH_SOURCES)[number];
```

A typed comparison such as `source === 'jobnet'` is allowed: the compiler checks
that literal against the closed union. Use a named member when it adds domain
meaning or reuse. Do not force every union literal into a second constants
object or introduce enums solely to remove quotes.

## Functions and dependencies

- **Prefer:** exported functions for pure transformations and classes for
  modules with injected dependencies, state, or lifecycle. Do not add static
  utility classes where a module-level function expresses the operation.
- **Must:** avoid mutable module-level state for user data, requests, or feature
  drafts. Keep state with its explicit owner; do not hide dependencies in a
  globally accessible singleton or service locator.
- **Must:** handle every promise deliberately: await it, return it, or explicitly
  own its failure and cancellation behavior. `void somePromise()` alone does
  not handle a rejection. Use `unknown` for caught failures and narrow before
  accessing error properties.
- **Prefer:** `const` by default, `let` for reassignment, and no `var`. Use strict
  equality and intentional null checks. Use `??` when only a missing value
  should trigger a fallback; `||` also replaces `0`, `false`, and empty text.
- **Prefer:** options objects when multiple primitive arguments would otherwise
  be ambiguous. Do not add generics unless they express an actual relationship
  between inputs and outputs.
- **Must:** use `import type` for imports used only as types. Keep module
  dependencies acyclic; do not introduce barrel exports that obscure ownership
  or cause cycles.

## Member and declaration order

Use this order in new or substantially changed classes:

1. Static constants, when a class genuinely needs them.
2. Injected dependencies and foundational private state.
3. Angular inputs, outputs, public state, and computed values in dependency order.
4. Constructor, when required, and lifecycle hooks in lifecycle order.
5. Public user actions and other public operations, grouped by use case.
6. Private helpers in the sequence their callers use them.

**Must:** initialize values in dependency order. A computed value or field
initializer must not read a field before that field is initialized. Keep
overloads adjacent. A private field may precede public state when that public
state depends on it; visibility sorting must not break initialization.

For function modules, put imports first, then foundational constants and types,
exported operations, and private helpers. Move foundational declarations earlier
when initialization requires it. Order operations by use case, not alphabetically.
Avoid reordering untouched code in unrelated changes.

Use two-space indentation, semicolons, and the surrounding file's quote and
import layout. Use camelCase for functions and variables, PascalCase for types
and classes, and UPPER_SNAKE_CASE for exported constant definitions. Local
`const` variables remain camelCase. Keep named domain operations easy to search;
avoid anonymous exports and nested ternaries when they obscure control flow.
