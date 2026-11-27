---
name: speckit-local-full
description: Run the whole GitHub Spec Kit cycle (specify, plan, tasks, implement) unattended for a range of the numbered backlog files in features/, one feature at a time, each step in its own subagent, with Codex answering the clarification questions instead of the user, then verify, review, squash and close each feature on develop. Use whenever the user asks to run, build, process, implement or "do" backlog features by number or range ("speckit-full 00-03", "run features 2 to 5", "implement feature 07 end to end", "continue the backlog"), or to resume a feature run that stopped part-way, even if they do not name Spec Kit.
argument-hint: "Feature range, e.g. 00-03, 5, 02,04 or all; optional models: step=model ..."
compatibility: "Spec Kit project (.specify/), the speckit-* skills, the Codex CLI logged in, PowerShell 7, git, .NET SDK"
---

# speckit-local-full

Takes numbered backlog files from `features/` and drives each one through Spec Kit to a
single squashed commit on `develop`. The user is not at the keyboard while this runs, which
shapes everything below: nothing may wait for a human, and anything that cannot be finished
is left in a state the user can inspect.

## Requested range

```text
$ARGUMENTS
```

The range comes from the user's prompt (`00-03`, `5`, `02,04 07`, `all`). If the prompt
gives none, ask for it once before starting. That is the only question this skill asks.

After the range the prompt may name the Claude model for some steps, for example
`02-03 models: specify=opus tasks=haiku implement=sonnet`. Steps left out keep their
default; see "Models per step".

## Models per step

Every subagent is started with the `model` parameter of the `Agent` tool set from this
map. Never leave it out: an omitted model silently inherits the session's model.

| Step key | Used for | Default |
|---|---|---|
| `constitution` | filling the constitution in preflight | `opus` |
| `specify` | the specify step | `opus` |
| `plan` | the plan step, including a re-run of `speckit-research-verify` | `opus` |
| `tasks` | the tasks step | `sonnet` |
| `implement` | each implement subagent | `auto` |
| `fix` | verify fixes, mutation fixes, review fixes | `sonnet` |
| `review` | full reviews and the confirmation review | `opus` |

The defaults live in `.claude/skills/speckit-local-full/models.json`. Allowed models are `opus`,
`sonnet`, `haiku` and `fable`; `auto` is allowed for `implement` only.

`implement=auto` picks one model per phase of `tasks.md`, from the complexity the tasks step
records under each phase heading (`use-feature.ps1` returns it in `phases`):

| Phase complexity | First attempt |
|---|---|
| `low` | `haiku` |
| `medium`, `high`, or no marker | `sonnet` |

A phase attempt that leaves tasks of that phase open, or ends with `verify.ps1` failing, is
retried on the next model up (`haiku` → `sonnet` → `opus`). With a fixed implement model
the retry uses the same model. Three attempts on a phase whose open count does not drop
stop the run.

Keep a log of the model each subagent ran on, escalations included; it goes in the final
report.

## Your role: orchestrator

You run the loop, the scripts and the commits. Every Spec Kit step, every fix and the review
run in a subagent (`Agent` tool, `general-purpose`, `model` from "Models per step"), one at a
time and never in parallel: they share one working tree and one `.specify/feature.json`.

Your prompts carry facts, findings and the rules; they never carry a product judgment of
your own. "This case cannot be tested", "the user would want X", "keep it simple and skip
Y" are product decisions: if one is needed, it is a question for Codex like any other, and
an existing Codex answer is never overridden by your wording (ADR 0003 records what that
cost).

Keep your own context small, because a range can be many features long: do not read specs,
plans, code or build logs yourself. Ask each subagent for a short report and act on that.

All scripts live in `.claude/skills/speckit-local-full/scripts/` and are run from the repository
root as `pwsh -NoProfile -File .claude/skills/speckit-local-full/scripts/<name>.ps1 ...`. They
print JSON.

## 1. Preflight

0. Run `resolve-models.ps1 -Arguments "<the whole argument text>"`. It prints the model for
   each step, defaults merged with the user's `models:` part. If it fails (unknown step or
   model), stop and report its message: a typo in the models must not silently fall back.
1. Run `resolve-features.ps1 -Range "<range>"`. It returns the features in order, with
   `nextStep` for each (`specify`, `plan`, `tasks`, `implement`, `verify` or `done`). A bad
   range fails with a message that lists the valid numbers.
2. Stop and report if `cleanTree` is false and the current branch is not the feature branch
   of the first unfinished feature: uncommitted work of unknown origin must not be swept
   into a feature commit.
3. Run `check-codex.ps1`. It sends Codex a one-word request, because `codex login status`
   says "Logged in" even when the token has expired. If it fails, stop and tell the user to
   run `codex login`: the clarification answers depend on Codex, and finding that out in
   the middle of feature one wastes the run.
   Codex answers with the model `gpt-6.2-sol` (the default in `scripts/common.ps1`). If the
   user names another Codex model for this run, pass `-Model <slug>` to `check-codex.ps1`
   and add the same `-Model <slug>` to the `ask-codex.ps1` command in the specify prompt.
4. If `.specify/memory/constitution.md` still contains template placeholders such as
   `[PROJECT_NAME]`, fill it first: on `develop`, spawn a subagent (model `constitution`) that invokes the
   `speckit-constitution` skill with principles drawn from the "Fixed constraints" and
   cross-cutting decisions of `features/README.md` plus the standing rules below, then
   commit with `git commit --no-gpg-sign -m "docs: ratify project constitution"`. Spec Kit's
   plan step checks every design against the constitution, so an empty one makes that gate
   meaningless.

Features are in dependency order, so process them in ascending order and skip those whose
`nextStep` is `done`.

## Standing rules passed to every subagent

Put this block, verbatim, in every subagent prompt:

```text
Standing rules for this run:
- Nobody is available to answer questions. Never wait for user input, and do not settle on
  your own a choice that belongs to the product owner. When you have a question (a step
  tells you to ask the user, or the backlog and the spec leave a choice open), ask Codex:
  write the question with its context and the options you see to
  <specDir>/decisions/questions-<topic>.md (a filled copy of
  .claude/skills/speckit-local-full/templates/questions-template.md), run
  pwsh -NoProfile -File .claude/skills/speckit-local-full/scripts/ask-codex.ps1
  -QuestionsFile <specDir>/decisions/questions-<topic>.md
  -AnswersFile <specDir>/decisions/answers-<topic>.md -FeatureFile <file>
  (add -SpecFile <specDir>/spec.md once the spec exists; allow it up to 10 minutes), and
  act on the answer. Keep both files and name them in your report. If this prompt gives a
  more specific instruction for a case, follow that one.
- Architecture and harness are not yours to change, and not Codex's either. That covers
  docs/architecture-rules.md, architecture-rules.json, the architecture tests,
  Directory.Build.props, the analyzers in Directory.Packages.props, BannedSymbols.txt,
  .editorconfig, stryker-config.json, global.json, .specify/ (constitution, templates,
  extensions) and everything under .claude/skills/. It also covers a decision that departs
  from the layout or the reference rules without touching a file. If the work cannot be
  finished without such a change, change nothing: write an Architecture Decision Record
  as docs/adr/NNNN-<short-title>.md, a filled copy of
  .claude/skills/speckit-local-full/templates/adr-template.md (status Proposed),
  stop at once, and put the ADR path first in your report.
- features/README.md (fixed constraints, decisions D1 to D35) and the feature's own backlog
  file are settled. Follow them; do not reopen them.
- Warnings are errors. Directory.Build.props at the repository root turns on warnings as
  errors, nullable reference types, the recommended .NET analyzers and build-time code
  style; .editorconfig sets the style rules. Both already exist: build on them, never
  replace or relax them. Fix the cause of a diagnostic; do not silence it with NoWarn,
  #pragma warning disable, SuppressMessage or a severity override unless the spec itself
  requires the exception. verify.ps1 fails when these settings are weakened.
- BannedSymbols.txt lists APIs the build rejects (RS0030), each with its replacement:
  the system clock (inject TimeProvider), blocking on tasks (await), Thread.Sleep
  (Task.Delay), Console (ILogger). Use the replacement; do not edit the list. Template
  code that uses a banned API (the Blazor sample pages do) is removed or rewritten.
- The Sonar C# rules (Sxxxx) run in the build through SonarAnalyzer.CSharp and fail it like
  any other diagnostic: empty catch blocks, TODO comments, hard-coded credentials, unused
  members, nested ifs that should merge, and so on. The message says what to change. The
  `dotnet new` templates trip a few of them (empty class, empty test, `app.Run()` instead
  of `await app.RunAsync()`): fix the template code, do not disable the rule.
- Security rules are on in full: SQL text built from strings (CA2100, S2077), weak hashes,
  non-cryptographic randomness used for secrets, XML external entities, hard-coded
  credentials. Use parameters, strong algorithms and RandomNumberGenerator. The one place
  where the user's own query text is executed as written (decision D21) may carry a single
  suppression on that statement, with a justification naming D21, and only if the spec for
  that feature says so. NuGet's vulnerability audit also fails the restore (NU1901 to
  NU1904) for a package with a known vulnerability: pick a fixed version.
- Package versions live only in Directory.Packages.props (central package management).
  After `dotnet new`, move each Version from the project file into a <PackageVersion>
  there, or the restore fails with NU1008.
- docs/architecture-rules.md fixes the layout (src/, tests/, four projects), which project
  may reference what, and the type rules A1 to A5 that Jarvis.Dataset.Etl.Tests must check
  with ArchUnitNET tests. Read it before creating a project, a folder or a reference. Do
  not edit architecture-rules.json or remove an architecture test to get a check through;
  if a rule really has to change, that is an Architecture Decision Record and a stop, as
  above.
- Unit tests are mandatory for new behaviour. Integration tests skip themselves when their
  database environment variable is missing; they must not fail for that reason.
- Do not run git commit, switch, merge or branch commands: the orchestrator owns git.
- Finish with a report of at most 15 lines: what you produced (paths), what is unfinished,
  and anything the orchestrator must know. No file dumps.
```

`<specDir>` and `<file>` in the block are the feature's own values; fill them in.

## Questions go to Codex; architecture and harness changes stop the run

These two rules bind you, the orchestrator, exactly as they bind the subagents.

**A question is answered by Codex, never by the user and never by you alone.** That
includes your own questions: whether a reviewer's finding that "depends on the caller's
judgment" is blocking, which of two options a subagent left open, whether a non-blocking
finding should be fixed now. Write it to `<specDir>/decisions/questions-<topic>.md`, run
`ask-codex.ps1` as in the standing rules, act on the answer, and commit both files with the
step they belong to. Do not put the question to the user in a status message and carry on:
nobody reads it in time. If `ask-codex.ps1` fails twice, take the option most consistent
with `features/README.md` and the feature file and record it in the answers file under
"Answered by fallback (Codex unavailable)".

**A change to the architecture or the harness is nobody's to make during a run.** The list
is in the standing rules; for you it also means the limits and gates of this skill (the
number of fix and review rounds, the mutation threshold, what counts as blocking) and its
scripts. Codex cannot approve such a change: `ask-codex.ps1` tells it to answer "Needs ADR"
instead. When you or a subagent reach that point:

1. Make sure the change itself was not made. Revert it if a subagent made it.
2. Write, or check that the subagent wrote, `docs/adr/NNNN-<short-title>.md`, numbered after
   the highest existing one (start at `0001`), as a filled copy of
   `.claude/skills/speckit-local-full/templates/adr-template.md`: every heading kept, in order.
3. Commit it on the feature branch as `docs(<id>): ADR NNNN <short title>` and stop the run
   as section 3 describes. The user accepts or rejects the record; only then is the change
   applied, by the user or by a run the user starts for it.

A problem that tests or feature code can fix is not an architecture decision: fix it. The
record is for the cases where the only ways forward change a rule, a gate or the tooling.

## Templates

Every document this skill writes outside Spec Kit's own artifacts is a filled copy of a
template in `.claude/skills/speckit-local-full/templates/`, so that the records of different
features and runs read alike. Keep every heading, in the template's order; write "none"
under a heading with nothing to say instead of dropping it.

| Document | Template | Written by |
|---|---|---|
| `docs/adr/NNNN-<short-title>.md` | `adr-template.md` | whoever hits the change (subagent or orchestrator) |
| `<specDir>/decisions/questions-<topic>.md` | `questions-template.md` | whoever has the question |
| `<specDir>/review.md` | `review-template.md` | orchestrator, from the last reviewer's report |

The spec, plan, tasks and checklists keep the templates under `.specify/templates/`; the
clarification questions of the specify step keep that skill's own format. If a run needs a
kind of document that has no template, that is a harness change: Architecture Decision
Record, not an improvised format.

## 2. Per feature

For each unfinished feature `NN` (values come from the resolve output: `id`, `title`,
`file`, `branch`, `specDir`):

### 2.1 Activate

Run `use-feature.ps1 -Number NN`. It creates or checks out `feature/<id>` from `develop` and
writes `.specify/feature.json`, which is how Spec Kit's plan, tasks and implement commands
find the feature. Run it again before each step after any interruption. Start from the
`nextStep` it reports, so a resumed feature does not redo finished steps.

### 2.2 The four Spec Kit steps

Run them in order, one subagent each (model `specify`, `plan`, `tasks`; implement as below).
After each one returns, check that its artifact exists, then commit on the feature branch:

```
git add -A
git commit --no-gpg-sign -m "<step>(<id>): <short summary>"
```

`--no-gpg-sign` is required on every commit this skill makes: the user is not present to
unlock a signing key, and a signing prompt would hang the run.

| Step | Skill to invoke | Artifact that must exist | Commit prefix |
|---|---|---|---|
| specify | `speckit-specify` | `<specDir>/spec.md` with no `[NEEDS CLARIFICATION` left | `spec` |
| plan | `speckit-plan` | `<specDir>/docs-verification.md` and `<specDir>/plan.md` | `plan` |
| tasks | `speckit-tasks` | `<specDir>/tasks.md` | `tasks` |
| implement | `speckit-implement` | every task in `tasks.md` checked | `impl` |

Subagent prompt, common part for all four:

```text
You are running one step of GitHub Spec Kit for backlog feature <NN> "<title>" in this
repository. Invoke the `<skill>` skill with the Skill tool and carry it out completely. If
the Skill tool is unavailable, read .claude/skills/<skill>/SKILL.md and follow it.

Feature directory: <specDir> (already recorded in .specify/feature.json)
Backlog source: <file>, plus features/README.md

<standing rules block>

<step-specific part>
```

**specify**: pass as the skill's arguments:

```text
Feature <NN>: <title>. The full description is <file>; read it together with
features/README.md and specify exactly that feature, no more.
SPECIFY_FEATURE_DIRECTORY=<specDir>
```

and add to the prompt:

```text
Clarifications: if [NEEDS CLARIFICATION] markers remain and the skill tells you to present
questions and wait for the user, do this instead:
1. Write the questions, in the skill's own format (context, question, options table), to
   <specDir>/clarifications/questions.md.
2. Run: pwsh -NoProfile -File .claude/skills/speckit-local-full/scripts/ask-codex.ps1
   -QuestionsFile <specDir>/clarifications/questions.md
   -AnswersFile <specDir>/clarifications/answers.md
   -FeatureFile <file> -SpecFile <specDir>/spec.md
   (allow it up to 10 minutes).
3. Treat the answers file as the user's reply: replace each marker with the answer and
   continue the skill's validation.
4. If the script fails, retry once. If it fails again, choose for each question the option
   most consistent with features/README.md and <file>, and write your choices to the
   answers file under the heading "Answered by fallback (Codex unavailable)".
Keep both files: they are the user's audit trail of what was decided for them. Say in your
report how many questions were raised and who answered them.
```

**plan** and **tasks**: no arguments are needed. Add to the tasks prompt:

```text
The tasks template says tests are optional. In this project they are not (constitution,
standing rules): every piece of new behaviour gets test tasks, written before the code they
cover. Do not edit the template.
Every functional requirement needs an automated test that would fail if the behaviour were
removed; a manual check does not count. End tasks.md with a coverage table FR -> test task,
with no empty cell.
Directly under every "## Phase <n>: <title>" heading, write one line
"Complexity: low|medium|high - <reason in a few words>". It chooses the model that
implements the phase. low: mechanical work that follows an existing pattern (scaffolding,
wiring, renames, documentation, tests of code that already exists). high: a new abstraction
or project, security or secret handling, concurrency, schema migrations, or behaviour the
spec describes with many edge cases. medium: everything else. When unsure, choose the higher
level.
Honour every "corrected" row of <specDir>/docs-verification.md and turn the open items of
research.md into tasks. The last task runs
pwsh -NoProfile -File .claude/skills/speckit-local-full/scripts/verify.ps1 until it passes.
Write tasks as checkboxes (`- [ ] T001 ...`) so progress can be counted.
```

Add to the plan prompt:

```text
The technology choices are fixed by features/README.md and <file>; record them, do not
re-evaluate them.
The plan skill has a mandatory before_plan hook, speckit-research-verify (registered in
.specify/extensions.yml). Run it first, exactly as the skill instructs: it checks the
feature's technical assumptions against the Microsoft Learn and Context7 MCP servers and
writes <specDir>/docs-verification.md with the files to touch. Load those MCP tools with
tool search if they are not already available. Then write the plan from that file: a
"corrected" row overrides the spec and your memory, "Files to touch" seeds the project
structure, and "unverified" rows go into research.md as open items. In your report, give
the confirmed / corrected / unverified counts.
```

If `docs-verification.md` is missing after the plan subagent returns, the hook was skipped:
spawn a subagent (model `plan`) to run `speckit-research-verify` and then revise `plan.md` against it,
before committing. A plan built on unchecked assumptions is the failure this hook exists to
prevent.

**implement** runs one subagent per phase of `tasks.md`, in phase order, using the `phases`
list from `use-feature.ps1` (phase number, title, complexity, first and last task, open
count). Skip phases with no open task. The model comes from "Models per step"; with `auto`
it follows the phase's complexity. Add to the prompt:

```text
Implement only Phase <n> "<phase title>" of tasks.md (tasks <first> to <last>); the earlier
phases are done. Do not start a task of a later phase.
If the skill stops to ask whether to proceed with incomplete checklists: complete the
checklist items yourself when they are about the spec's own quality; otherwise proceed and
name the open items in your report.
Mark each task [x] in tasks.md as you finish it. Before you report, run
pwsh -NoProfile -File .claude/skills/speckit-local-full/scripts/verify.ps1 and fix what it
reports. If you run out of room before the phase is done, stop at a task boundary and
report which task comes next.
```

After each phase subagent returns, run `use-feature.ps1 -Number NN` again and commit as
`impl(<id>): phase <n> <short title>`. If that phase still has open tasks, or the
subagent reported `verify.ps1` failing, spawn the phase again as "Models per step" says
(escalating under `auto`). When every phase is done, `nextStep` is `verify`.

A `tasks.md` without "## Phase" headings is implemented by a single subagent, with the
implement model (`sonnet` under `auto`), repeated while `tasksOpen` drops.

### 2.3 Verify

Run `verify.ps1` yourself. It checks the project-level architecture rules
(`architecture.ps1`: layout, approved projects, references, cycles, presence of the
architecture tests), builds the solution with warnings as errors, runs the tests (the
ArchUnitNET architecture tests among them), and prints a verdict (`ok`, `problems`, `details`); full logs go to the paths in `logs`.

If `ok` is false, spawn a fix subagent (model `fix`) with the `problems` and `details` and the log paths,
telling it to fix the causes and rerun `verify.ps1` until it passes. Commit as
`fix(<id>): ...` and verify again yourself. Three failed rounds stop the run.

### 2.4 Mutation testing

Run `mutation.ps1 -Since develop` yourself once `verify.ps1` passes (allow it 20 minutes).
Green tests only show that the tests run; this step changes the feature's code in small
ways and checks that some test notices. It covers only the code changed on the feature
branch, through the unit-test projects, and passes at the threshold in
`stryker-config.json` (70%).

If `ok` is false, spawn a fix subagent (model `fix`) with `problems` and the `survivors` list (each entry
is file, line, the original code and the change no test caught), telling it to add or
strengthen unit tests until `mutation.ps1` passes, without touching `stryker-config.json`
and without removing the code. Commit as `test(<id>): kill surviving mutants`, then run
`verify.ps1` and `mutation.ps1` again. Two failed rounds stop the run.

Some survivors are equivalent mutants: the change cannot alter behaviour, so no test can
catch it. That is why the threshold is not 100%; do not chase them once the step passes.
Pass the final `survivors` list to the reviewer.

### 2.5 Review

Spawn a reviewer subagent (model `review`) that changes nothing:

```text
Review the work on branch <branch> for backlog feature <NN> "<title>". Do not edit files.
Read <file>, features/README.md, <specDir>/spec.md, plan.md and tasks.md, then the diff
`git diff develop...HEAD` (skip the files under specs/).

Report findings in two lists:
BLOCKING: a requirement or success criterion of the spec that is not implemented; behaviour
that contradicts the backlog decisions; a bug; new behaviour without a unit test; a warning
silenced instead of fixed; a task marked done that is not done; a change to
architecture-rules.json, Directory.Build.props, Directory.Packages.props' analyzers,
BannedSymbols.txt, .editorconfig or stryker-config.json, or an
architecture test weakened or removed, that the spec does not call for; code that ignores a
"corrected" row of <specDir>/docs-verification.md.
NON-BLOCKING: everything else worth saying.
Mutants that survived mutation testing: <survivors list, or "none">. For each, say whether
it is an equivalent mutant or a real gap in the tests; a real gap is NON-BLOCKING unless it
leaves a requirement of the spec unchecked.
For each finding: file and line, what is wrong, what would fix it. If nothing is blocking,
say "BLOCKING: none".
```

If there are blocking findings, spawn a fix subagent (model `fix`) with them, commit as
`fix(<id>): review findings`, run `verify.ps1` and `mutation.ps1` again, then run the review
once more. Two
review rounds that still end with blocking findings stop the run. When the reviewer makes a
finding's blocking status depend on your judgment, ask Codex and take its answer as the
status; do not decide it yourself and do not add a round of your own.

One exception (ADR 0002): when the second round's blocking findings are all fixed by
test-only changes (no file under `src/` changes behaviour), run one confirmation review
limited to those findings (model `review`): the reviewer checks that each is fixed and that the fix
introduced nothing blocking. If it reports "BLOCKING: none", the feature may close;
otherwise the run stops. This is not a third full review and it cannot be repeated.

Write the last review to `<specDir>/review.md` as a filled copy of
`.claude/skills/speckit-local-full/templates/review-template.md` and commit it, so the squashed
commit carries what was knowingly left as non-blocking. Tell the reviewer to order its
report by that template's headings, so the record is a transcription and not a rewrite.

### 2.6 Close

Only when `verify.ps1` and `mutation.ps1` pass and the review has no blocking finding:

```
pwsh -NoProfile -File .claude/skills/speckit-local-full/scripts/close-feature.ps1 -Number NN -BodyFile <file>
```

Write the body file first (outside the repository, e.g. the temp folder): three to six
lines on what the feature delivers, the number of clarification questions and who answered
them, the models that ran its steps, and any commit trailers this session is required to
add. The script squashes the
branch into one commit on `develop` with the subject `feat(<id>): <title>`, commits without
signing, and deletes the feature branch. Do not reword the subject: `resolve-features.ps1`
recognises a closed feature by it.

Nothing is pushed. Pushing is the user's call.

Then go on to the next feature.

## 3. When something cannot be finished

Stop the whole run at the first feature that cannot be closed. Later features depend on
earlier ones, so carrying on would build on something broken.

Leave the feature branch as it is, with everything committed (`wip(<id>): ...`,
`--no-gpg-sign`), and do not merge it. Stay on that branch. A later run with the same range
resumes there.

When the cause is a change to the architecture or the harness, the stop comes with an
Architecture Decision Record under `docs/adr/` (see "Questions go to Codex" above), and the
record is what the user reads first.

## 4. Final report

End with a table, one row per feature in the range:

| Feature | Result | Commit on develop | Clarifications | Models | Notes |
|---|---|---|---|---|---|

`Result` is `closed`, `already done`, `stopped at <step>` or `not started`. Under
`Clarifications` give the count and who answered (Codex or fallback). Under `Models` give
the model per step in short form (`spec opus, plan opus, tasks sonnet, impl haiku×3
sonnet×5, fix sonnet, review opus`) and name every escalation, so the user can see where
a cheaper model was not enough. Below the table, for a
stopped feature, state the branch, the failing check and the log path, and the path of the
Architecture Decision Record when there is one. Mention that the answers Codex gave are in
`<specDir>/clarifications/answers.md` and `<specDir>/decisions/` for review, and list in one
line each the decisions Codex took outside the specify step.
