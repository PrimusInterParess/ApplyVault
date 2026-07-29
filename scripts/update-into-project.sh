#!/usr/bin/env bash
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET="${1:-}"
DRY_RUN="${DRY_RUN:-0}"
SKIP_CURSOR="${SKIP_CURSOR:-0}"
SKIP_CLAUDE="${SKIP_CLAUDE:-0}"
SKIP_COPILOT="${SKIP_COPILOT:-0}"
SKIP_AGENTS_MD="${SKIP_AGENTS_MD:-0}"
# Skip app-root .architect/ stamp when set, or when
# agent-system/architect-install.yaml has write_app_root_stamp: false
NO_STAMP="${ARCHITECT_NO_APP_STAMP:-0}"

if [[ -z "$TARGET" ]]; then
  echo "Usage: $0 /path/to/target-project" >&2
  echo "Env: DRY_RUN=1 SKIP_CURSOR=1 SKIP_CLAUDE=1 SKIP_COPILOT=1 SKIP_AGENTS_MD=1 ARCHITECT_NO_APP_STAMP=1" >&2
  exit 1
fi

if [[ ! -d "$TARGET" ]]; then
  echo "Target not found: $TARGET" >&2
  exit 1
fi

VERSION="unknown"
if [[ -f "$ROOT/VERSION" ]]; then
  VERSION="$(tr -d '[:space:]' < "$ROOT/VERSION")"
fi

should_write_app_stamp() {
  if [[ "$NO_STAMP" == "1" ]]; then
    return 1
  fi
  local policy="$TARGET/agent-system/architect-install.yaml"
  if [[ -f "$policy" ]] && grep -Eq '^[[:space:]]*write_app_root_stamp:[[:space:]]*false[[:space:]]*$' "$policy"; then
    return 1
  fi
  return 0
}

update_path() {
  local rel="$1"
  local optional="${2:-0}"
  local src="$ROOT/$rel"
  local dst="$TARGET/$rel"
  if [[ ! -e "$src" ]]; then
    if [[ "$optional" == "1" ]]; then
      return
    fi
    echo "Missing source: $rel" >&2
    exit 1
  fi
  if [[ "$DRY_RUN" == "1" ]]; then
    if [[ -e "$dst" ]]; then
      echo "DRY-RUN REPLACE: $rel"
    else
      echo "DRY-RUN ADD: $rel"
    fi
    return
  fi
  mkdir -p "$(dirname "$dst")"
  rm -rf "$dst"
  cp -R "$src" "$dst"
  echo "UPDATED: $rel"
}

echo "Updating The Architect ($VERSION) into: $TARGET"
echo "Preserved: agent-system/ (not touched)"
echo

update_path "core"
update_path "schemas"
update_path "references/source-prompts"
update_path "scripts"
update_path "examples" 1
update_path "INSTALL.md" 1
update_path "VERSION" 1

if [[ "$SKIP_AGENTS_MD" != "1" ]]; then
  update_path "AGENTS.md"
fi

if [[ "$SKIP_CURSOR" != "1" ]]; then
  update_path ".cursor/skills"
  update_path ".cursor/commands"
  update_path ".cursor/rules"
fi

if [[ "$SKIP_CLAUDE" != "1" && -f "$ROOT/adapters/claude-code/CLAUDE.md" ]]; then
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "DRY-RUN REPLACE: CLAUDE.md"
  else
    cp "$ROOT/adapters/claude-code/CLAUDE.md" "$TARGET/CLAUDE.md"
    echo "UPDATED: CLAUDE.md"
  fi
fi

if [[ "$SKIP_COPILOT" != "1" && -f "$ROOT/adapters/copilot/copilot-instructions.md" ]]; then
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "DRY-RUN REPLACE: .github/copilot-instructions.md"
  else
    mkdir -p "$TARGET/.github"
    cp "$ROOT/adapters/copilot/copilot-instructions.md" "$TARGET/.github/copilot-instructions.md"
    echo "UPDATED: .github/copilot-instructions.md"
  fi
fi

if should_write_app_stamp; then
  if [[ "$DRY_RUN" == "1" ]]; then
    echo "DRY-RUN would write: .architect/library-version = $VERSION"
  else
    mkdir -p "$TARGET/.architect"
    printf '%s' "$VERSION" > "$TARGET/.architect/library-version"
    echo "STAMP: .architect/library-version = $VERSION"
  fi
else
  echo "STAMP skipped (ARCHITECT_NO_APP_STAMP / write_app_root_stamp: false)"
fi

echo
echo "Update complete. Review git diff in the target project, then reload the IDE window if Cursor adapters changed."
echo "Your generated fleet under agent-system/ was left unchanged."
echo "Next: in the target project, run /upgrade-architect to regenerate agent docs from the approved spec."
