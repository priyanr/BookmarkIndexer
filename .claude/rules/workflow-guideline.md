# Workflow Guideline

How a feature goes from request to merged code. The coding guideline says what the code looks like; this file says how the work is done. Hooks in `.claude/hooks/` enforce the mechanical parts, so don't try to get around them.

## 1. Clarify

- If the request is vague enough that two reasonable developers would build different things, ask questions before planning. One question at a time, each with your recommended answer.
- Use the terms in `GLOSSARY.md`. If a request uses a term that isn't there or means something else there, ask which one is meant. Don't invent a new name for an existing concept.

## 2. Plan

Plan in plan mode. Write the approved plan to `docs/plans/<feature>.md` before writing code, so it survives the session. Use this shape:

```markdown
# <Feature>

## Goal
One or two sentences: what the user can do afterwards.

## Affected existing features
Name each existing feature whose UI, flow, API contract, data or defaults change, or "None".

## Stages
### Stage 1: <thin end-to-end path>
- Behaviour: what works after this stage, from the outside.
- Test seam: where it is tested (static rule / service with real DbContext / WebApplicationFactory / repository against real DB).
- New types: each new class, interface or record with one line saying why it exists.
- Files touched: the existing files that change.
- [ ] done

### Stage 2: ...

## Out of scope
What this plan deliberately does not do.
```

- Stages are vertical slices: each one works end to end (endpoint → service → database → test) for a narrow case. Stage 1 is the thinnest path that proves the design. Never "all entities first, then all services, then all controllers".
- Keep a stage small enough to review in one sitting: roughly under 10 changed files and 400 changed lines. Split it if it is bigger.
- Every new type listed in the plan needs a reason tied to the request. "Separation of concerns", "future flexibility" or "best practice" is not a reason; drop the type.
- Wait for approval of the plan before stage 1.

## 3. Build, one stage at a time

- Work only on the current stage. Don't start code that belongs to a later stage.
- If an architect skill fits the change (`add-endpoint`, `add-integration`), follow it.
- Write the test at the agreed seam first, watch it fail, then write just enough code to pass. Expected values come from the request or a worked example, not from re-computing them the way the code does.
- If the plan turns out to be wrong, stop and say what changed. Update the plan file before continuing.
- A stage is done when:
  - `dotnet build` passes with no warnings and `dotnet test` passes (the Stop hook runs both and will not let you finish otherwise);
  - you have done the "Before you finish" check from the coding guideline;
  - the stage is ticked in the plan file.
- After each stage, stop and summarise: what changed, new types and why, anything you noticed but did not fix. Then I commit and we start the next stage from green.

## 4. Review

After the last stage, review in a fresh session with the `rules-review` skill: the plan file and the full diff against the base branch, checked against the coding guideline. Refactoring found in review is done there, not during the stages.

## 5. Retro

For each problem the review found, propose where the fix belongs, in this order of preference:

1. A gate: an architecture test, an analyzer setting in `.editorconfig`, a banned API, or a hook check.
2. A skill: when the same kind of change keeps coming out wrong.
3. A line in a rules file: only when it can't be checked mechanically.

Rules that become gates are removed from the markdown.
