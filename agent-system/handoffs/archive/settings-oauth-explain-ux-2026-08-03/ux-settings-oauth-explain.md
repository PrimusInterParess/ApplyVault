# Settings — OAuth / integrations explain UX

**Task:** `settings-oauth-explain-ux-2026-08-03`  
**Owner:** ui-ux-designer → frontend-engineer  
**Surface:** `/settings` (User settings → Integrations)  
`frontend/applyvault-jobs-ui/src/app/features/settings/pages/user-settings-page/`

---

## 1. Executive Summary

Users can connect Calendar, GitHub, and Gmail, but explanations are uneven: empty-state provider cards have a one-line blurb; connected-state connect buttons are bare labels; Refresh has no cue; Disconnect relies only on the confirm dialog after click.

**Recommendation:** Reuse the hover/focus popover pattern (`role="tooltip"`, `aria-describedby`, suppress-on-click until mouseleave) on Connect / Refresh / Disconnect controls. Unify empty vs connected connect UI around the richer provider-action card. No API or facade contract changes.

---

## 2. Scope Confirmation

| In scope | Out of scope |
| --- | --- |
| Settings integrations page copy + interaction explainers | New OAuth providers or Outlook mailbox |
| Light layout / hierarchy polish (tokens, spacing, empty vs connected clarity) | API / facade / endpoint changes |
| FE-implementable acceptance checklist | Chrome extension UI |
| Job-results page rule | Not applicable (different surface) |

---

## 3. Verified Facts

From current Settings template:

1. **Empty state** uses `.settings-page__provider-action` cards (badge + title + muted one-liner + Connect).
2. **Connected / partial calendar state** switches to flat `.settings-page__primary-action` labels (`Connect Google`, `Microsoft connected` disabled) — weaker explanation than empty state.
3. **Refresh** is a header secondary button with no help text.
4. **Disconnect** opens an existing `alertdialog` with solid consequence copy (keep).
5. Hover/focus popovers: wrap → button + absolute popover; show on `:hover` / `:focus-within`; `--suppressed` after click + `blur()`; clear suppress on `mouseleave`.
6. Tokens in use: `--app-*` on both surfaces. Prefer reuse over new system.
7. **Gmail-only** mailbox sync today — must not imply Microsoft/Outlook mail.
8. Facades already expose connect / disconnect / load — bindings stay.

---

## 4. Assumptions

- Hover + keyboard focus is enough for progressive disclosure; no always-visible marketing paragraphs under every button.
- FE may keep popover styles in Settings SCSS, or a tiny shared class later — either OK if visual parity holds.
- Native `title` may remain as progressive enhancement but must **not** be the only explanation for Connect actions.
- Settings popovers stay shorter (title + lead + detail).

---

## 5. Decisions

| ID | Decision |
| --- | --- |
| D1 | **Pattern:** hover/focus chip-style popover on action controls. |
| D2 | Apply popovers to **Connect**, **Refresh**, and **Disconnect** controls. |
| D3 | **Unify connect UI:** when a provider is available to connect, always use the provider-action card (empty + partial). Do not show disabled “X connected” primary buttons — connected accounts already appear in the list with status chips. |
| D4 | Keep section status chips, loading skeletons, error alerts, and disconnect confirm dialog. |
| D5 | Copy facts locked: Calendar = Google and/or Microsoft calendars for interview events from saved jobs; GitHub = repo/profile for portfolio / CV projects; Gmail = background sync for rejection/interview emails for saved jobs (Gmail only). |
| D6 | No new design tokens; reuse `--app-surface`, `--app-border-strong`, `--app-shadow-md`, `--app-radius`, text colors. |

---

## 6. UX deliverables

### 6.1 Interaction pattern (hover/focus popover)

**Behavior:** wrap → button + absolute popover; show on `:hover` / `:focus-within`; suppress after click + `blur()`; clear suppress on `mouseleave`.

**Settings adaptation:**

```html
<div
  class="settings-page__help-wrap"
  [class.settings-page__help-wrap--suppressed]="isHelpSuppressed(helpKey)"
  (mouseleave)="onHelpMouseLeave(helpKey)">
  <button
    type="button"
    …existing classes / disabled / click…
    [attr.aria-describedby]="helpId"
    (click)="onConnectOrAction(..., $event)">
    …label / provider-action content…
  </button>
  <div class="settings-page__help-popover" role="tooltip" [id]="helpId">
    <p class="settings-page__help-popover-title">{{ title }}</p>
    <p class="settings-page__help-popover-lead">{{ lead }}</p>
    <p>{{ detail }}</p>
  </div>
</div>
```

**Behavior rules:**

| Rule | Spec |
| --- | --- |
| Show | Wrap `:hover` or `:focus-within`, unless suppressed |
| Hide | Default opacity/visibility; suppressed class forces hide |
| Suppress | On activating click (Connect starts OAuth, Refresh starts load, Disconnect opens dialog): set suppress key for that control, `blur()` the button |
| Clear suppress | `mouseleave` on the wrap when that key is suppressed |
| Keyboard | Focus on button reveals popover; Escape is not required beyond native blur |
| Disabled | Disabled Connect/Refresh: still allow focus + popover if focusable; if not focusable, section intro remains the fallback |
| Positioning | Prefer below control (`top: calc(100% + 0.5rem)`); flip/align right near viewport edge if needed |
| Width | `min(22rem, calc(100vw - 2.5rem))` |
| Pointer events | Popover `pointer-events: none` (tooltip, not dialog) |

**Help keys (suggested):**

- `calendar:connect:google` / `calendar:connect:microsoft` / `calendar:refresh` / `calendar:disconnect:{id}`
- `github:connect` / `github:refresh` / `github:disconnect:{id}`
- `mail:connect:gmail` / `mail:refresh` / `mail:disconnect:{id}`

Tooltip element ids must be unique in the document (e.g. `settings-help-calendar-connect-google`).

### 6.2 Copy matrix

#### Page hero

| Element | Copy |
| --- | --- |
| Eyebrow | User settings *(keep)* |
| H1 | Integrations *(keep)* |
| Subtitle | Connect calendar, GitHub, and Gmail so ApplyVault can schedule interviews, pull portfolio projects, and notice status emails for your saved jobs. |

#### Calendar section

| Element | Copy |
| --- | --- |
| Label | Calendar |
| H2 | Connections *(or “Calendar” if FE prefers singular section title — either OK; keep one clear H2)* |
| Section help | Connect Google Calendar and/or Microsoft Outlook calendar. ApplyVault uses these accounts when you create interview events from saved jobs. |
| Empty H3 | No calendar connected |
| Empty body | Connect at least one calendar to schedule interview events from the jobs workspace. |
| Status chips | Keep current logic (`Not connected` / `N connected` / `Loading`) |

| Control | Popover title | Lead | Detail |
| --- | --- | --- | --- |
| Connect Google | Google Calendar | Create interview events on Google Calendar | Opens Google sign-in so ApplyVault can add interview events from your saved jobs to this calendar account. |
| Connect Microsoft | Microsoft Calendar | Create interview events on Outlook calendar | Opens Microsoft sign-in so ApplyVault can add interview events from your saved jobs to this Outlook calendar account. |
| Refresh | Refresh calendars | Reload connected calendar accounts | Fetches the latest connection status and expiry for Google and Microsoft calendars. Does not create events. |
| Disconnect | Disconnect calendar | Stop using this calendar for interview events | Removes this account from ApplyVault. You can reconnect later. Confirm in the next step. |

**Inline card one-liner (visible without hover — keep on provider-action):**

- Google: `Interview events on Google Calendar`
- Microsoft: `Interview events on Outlook calendar`

#### GitHub section

| Element | Copy |
| --- | --- |
| Label | Portfolio |
| H2 | GitHub *(keep)* |
| Section help | Connect GitHub so ApplyVault can read your profile and repositories for portfolio and CV project sections. |
| Empty H3 | No GitHub account connected |
| Empty body | Link GitHub to import repositories into your portfolio and future CV project sections. |

| Control | Popover title | Lead | Detail |
| --- | --- | --- | --- |
| Connect GitHub | GitHub | Repository and profile access | Opens GitHub sign-in so ApplyVault can read your public profile and repos for portfolio / CV projects. |
| Refresh | Refresh GitHub | Reload GitHub connection | Fetches the latest GitHub connection status. Does not change which repos are imported. |
| Disconnect | Disconnect GitHub | Remove GitHub from ApplyVault | Stops repo/profile access. Synced projects will be removed from ApplyVault. Confirm in the next step. |

**Inline card one-liner:** `Repository and profile access` *(keep)*

**Disconnect dialog** *(keep existing meaning; optional polish):*

- Current: `Disconnect GitHub? Synced projects will be removed from ApplyVault.` — **keep**.

#### Gmail section

| Element | Copy |
| --- | --- |
| Label | Mailbox |
| H2 | Gmail *(keep)* |
| Section help | Connect Gmail for background email sync. ApplyVault looks for rejection and interview messages related to your saved jobs. Gmail only — Outlook mailbox sync is not available yet. |
| Empty H3 | No Gmail mailbox connected |
| Empty body | Connect Gmail to detect rejection and interview emails for saved jobs in the background. |

| Control | Popover title | Lead | Detail |
| --- | --- | --- | --- |
| Connect Gmail | Gmail | Background sync for job status emails | Opens Google sign-in for Gmail so ApplyVault can detect rejection and interview emails about your saved jobs. Does not send mail. Outlook mailbox is not supported. |
| Refresh | Refresh Gmail | Reload mailbox connection | Fetches the latest Gmail connection and sync status. Does not send email. |
| Disconnect | Disconnect Gmail | Stop mailbox sync | Stops interview/rejection email detection for saved jobs. Confirm in the next step. |

**Inline card one-liner:** `Rejection and interview email sync`

**Disconnect dialog** *(keep):*

- Current: `Disconnect Gmail? Interview and rejection email sync will stop for your saved jobs.` — **keep**.

**Calendar disconnect dialog** *(keep):*

- Current pattern with provider label — **keep**.

### 6.3 Layout / polish (incremental)

1. **One connect pattern:** Always render available providers with `.settings-page__provider-action` cards (badge + copy + Connect). Remove the dual path of flat primary “Connect Google” buttons in the non-empty branch.
2. **Connected clarity:** List row remains the source of truth (account name, Connected / expiry / sync chips, meta dates). Do not duplicate a disabled “Google connected” CTA above the list.
3. **Partial calendar:** If Google connected and Microsoft not (or reverse), show only the missing provider’s card above/beside the list; section help still explains both.
4. **Header actions:** Keep Refresh top-right; wrap with help popover. Visually secondary (already is).
5. **Hierarchy per panel:** Label → H2 + status chip → section help → (error) → (loading \| empty cards \| available-connect cards + connection list).
6. **Spacing:** Keep existing panel rhythm; ensure provider-action cards get `margin-top` when appearing above a non-empty list (`~1.25rem`).
7. **Avoid:** New cards around whole sections, purple glow, always-visible alert-styled callouts for help, inventing Outlook mail CTAs.
8. **Destructive:** Disconnect stays outline/secondary until confirm; confirm button remains the filled destructive primary in the dialog (existing).

### 6.4 States

| State | UX |
| --- | --- |
| Loading | Existing skeletons; Refresh shows `Refreshing...` + `aria-busy`; popovers optional while disabled |
| Empty | Empty heading + body + provider-action card(s) with popovers |
| Connected | List + Disconnect popovers; optional Refresh popover |
| Partial (calendar) | List of connected + provider-action for missing provider(s) |
| Error | Existing alert; do not hide connect/refresh |
| Connecting / Disconnecting | Existing label swaps (`Connecting...` / `Disconnecting...`); suppress popover on that click |
| Sync issue (mail) | Keep section + row warning chips; no new badge taxonomy |

---

## 7. Contracts (FE bindings)

**Preserve:**

- `CalendarConnectionsFacade` / `GitHubConnectionsFacade` / `MailConnectionsFacade` — `connect`, `disconnect`, `load`, loading/error/connecting signals
- Disconnect confirm flow (`beginDisconnect*`, `confirmDisconnect`, `cancelDisconnectConfirm`)
- Provider label/initial/badge helpers
- Status / expiry / sync presentation helpers
- Routes and auth redirects owned by facades

**Allowed FE-local additions:**

- Help key signal + suppress helpers
- Static copy map or inline strings in the settings page component
- SCSS for `.settings-page__help-wrap` / `__help-popover*` (token-aligned)

**Do not:**

- Add API fields, new providers, or Outlook mail connect
- Remove `role="alertdialog"` confirm
- Require new design tokens or shared library packages unless already trivial

---

## 8. Security / a11y notes

- Popover content is static explanatory copy — no tokens, scopes, or raw OAuth URLs.
- Use `role="tooltip"` + `aria-describedby` (not `role="dialog"` for hover help).
- Keep confirm dialog `aria-modal` + labelled/described ids.
- Do not trap focus in tooltips.
- Destructive Disconnect remains behind confirm; popover is preview only (“Confirm in the next step”).
- Do not claim Microsoft/Outlook **mailbox** sync; Microsoft is **calendar** only in copy.

---

## 9. Validation (FE acceptance checklist)

Frontend-engineer should treat this as done when:

- [ ] **A1** Hovering Connect Google / Microsoft / GitHub / Gmail shows a popover with title, lead, and detail from the copy matrix.
- [ ] **A2** Keyboard focusing those Connect controls shows the same popover (`:focus-within`).
- [ ] **A3** Each Connect control has `aria-describedby` pointing at a unique `role="tooltip"` id.
- [ ] **A4** Activating Connect suppresses that popover until pointer leaves the wrap (and blurs the button).
- [ ] **A5** Refresh buttons in all three sections have short popovers (title/lead/detail as matrix).
- [ ] **A6** Disconnect buttons have short popovers; existing confirm dialog still runs and keeps consequence copy.
- [ ] **A7** Empty and partial states use provider-action cards (not bare primary connect labels); no disabled “X connected” CTA when the account already appears in the list.
- [ ] **A8** Section/hero copy updated per matrix; Gmail copy states Gmail-only / no Outlook mailbox.
- [ ] **A9** Calendar copy mentions Google and/or Microsoft calendar → interview events from saved jobs.
- [ ] **A10** GitHub copy mentions repo/profile for portfolio / CV projects.
- [ ] **A11** Uses existing `--app-*` tokens; no new design system; facade connect/disconnect/load bindings unchanged.
- [ ] **A12** Loading, error, and status-chip behaviors remain intact.

---

## 10. Risks

| Risk | Mitigation |
| --- | --- |
| Popover clipped by `overflow: hidden` on connection list / panel | Attach wrap outside overflow containers; prefer portal-less absolute on section header/actions first |
| Dense mobile: popover covers CTAs | Cap width; prefer below; user can move pointer / blur |
| Dual copy (inline one-liner + popover) feels redundant | Inline stays one short phrase; popover carries why + scope |
| FE copies large SCSS twice | Accept short Settings-local mirror; shared extract optional later |

---

## 11. Handoffs

- **Next agent:** `frontend-engineer`
- **Artifact:** this file + `handoff-ui-ux-designer.yaml`
- **Scratch:** `agent-system/scratch/settings-oauth-explain-ux-2026-08-03/` (probes only if needed)

---

## 12. Status

**READY** for frontend implementation. No product blockers. No API decisions required.
