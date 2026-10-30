"""PreToolUse hook: stop the agent from quietly changing the solution's shape or its own gates.

- Banned libraries in C# code (MediatR, AutoMapper, FluentValidation) are denied.
- New packages, new projects, project-file edits and edits to the gates themselves need my approval.
- New files named like enterprise ceremony (*Factory, *Manager, *Helper, ...) need my approval.

Reads the hook JSON from stdin. Prints a permission decision as JSON, or nothing to allow.
"""
import json
import os
import re
import sys

BANNED_USINGS = re.compile(r"^\s*(global\s+)?using\s+(MediatR|AutoMapper|FluentValidation)\b", re.MULTILINE)

# Files that define the solution's shape or the gates. Editing them needs approval.
GUARDED_FILES = [
    (re.compile(r"\.(csproj|sln|slnx)$"), "project or solution file (packages, project references, new projects)"),
    (re.compile(r"(^|/)Directory\.(Packages|Build)\.(props|targets)$"), "central package or build settings"),
    (re.compile(r"(^|/)(global\.json|nuget\.config|NuGet\.Config|package\.json)$"), "SDK or package configuration"),
    (re.compile(r"(^|/)(\.editorconfig|BannedSymbols\.txt)$"), "analyzer gate"),
    (re.compile(r"(^|/)ArchitectureTests[^/]*\.cs$"), "architecture tests (a gate)"),
    (re.compile(r"(^|/)\.claude/(settings[^/]*\.json|done-check\.json|hooks/.*)$"), "agent hooks and settings"),
]

CEREMONY_NAME = re.compile(r"(^|/)(Base[A-Z]\w*|\w+(Factory|Manager|Helper|Helpers|Wrapper|Provider|Builder|Utils|Utility))\.cs$")

PACKAGE_COMMANDS = re.compile(
    r"\bdotnet\s+(add\s+\S+\s+package|add\s+package|package\s+add|new\s+(?!tool-manifest)\w+|sln\s+\S*\s*add)"
    r"|\bnuget(\.exe)?\s+install\b"
    r"|\b(npm|pnpm)\s+(i|install|add)\s+(?![-&|;])\S"
    r"|\byarn\s+add\b"
)


def relative_path(file_path, project_dir):
    path = file_path.replace("\\", "/")
    root = project_dir.replace("\\", "/").rstrip("/")
    if root and path.lower().startswith(root.lower() + "/"):
        path = path[len(root) + 1:]
    return path


def new_text(tool_name, tool_input):
    if tool_name == "Write":
        return tool_input.get("content", "")
    if tool_name == "Edit":
        return tool_input.get("new_string", "")
    if tool_name == "MultiEdit":
        return "\n".join(e.get("new_string", "") for e in tool_input.get("edits", []))
    return ""


def decide(event, project_dir):
    tool_name = event.get("tool_name", "")
    tool_input = event.get("tool_input", {}) or {}

    if tool_name == "Bash":
        command = tool_input.get("command", "")
        if PACKAGE_COMMANDS.search(command):
            return "ask", "ClaudeRules: this command adds a package or a project. No new package or project without my approval (coding guideline: Write less code, Solution layout)."
        return None

    if tool_name not in ("Write", "Edit", "MultiEdit"):
        return None

    path = relative_path(tool_input.get("file_path", ""), project_dir)

    if path.endswith(".cs") and BANNED_USINGS.search(new_text(tool_name, tool_input)):
        return "deny", "ClaudeRules: MediatR, AutoMapper and FluentValidation are not used in this codebase. Call services directly, map with a few lines of code, validate inline at the top of the service method."

    for pattern, what in GUARDED_FILES:
        if pattern.search(path):
            return "ask", f"ClaudeRules: {path} is a {what}. Changing it needs my approval. Say why in one line."

    if tool_name == "Write" and CEREMONY_NAME.search(path) and not os.path.exists(os.path.join(project_dir, path)):
        return "ask", f"ClaudeRules: new file {path} looks like a Factory/Manager/Helper/Provider/Base class. These are only allowed when they hold real logic (coding guideline: Abstractions). Say what logic it holds."

    return None


def main():
    try:
        event = json.load(sys.stdin)
    except ValueError:
        return 0
    project_dir = os.environ.get("CLAUDE_PROJECT_DIR") or event.get("cwd") or os.getcwd()
    result = decide(event, project_dir)
    if result:
        decision, reason = result
        print(json.dumps({
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": decision,
                "permissionDecisionReason": reason,
            }
        }))
    return 0


if __name__ == "__main__":
    sys.exit(main())
