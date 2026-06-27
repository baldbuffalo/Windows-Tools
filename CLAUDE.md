# TOKEN SAVING MODE v3 (PROJECT-AWARE SCOPED SCAN)

Always operate in this mode for this repo.

## Core objective
Minimize tokens while staying accurate and using correct project context.

## Absolute rules
- No narration of actions, thoughts, or debugging process.
- No explanations unless explicitly requested.
- No step-by-step reasoning.
- No storytelling about CI, builds, or errors.
- No repeated summaries of work done.

## Execution style
- Act directly and silently.
- Do all thinking/working silently — no narration while executing.
- Only AFTER finishing, give one short summary of what was added/fixed.
- Prefer immediate fixes over analysis.
- Do not explore multiple solutions unless necessary.
- Do not re-check already known information.

## Project scope rules
- Scan relevant project folders when needed for context.
- Restrict scanning ONLY to the relevant platform/module — do not scan
  unrelated platform folders unless explicitly required.

## Search / version rules
- Search only when necessary.
- Use the first high-confidence result.
- Do not double-verify unless results conflict.

## Code output rules
- Prefer minimal diffs or patch-style edits.
- Do not output full files unless requested.
- Do not add comments unless explicitly requested.
- Do not generate documentation, changelogs, or explanations.

## Output format (strict) — only one of:
- A) SHORT RESULT: e.g. "Fixed 2 errors. Build passed. Pushed to main (b13210f)."
- B) REQUIRED INPUT: e.g. "Need clarification: X?"
- C) CODE PATCH ONLY: show only changed lines / minimal diff.

## Forbidden output
Debug narration, multi-paragraph explanations, repeated summaries.

## Git
- Auto-commit to `main`, no PRs unless explicitly asked.
