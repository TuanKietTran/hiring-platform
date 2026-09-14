#!/usr/bin/env python3
"""Format C# positional records and primary constructors vertically.

Roslyn does not expose an EditorConfig rule for this layout. This formatter is
purposefully narrow: it only rewrites parameter lists attached to type
declarations, never methods, delegates, calls, or ordinary constructors.
"""
from __future__ import annotations

import argparse
import re
import sys
from pathlib import Path

DECLARATION = re.compile(
    r"(?m)^(?P<indent>[ \t]*)(?P<head>"
    r"(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly|ref|file)\s+)+"
    r"(?:record(?:\s+(?:class|struct))?|class|struct)\s+"
    r"[A-Za-z_][A-Za-z0-9_]*(?:\s*<[^>\n]+>)?"
    r")\s*\("
)


def closing_parenthesis(text: str, opening: int) -> int:
    depth = 0
    quote: str | None = None
    escaped = False
    for index in range(opening, len(text)):
        char = text[index]
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = None
            continue
        if char in {'"', "'"}:
            quote = char
        elif char == "(":
            depth += 1
        elif char == ")":
            depth -= 1
            if depth == 0:
                return index
    raise ValueError(f"unclosed type parameter list at offset {opening}")


def split_parameters(value: str) -> list[str]:
    parameters: list[str] = []
    start = 0
    round_depth = square_depth = curly_depth = angle_depth = 0
    quote: str | None = None
    escaped = False
    for index, char in enumerate(value):
        if quote:
            if escaped:
                escaped = False
            elif char == "\\":
                escaped = True
            elif char == quote:
                quote = None
            continue
        if char in {'"', "'"}:
            quote = char
        elif char == "(": round_depth += 1
        elif char == ")": round_depth -= 1
        elif char == "[": square_depth += 1
        elif char == "]": square_depth -= 1
        elif char == "{": curly_depth += 1
        elif char == "}": curly_depth -= 1
        elif char == "<": angle_depth += 1
        elif char == ">": angle_depth = max(0, angle_depth - 1)
        elif char == "," and not any((round_depth, square_depth, curly_depth, angle_depth)):
            parameters.append(value[start:index])
            start = index + 1
    parameters.append(value[start:])
    return [" ".join(parameter.split()) for parameter in parameters if parameter.strip()]


def format_text(text: str) -> str:
    output: list[str] = []
    cursor = 0
    while match := DECLARATION.search(text, cursor):
        opening = match.end() - 1
        closing = closing_parenthesis(text, opening)
        parameters = split_parameters(text[opening + 1:closing])
        if not parameters:
            cursor = closing + 1
            continue

        indent = match.group("indent")
        parameter_indent = indent + "    "
        replacement = (
            f"{indent}{match.group('head')}(\n"
            + ",\n".join(f"{parameter_indent}{parameter}" for parameter in parameters)
            + f"\n{indent})"
        )
        output.append(text[cursor:match.start()])
        output.append(replacement)
        cursor = closing + 1
    output.append(text[cursor:])
    return "".join(output)


def source_files(paths: list[Path]) -> list[Path]:
    found: set[Path] = set()
    for path in paths:
        if path.is_file() and path.suffix == ".cs":
            found.add(path)
        elif path.is_dir():
            found.update(p for p in path.rglob("*.cs") if not {"bin", "obj"}.intersection(p.parts))
    return sorted(found)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--check", action="store_true", help="report files that need formatting")
    parser.add_argument("paths", nargs="*", type=Path, default=[Path("src"), Path("tests")])
    args = parser.parse_args()

    changed: list[Path] = []
    for path in source_files(args.paths):
        original = path.read_text()
        formatted = format_text(original)
        if formatted == original:
            continue
        changed.append(path)
        if not args.check:
            path.write_text(formatted)

    if args.check and changed:
        print("Type declaration parameters must be vertical:", file=sys.stderr)
        for path in changed:
            print(f"  {path}", file=sys.stderr)
        print("Run: python3 scripts/format-csharp-types.py", file=sys.stderr)
        return 1
    if not args.check:
        print(f"Formatted {len(changed)} file(s).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
