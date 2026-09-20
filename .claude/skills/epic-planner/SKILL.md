---
name: epic-planner
description: Turns a rough feature-area idea into a well-formed GitHub epic ready for implementation — grounds a prioritized roadmap in what the codebase actually contains (not assumptions), then translates the highest-priority phase into a tracking issue plus dependency-ordered child issues, each carrying either a settled design decision or an explicit open question. Use this whenever the user asks to "plan out X", "create a roadmap for Y", "break this feature area into issues", "set up an epic for Z", or describes wanting to start a new initiative or phase of work that needs to become trackable GitHub issues before anyone — human, or an automated coding bot — starts building. Companion to the epic-orchestrator skill, which drives an already-created epic to completion: use this one first, that one second.
---

# Epic Planner

Your output here is a document and a handful of GitHub issues, not code. The value of this skill is entirely in *not guessing* — every claim in the roadmap should be traceable to something you actually checked, and every issue should hand the next person (or bot) a decision that's already made, or an honestly-open question, never a silent assumption dressed up as fact.

## 1. Ground the roadmap in what's actually there

Before writing a single line of the roadmap, go find out what already exists — grep the codebase for the relevant types/interfaces, check for stray branches or PRs that might already contain related work under a different name, and verify any external claim (a provider's API shape, a library's capability) against real documentation rather than training-data recall. A roadmap built on "I think we have X" instead of "I grepped for X and found it at path:line" will misdirect every issue that inherits its assumptions.

Two things worth actively hunting for, because they're the ones that bite:

- **Work that shipped somewhere other than the main branch.** A closed issue and a merged PR don't guarantee the code reached where people expect it — check what branch that PR actually targeted.
- **A claim that would change scope if wrong.** If your roadmap says "we don't have chunking" or "prompt caching isn't exposed," that single sentence determines whether an entire phase item exists — verify it directly, don't repeat it from memory or from an old doc.

## 2. State the prioritization filter explicitly, then use it

Ask the user (or infer from their framing) what makes something in-scope at all for this initiative. Write that filter as one literal sentence at the top of the roadmap — not "important things first" but a test you can apply to any candidate item, e.g. "does this wrap a provider API surface, or shape the payload we send to a model?" A concrete filter does two things a vague one can't: it justifies *why* an item is deferred (a paragraph, not a vibe), and it gives future contributors a way to evaluate new candidates themselves without re-asking you.

Apply it explicitly to the boundary cases, not just the obvious yeses — an "out of scope" section with one reason per item is worth as much as the phases themselves, because it's what stops someone re-proposing the same rejected idea in six months.

## 3. Write the roadmap, then stop and show it

Structure: the filter, a "what exists today" inventory (with file:line evidence for the load-bearing claims), phases ordered by value-per-unit-of-work under the filter (not by dependency order alone — a phase with no prerequisites and high value goes first even if a later phase is "more foundational" in the abstract), and the out-of-scope table.

Save it somewhere durable (a docs/memory file the team already uses, not a throwaway scratch file) and **stop there** — present it and ask whether to proceed to issue creation, adjust the phasing, or stop at "just the document" for now. Turning a roadmap into a GitHub epic is a bigger, more visible commitment than writing a doc; don't make that jump without the user actually looking at the phasing first.

## 4. Before touching GitHub, verify identity

Same discipline as epic-orchestrator: `gh api user --jq .login` before creating anything, `gh auth switch --hostname github.com --user <name>` if it's wrong. Issues and epics are attributed to whoever's authenticated — get this right before the first `gh issue create`, not after.

## 5. Re-verify the roadmap's load-bearing claims one more time

Between "roadmap written" and "issues opened," something may have changed, or you may simply have been wrong the first time. Re-check anything the roadmap treats as a blocker or a scope boundary — cheap now, expensive once it's baked into six issue bodies that a bot starts implementing against. If you find a correction here, make it before creating anything, and say so plainly rather than quietly opening issues that match what you now know rather than what you wrote.

## 6. Create the epic, then the children

One tracking issue for the phase (or whichever unit the user wants tracked), referencing the roadmap doc rather than duplicating it. Then one child issue per work item, each with:

- **A stated dependency, if one exists** — "must not start until #N merges," not left implicit. If two items are independent, say that too; it's what lets a later orchestration process run them in parallel safely.
- **Either a settled design decision or an explicit open question** — never an implicit assumption. If you already know the answer to something ambiguous (a data-shape choice, a naming call), write it as a decision. If you don't, write the actual fork in the road as a question in the issue body — a future reader (bot or human) should be able to tell the difference between "this is decided, don't relitigate it" and "this genuinely needs an answer."
- **What's explicitly deferred and why**, if the item's scope was trimmed relative to some larger version of it (e.g. "richer source types go through the escape hatch for now, first-class support is a later phase").

Link children to the epic via the platform's native mechanism if one exists (e.g. GitHub sub-issues) rather than only prose references — it gets you progress tracking for free and survives issue renumbering better than a manually-maintained checklist.

See `references/templates.md` for skeleton issue bodies if you want a starting structure rather than composing from scratch each time.

## 7. Do not auto-trigger implementation

If this project uses a label or similar mechanism to kick off automated implementation (see epic-orchestrator), **do not apply it** when creating issues, even if all six items are ready to start in principle. Surface that as the user's decision explicitly ("none of these are labelled yet — tell me which ones to start, or I can label them myself if you want them all going"). Opening six issues is reversible browsing; kicking off six unattended implementation runs at once is not something to default into.

## 8. Ask what needs clarifying — proactively, not just when stuck

Once the issues exist, go back through them and identify the two or three decisions that would most change what gets built if answered differently, and ask about those specifically rather than a generic "any questions?" A batch of targeted questions up front is worth far more than discovering the ambiguity three issues deep once a bot has already started guessing.

## Correction discipline

When something you wrote turns out wrong — a roadmap claim, an issue's premise — fix it with a visible retraction in the artifact itself ("earlier I wrote X; that was wrong, here's why, here's the correction"), not a silent edit. Anyone re-reading the issue later should be able to see that a correction happened and trust the current text, rather than wonder what else might be stale.
