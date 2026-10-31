---
name: rules-review
description: Review a finished feature in a fresh session against its plan and the coding guideline, then propose where each finding should be prevented next time. Use after the last stage of a plan, before merging.
---

# Review against the rules

Run this in a new session, not the one that wrote the code: it has to read the diff without the reasons that produced it.

## Inputs

- The plan: `docs/plans/<feature>.md`. Ask if there is more than one candidate.
- The base branch to diff against. Default `main`; ask if it doesn't exist.

## Steps

1. Read the plan, `.claude/rules/coding-guideline.md` and `.claude/rules/development-guideline.md`.
2. Get the change: `git diff <base>...HEAD --stat`, then the full diff. Read each changed file in full where the diff alone doesn't show enough.
3. Check, in this order:
   1. **Scope.** Every changed file and every new type traces to a stage in the plan. List anything that doesn't.
   2. **Existing features.** Any change to an existing feature's UI, flow, API contract, data or defaults that the plan didn't list.
   3. **New types.** Each new class, interface and record, with its reason. Flag those with one caller, or whose only reason is structure.
   4. **Coding guideline.** Go section by section (Write less code, Method shape, Errors, Comments, Controllers, Abstractions, Data access, Testability, Naming). Skip what the build and architecture tests already enforce. In Data access, trace every navigation a service reads or changes (`order.Details.Clear()`, `bookmark.Chunks.Add(...)`) back to the repository method that loaded the entity, and check that method `Include`s it. Without the Include the collection is silently empty, so old children are never removed.
   5. **Tests.** Tests sit at the seams the plan agreed. Expected values are literals or come from the spec, not recomputed. No mocks of the team's own classes other than repositories. Each bug fix has a test that reproduces it.
4. Don't change code yet.

## Output

Findings, most important first. For each one:

- `file:line`, the rule it breaks (section name), and what to do about it in one or two sentences.

Then a **Retro** table, one row per finding that could recur:

| Finding | Prevent it with | Concretely |
|---------|-----------------|------------|
| ... | architecture test / analyzer or `.editorconfig` / banned API / hook / skill / rule line | the test, setting or sentence to add |

Prefer a gate over a skill, and a skill over a rule line. When a rule line becomes a gate, say which line to delete from the rules file.

Then ask which findings to fix. Apply only the approved ones, keeping each fix surgical.
