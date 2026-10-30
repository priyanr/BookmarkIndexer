"""Stop hook: the agent can't finish while the build or the tests are red.

When the agent tries to end its turn and C# code has changed:
1. Runs the build (warnings are errors) and the tests. If either fails, the agent is sent back
   with the errors. After 3 failed attempts in a row it is let go and you get a message instead,
   so a broken build can't loop forever.
2. If the change adds more new .cs files than allowed, the agent is sent back once to justify
   each new file (coding guideline: Before you finish) or inline it.

A green result is remembered for the exact working-tree state, so a turn that changes nothing
doesn't rebuild. Optional settings live in .claude/done-check.json (see README).
"""
import hashlib
import json
import os
import re
import subprocess
import sys

DEFAULTS = {
    "workingDirectory": ".",
    "commands": [
        "dotnet build -warnaserror --nologo -v q -clp:NoSummary",
        "dotnet test --no-build --nologo -v q",
    ],
    "maxNewFiles": 8,
    "maxAttempts": 3,
}

CODE_FILE = re.compile(r"\.(cs|csproj|props|targets|sln|slnx|razor|cshtml|editorconfig)$|(^|/)BannedSymbols\.txt$")
COMPILER_ERROR = re.compile(r": (error|warning) [A-Z]+\d+:")


def git(project_dir, *args):
    result = subprocess.run(["git", *args], cwd=project_dir, capture_output=True)
    return result.returncode, result.stdout


def changed_files(project_dir):
    """(status, path) for every changed or untracked file, paths relative to the repo root."""
    code, out = git(project_dir, "status", "--porcelain", "-uall", "-z")
    if code != 0:
        return None
    entries = []
    parts = out.decode("utf-8", "replace").split("\0")
    i = 0
    while i < len(parts):
        item = parts[i]
        i += 1
        if len(item) < 4:
            continue
        status, path = item[:2], item[3:]
        if status[0] in "RC":
            i += 1  # the next entry is the original path of a rename/copy
        if re.search(r"(^|/)(bin|obj)/", path):
            continue  # build output in a repo without a .gitignore
        entries.append((status, path))
    return entries


def tree_fingerprint(project_dir, entries):
    """Hash of the uncommitted state: the diff against HEAD plus size and time of untracked files."""
    digest = hashlib.sha256()
    code, diff = git(project_dir, "diff", "HEAD", "--binary")
    if code != 0:
        _, diff = git(project_dir, "diff", "--binary")
    digest.update(diff)
    for status, path in sorted(entries):
        if status == "??":
            try:
                stat = os.stat(os.path.join(project_dir, path))
                digest.update(f"{path}|{stat.st_size}|{stat.st_mtime_ns}".encode())
            except OSError:
                pass
    return digest.hexdigest()


def problem_summary(output, project_dir, limit=60):
    """Compiler errors if there are any, otherwise the end of the output (test failures)."""
    root = project_dir.replace("\\", "/").rstrip("/") + "/"
    lines = [line.rstrip().replace("\\", "/").replace(root, "") for line in output.splitlines() if line.strip()]
    errors = []
    for line in lines:
        line = re.sub(r"\s+\[[^\]]+\.csproj\]$", "", line)
        if COMPILER_ERROR.search(line) and line not in errors:
            errors.append(line)
    picked = errors if errors else lines
    return "\n".join(picked[-limit:])


def load_json(path, fallback):
    try:
        with open(path, encoding="utf-8") as f:
            return json.load(f)
    except (OSError, ValueError):
        return fallback


def main():
    try:
        event = json.load(sys.stdin)
    except ValueError:
        event = {}
    project_dir = os.environ.get("CLAUDE_PROJECT_DIR") or event.get("cwd") or os.getcwd()

    config = dict(DEFAULTS)
    config.update(load_json(os.path.join(project_dir, ".claude", "done-check.json"), {}))

    entries = changed_files(project_dir)
    if entries is None:
        return 0  # not a git repository: nothing to compare against
    code, git_dir = git(project_dir, "rev-parse", "--absolute-git-dir")
    state_path = os.path.join(git_dir.decode().strip() if code == 0 else project_dir, "claude-done-check.json")
    state = load_json(state_path, {})
    session = event.get("session_id", "")

    def save():
        try:
            with open(state_path, "w", encoding="utf-8") as f:
                json.dump(state, f)
        except OSError:
            pass

    if not any(CODE_FILE.search(path) for _, path in entries):
        if state.get("attempts"):
            state["attempts"] = {}
            save()
        return 0  # no code changed in the working tree

    fingerprint = tree_fingerprint(project_dir, entries)

    def block(reason):
        print(json.dumps({"decision": "block", "reason": reason}))
        return 0

    # 1. Build and tests
    if state.get("green") != fingerprint:
        work_dir = os.path.join(project_dir, config["workingDirectory"])
        for command in config["commands"]:
            result = subprocess.run(command, cwd=work_dir, shell=True, capture_output=True,
                                    text=True, encoding="utf-8", errors="replace")
            if result.returncode == 0:
                continue
            attempts = state.get("attempts", {})
            attempts = {session: attempts.get(session, 0) + 1}
            state["attempts"] = attempts
            save()
            summary = problem_summary(result.stdout + "\n" + result.stderr, project_dir)
            if attempts[session] > config["maxAttempts"]:
                state["attempts"] = {}  # let go once; the next turn is checked again
                save()
                print(json.dumps({"systemMessage": f"done-check: `{command}` still fails after {config['maxAttempts']} attempts; stopping anyway. Check the build before committing."}))
                return 0
            return block(
                f"`{command}` failed (attempt {attempts[session]} of {config['maxAttempts']}). "
                f"A stage is not done until the build has no warnings and all tests pass. Fix the cause; "
                f"don't skip, delete or weaken tests, and don't suppress warnings without asking.\n\n{summary}"
            )
        state["green"] = fingerprint
        state["attempts"] = {}
        save()

    # 2. Too many new files
    new_files = sorted(path for status, path in entries
                       if path.endswith(".cs") and (status == "??" or status[0] == "A")
                       and "/Migrations/" not in "/" + path)
    if len(new_files) > config["maxNewFiles"]:
        listing = hashlib.sha256("\n".join(new_files).encode()).hexdigest()
        if state.get("newFilesAcknowledged") != listing:
            state["newFilesAcknowledged"] = listing
            save()
            files = "\n".join(f"- {path}" for path in new_files)
            return block(
                f"This change adds {len(new_files)} new .cs files (limit {config['maxNewFiles']}). "
                f"For each one, say in one line why it exists. Inline any type whose reason is "
                f"'separation of concerns', 'future flexibility' or 'best practice', or that has a single caller. "
                f"If the stage is simply too big, say so and propose a split.\n\n{files}"
            )

    return 0


if __name__ == "__main__":
    sys.exit(main())
