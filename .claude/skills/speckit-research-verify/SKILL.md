---
name: speckit-research-verify
description: Verify a feature's technical assumptions against Context7 and Microsoft Learn and list the files it will touch
compatibility: Requires spec-kit project structure with .specify/ directory
metadata:
  author: jarvis-dataset-etl
  source: research:commands/speckit.research.verify.md
  version: 1.0.0
user-invocable: true
disable-model-invocation: false
---

# Verify Assumptions Before Planning

Runs as the `before_plan` hook. A plan written from memory repeats whatever the model
remembers about .NET, EF Core, Blazor and the other libraries, and that memory is often a
version or two behind. This command replaces those assumptions with what the current
documentation says, and works out which files the feature has to create or change, so the
plan that follows starts from checked facts.

## User Input

```text
$ARGUMENTS
```

## Steps

1. **Locate the feature.** Run
   `.specify/scripts/powershell/check-prerequisites.ps1 -Json -PathsOnly` from the
   repository root and take `FEATURE_DIR` and `FEATURE_SPEC`. Read the spec. If the
   repository has a `features/` backlog, also read `features/README.md` and the backlog
   file this spec was written from: their decisions are settled and are not up for
   re-evaluation here, only for verification that they can be implemented as stated.

2. **List the assumptions.** Write down every technical statement the plan will rely on:
   framework and library names and versions, APIs to call, configuration keys, hosting and
   packaging options, tooling commands, default behaviours. Aim for the ones that would
   send the implementation the wrong way if they were false; skip general knowledge.

3. **Verify each one against documentation.**
   - Microsoft technologies (.NET, ASP.NET Core, Blazor, EF Core, MSBuild, SQLite provider,
     hosting, Windows services, analyzers): use the Microsoft Learn MCP tools,
     `microsoft_docs_search` first, `microsoft_code_sample_search` for code,
     `microsoft_docs_fetch` when a page has to be read in full.
   - Every other library (NUnit, NSubstitute, bUnit, Serilog, Npgsql, Oracle provider,
     OpenTelemetry, Excel writers, and so on): use the Context7 MCP tools,
     `resolve-library-id` then `query-docs`.
   - These tools may be deferred: load them with tool search before the first call.
   - Check the version the project targets, not the latest one, when they differ.
   - If neither server is reachable, do not fall back to memory silently. Record each
     assumption as `unverified` with the reason, so the plan and the reviewer know.

4. **Map the files.** Inspect the repository as it is now (solution, projects,
   `Directory.Build.props`, `Directory.Packages.props`, existing folders) and list the
   files this feature has to create and the existing files it has to modify, each with one
   line on why. Name real paths; where a path depends on a choice the plan still has to
   make, say so instead of inventing one. Every path must fit
   `docs/architecture-rules.md` (layout, approved projects, allowed references): read it
   first, and if the feature cannot be built inside those rules, say so under
   "Unverified" instead of planning around them.

5. **Check the build rules.** Warnings are errors in this project. Confirm that
   `Directory.Build.props` sets `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>`; if
   the file or the setting is missing, add it to the files list as something this feature
   must create. For every API you verified, note any obsolete or analyzer warning the
   documentation attaches to it and the replacement to use, because under this rule a
   deprecated call fails the build.

6. **Write `FEATURE_DIR/docs-verification.md`** with exactly these sections:

   ```markdown
   # Documentation verification: [feature name]

   ## Assumptions
   | # | Assumption | Verdict | Source | What the plan must do |
   |---|------------|---------|--------|-----------------------|

   ## Files to touch
   | Path | Create / Modify | Why |
   |------|-----------------|-----|

   ## Build rules
   - Warnings as errors: [configured / must be added by this feature]
   - APIs to avoid because they warn: [list, with replacement]

   ## Unverified
   [Assumptions that could not be checked, and why. "None" if empty.]
   ```

   `Verdict` is one of `confirmed`, `corrected` (the documentation says something
   different: state what), or `unverified`. `Source` is the documentation URL or the
   Context7 library id.

## Handing over to the plan

Do not write `plan.md` or `research.md`: the plan command does that next. It must treat
`docs-verification.md` as input: every `corrected` row overrides what the spec or memory
suggested, the "Files to touch" table seeds the plan's project structure, and each
`unverified` row is carried into the plan's research phase as an open item.

Report in at most ten lines: how many assumptions were confirmed, corrected and left
unverified, the corrections in one line each, and the path of the file.