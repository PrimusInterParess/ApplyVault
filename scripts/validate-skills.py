#!/usr/bin/env python3
"""Validate The Architect library: core, slash commands, Cursor adapters, schemas."""

from __future__ import annotations

import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SKILLS_DIR = ROOT / ".cursor" / "skills"
COMMANDS_DIR = ROOT / ".cursor" / "commands"
RULES_DIR = ROOT / ".cursor" / "rules"
CORE_DIR = ROOT / "core" / "workflows"
SCHEMAS_DIR = ROOT / "schemas"
MAX_SKILL_LINES = 500

FRONTMATTER_RE = re.compile(r"^---\r?\n(.*?)\r?\n---\r?\n", re.DOTALL)
NAME_RE = re.compile(r"^name:\s*(.+)$", re.MULTILINE)
MD_LINK_RE = re.compile(r"\[[^\]]*\]\(([^)]+)\)")

REQUIRED_CORE = [
    "agent-system-builder.md",
    "project-discovery.md",
    "project-discovery.reference.md",
    "brownfield-research.md",
    "brownfield-research.reference.md",
    "generate-prompt-pack.md",
    "create-agent.md",
    "extend-fleet.md",
    "audit-prompts.md",
    "operate-agent-system.md",
]

REQUIRED_COMMANDS = [
    "architect.md",
    "discover.md",
    "brownfield.md",
    "hybrid.md",
    "generate-prompt-pack.md",
    "create-agent.md",
    "extend-fleet.md",
    "audit.md",
    "operate.md",
]

REQUIRED_RULES = [
    "operate-native-subagents.mdc",
]


def fail(msg: str, errors: list[str]) -> None:
    errors.append(msg)


def check_links(path: Path, label: str, errors: list[str]) -> None:
    text = path.read_text(encoding="utf-8")
    for link in MD_LINK_RE.findall(text):
        if link.startswith(("http://", "https://", "mailto:", "#")):
            continue
        target = (path.parent / link).resolve()
        if not target.exists():
            fail(f"{label}: broken link '{link}'", errors)


def main() -> int:
    errors: list[str] = []

    if not CORE_DIR.is_dir():
        fail("core/workflows missing", errors)
    else:
        for name in REQUIRED_CORE:
            path = CORE_DIR / name
            if not path.is_file():
                fail(f"missing core workflow: {name}", errors)
            else:
                check_links(path, f"core/{name}", errors)

    slash = ROOT / "core" / "slash-commands.md"
    if not slash.is_file():
        fail("core/slash-commands.md missing", errors)
    else:
        check_links(slash, "core/slash-commands.md", errors)

    if not COMMANDS_DIR.is_dir():
        fail(".cursor/commands missing", errors)
    else:
        for name in REQUIRED_COMMANDS:
            if not (COMMANDS_DIR / name).is_file():
                fail(f"missing Cursor command: {name}", errors)

    if not RULES_DIR.is_dir():
        fail(".cursor/rules missing", errors)
    else:
        for name in REQUIRED_RULES:
            if not (RULES_DIR / name).is_file():
                fail(f"missing Cursor rule: {name}", errors)

    skill_count = 0
    if not SKILLS_DIR.is_dir():
        fail(".cursor/skills missing", errors)
    else:
        skill_dirs = sorted(p for p in SKILLS_DIR.iterdir() if p.is_dir())
        skill_count = len(skill_dirs)
        for skill_dir in skill_dirs:
            skill_md = skill_dir / "SKILL.md"
            if not skill_md.is_file():
                fail(f"{skill_dir.name}: SKILL.md missing", errors)
                continue
            text = skill_md.read_text(encoding="utf-8")
            lines = text.count("\n") + (0 if text.endswith("\n") or not text else 1)
            if lines > MAX_SKILL_LINES:
                fail(f"{skill_dir.name}: SKILL.md has {lines} lines (max {MAX_SKILL_LINES})", errors)
            match = FRONTMATTER_RE.match(text)
            if not match:
                fail(f"{skill_dir.name}: missing YAML frontmatter", errors)
            else:
                block = match.group(1)
                name_m = NAME_RE.search(block)
                if not name_m:
                    fail(f"{skill_dir.name}: frontmatter missing name", errors)
                elif name_m.group(1).strip().strip("\"'") != skill_dir.name:
                    fail(f"{skill_dir.name}: name does not match directory", errors)
                if "description:" not in block:
                    fail(f"{skill_dir.name}: frontmatter missing description", errors)
            if "core/workflows/" not in text:
                fail(f"{skill_dir.name}: Cursor adapter must link to core/workflows/", errors)
            check_links(skill_md, skill_dir.name, errors)

    expected = {
        "project-context.schema.json",
        "capability-matrix.schema.json",
        "discovery-ledger.schema.json",
        "requirements-spec.schema.json",
    }
    if not SCHEMAS_DIR.is_dir():
        fail("schemas/ directory missing", errors)
    else:
        found = {p.name for p in SCHEMAS_DIR.glob("*.schema.json")}
        for name in sorted(expected - found):
            fail(f"missing schema: {name}", errors)
        for path in sorted(SCHEMAS_DIR.glob("*.schema.json")):
            try:
                data = json.loads(path.read_text(encoding="utf-8"))
            except json.JSONDecodeError as exc:
                fail(f"{path.name}: invalid JSON ({exc})", errors)
                continue
            if not isinstance(data, dict) or "$schema" not in data:
                fail(f"{path.name}: expected object with $schema", errors)

    for required in ("AGENTS.md", "LICENSE", "INSTALL.md", "CONTRIBUTING.md"):
        if not (ROOT / required).is_file():
            fail(f"{required} missing", errors)

    if errors:
        print("FAIL:")
        for e in errors:
            print(f"  - {e}")
        return 1

    cmd_count = len(list(COMMANDS_DIR.glob("*.md"))) if COMMANDS_DIR.is_dir() else 0
    print(f"OK: core workflows, {skill_count} skills, {cmd_count} slash commands, schemas valid")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
