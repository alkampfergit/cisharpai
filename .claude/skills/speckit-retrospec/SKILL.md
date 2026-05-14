---
name: "speckit-retrospec"
description: "Reverse-engineer speckit specification files (spec.md, plan.md, and supporting artifacts) from an already-implemented feature in the codebase. Use when the code exists but no speckit specs do. Tasks.md generation is skipped by default (add '--tasks' to include it)."
argument-hint: "Feature name or description, optionally with key file paths"
compatibility: "Requires spec-kit project structure with .specify/ directory"
metadata:
  author: "cisharpai"
  source: "custom"
user-invocable: true
disable-model-invocation: false
---

# speckit-retrospec — reverse-engineer specs from existing code

Generate the speckit artifact bundle (`spec.md`, `plan.md`,
`data-model.md`, `contracts/`, `research.md`, `quickstart.md`) by analyzing
an already-implemented feature.

**tasks.md generation is SKIPPED by default** because retrospec is about
capturing design intent, not recreating an implementation checklist. Include
`--tasks` in the user input to opt in. When generated, every task checkbox
is pre-marked `[x]` because the code already exists.

## User Input

```text
$ARGUMENTS
```

You **MUST** consider the user input before proceeding (if not empty).

## Pre-Execution Checks

**Check for extension hooks (before retrospec)**:
- Check if `.specify/extensions.yml` exists in the project root.
- If it exists, read it and look for entries under the `hooks.before_retrospec` key.
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally.
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- When constructing slash commands from hook command names, replace dots (`.`) with hyphens (`-`).
- For each executable hook, output based on its `optional` flag:
  - **Optional hook** (`optional: true`): Show prompt and command for user to execute.
  - **Mandatory hook** (`optional: false`): Execute automatically via `EXECUTE_COMMAND`.
- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently.

## Outline

### Step 1: Parse input and identify the feature

The text the user typed after `/speckit-retrospec` is the feature description or
identifier. It may be:

- A **feature name** (e.g., "streaming chat", "tool calling", "JSON output")
- A **feature name + key files** (e.g., "streaming chat — see IStreamingChatFeature, StreamingChatExtensions")
- A **file path or glob** (e.g., "src/Cisharpai/Features/Streaming/")
- A **description** (e.g., "the feature that allows clients to stream chat completions token by token")

If the input is empty, ERROR: "No feature description provided. Usage: `/speckit-retrospec <feature-name-or-description> [-- key-file-paths]`"

### Step 2: Generate short name and create spec directory

1. **Generate a concise short name** (2-4 words, kebab-case) from the feature description.
   - Use action-noun format when possible (e.g., "streaming-chat", "tool-calling", "json-output")
   - Preserve technical terms and acronyms

2. **Create the spec feature directory**:
   - Check `.specify/init-options.json` for `branch_numbering`
   - If `"timestamp"`: prefix is `YYYYMMDD-HHMMSS`
   - If `"sequential"` or absent: prefix is `NNN` (next available 3-digit number after scanning existing directories in `specs/`)
   - Construct: `specs/<prefix>-<short-name>` (e.g., `specs/001-streaming-chat`)
   - `mkdir -p SPECIFY_FEATURE_DIRECTORY`
   - `mkdir -p SPECIFY_FEATURE_DIRECTORY/checklists`
   - `mkdir -p SPECIFY_FEATURE_DIRECTORY/contracts`
   - Persist to `.specify/feature.json`:
     ```json
     {
       "feature_directory": "<resolved feature dir>"
     }
     ```

### Step 3: Discover the implementation

This is the core reverse-engineering step. Analyze the codebase to build a
complete picture of the feature's implementation.

**3a. Identify relevant source files**

Use the feature description and any provided file paths to locate ALL files
related to this feature. Search strategies (use all that apply):

- **Direct paths**: If the user provided file/directory paths, start there.
- **Grep for feature keywords**: Search for class names, interface names,
  method names, and domain terms from the feature description.
- **Follow the dependency graph**: From identified entry points, trace:
  - Interfaces and their implementations
  - Models/DTOs used by the feature
  - Tests covering the feature
  - DI registration code
  - Configuration/options classes
  - Helper/utility classes specific to the feature
- **Check feature interfaces**: Look in `src/Cisharpai/Features/` for matching
  feature interface definitions.
- **Check provider implementations**: Look in each provider project for
  implementations of the feature interface.
- **Check tests**: Look in `src/Cisharpai.Tests/` for test classes related
  to the feature.
- **Check wiki**: Look in `wiki/` for documentation about the feature.

**3b. Read and analyze all relevant files**

For each identified file, read it and extract:
- **Purpose**: What role does this file play in the feature?
- **Public API**: What interfaces, classes, methods, properties are exposed?
- **Dependencies**: What does this file depend on?
- **Patterns**: What design patterns are used (feature collection, builder, factory, etc.)?
- **Tests**: What scenarios are tested?

**3c. Build a feature map**

Organize findings into:
- **Core abstractions**: Interfaces, base classes, enums
- **Provider implementations**: Per-provider classes
- **Models/DTOs**: Request/response types, options classes
- **Infrastructure**: DI registration, configuration, helpers
- **Tests**: Unit tests, integration tests, test fakes
- **Documentation**: Wiki pages, XML docs

Present the discovered feature map to the user and ask for confirmation
before proceeding:

```
## Discovered Feature Map: [Feature Name]

**Core files** (N files):
- path/to/file.cs — brief description

**Provider implementations** (N files):
- path/to/file.cs — brief description

**Models** (N files):
- path/to/file.cs — brief description

**Tests** (N files):
- path/to/file.cs — brief description

**Documentation** (N files):
- path/to/file.md — brief description

Does this look complete? Reply "yes" to proceed, or list additional
files/areas I should include.
```

Wait for user confirmation. If the user provides corrections, update the
feature map and re-present.

### Step 4: Generate spec.md (business specification)

Read `.specify/templates/spec-template.md` and fill it with reverse-engineered
content. The spec must describe WHAT the feature does and WHY, not HOW.

**Rules for reverse-engineering the spec**:

1. **Extract user stories from test scenarios**: Each distinct test class or
   test group maps to a user story. Prioritize by:
   - P1: Core happy-path functionality
   - P2: Provider-specific behavior, configuration options
   - P3: Edge cases, error handling, advanced usage

2. **Extract functional requirements from public APIs**: Each public
   method/property/interface becomes one or more FR-NNN items.
   Write them technology-agnostically:
   - BAD: "System MUST implement `IStreamingChatFeature` interface"
   - GOOD: "System MUST support streaming delivery of chat responses"

3. **Extract success criteria from test assertions**: Convert test
   assertions into measurable, technology-agnostic outcomes.

4. **Extract key entities from model classes**: List the domain concepts
   without implementation details.

5. **Extract edge cases from negative/boundary tests**: Document error
   scenarios and boundary conditions.

6. **Extract assumptions from implementation constraints**: Document
   what was assumed (multi-target framework, HttpClient-based, etc.)
   in the Assumptions section.

7. **Add a `## Retrospec Metadata` section** at the bottom:
   ```markdown
   ## Retrospec Metadata

   **Generated**: [DATE]
   **Source**: Reverse-engineered from existing implementation
   **Analyzed files**: [count] files across [count] projects
   **Reference implementation branch**: [current branch]
   ```

Write to `SPECIFY_FEATURE_DIRECTORY/spec.md`.

### Step 5: Generate plan.md (technical plan)

Read `.specify/templates/plan-template.md` and fill it from the actual
implementation decisions visible in the code.

**Rules for reverse-engineering the plan**:

1. **Technical Context**: Fill from the actual project — language/version,
   dependencies, storage, testing framework, target platform, project type,
   performance constraints. Use concrete values (not NEEDS CLARIFICATION)
   since the implementation already exists.

2. **Constitution Check**: Load `.specify/memory/constitution.md` and
   verify the implementation complies. Note any deviations.

3. **Project Structure**: Document the ACTUAL file tree for this feature,
   not the template options.

4. **Complexity Tracking**: Note any places where the implementation
   exceeds constitutional complexity limits, with justification.

Write to `SPECIFY_FEATURE_DIRECTORY/plan.md`.

### Step 6: Generate supporting artifacts

**6a. data-model.md** (if the feature has model/DTO classes)

For each model class identified in Step 3:
- Entity name, C# type
- Fields with types and constraints
- Relationships to other entities
- Validation rules (from code or test assertions)
- State transitions (if any)

Write to `SPECIFY_FEATURE_DIRECTORY/data-model.md`.

**6b. contracts/** (if the feature exposes public APIs)

For each public interface or API surface:
- Document the contract (method signatures, expected behavior)
- Input/output types
- Error conditions
- Usage examples (extracted from tests or wiki)

Write contract files to `SPECIFY_FEATURE_DIRECTORY/contracts/`.

**6c. research.md** (capture technical decisions)

For non-obvious implementation choices visible in the code:
- Decision: [what was chosen]
- Rationale: [inferred from code patterns, comments, or conventional wisdom]
- Alternatives considered: [reasonable alternatives that were not chosen]

Write to `SPECIFY_FEATURE_DIRECTORY/research.md`.

**6d. quickstart.md** (usage guide)

Extract from tests and wiki documentation:
- How to set up the feature
- Minimal working example
- Common configuration options
- How to verify it works

Write to `SPECIFY_FEATURE_DIRECTORY/quickstart.md`.

### Step 7: Generate tasks.md (OPTIONAL — only when `--tasks` flag is present)

**Skip this step unless** the user input contains `--tasks`. Retrospec is
about capturing design intent, not recreating an implementation checklist.
If skipped, proceed directly to Step 8.

When opted in, read `.specify/templates/tasks-template.md` and generate the
task breakdown that WOULD HAVE produced this implementation. **Every task
checkbox must be marked `[x]` (complete)** since the code already exists.

**Rules for reverse-engineering tasks**:

1. **Phase 1 (Setup)**: Project structure, dependencies, configuration
   that were prerequisites for this feature.

2. **Phase 2 (Foundational)**: Base interfaces, shared models, DI
   registration that block all user stories.

3. **Phase 3+ (User Stories)**: One phase per user story from spec.md.
   Within each:
   - Models/DTOs created
   - Service/feature implementations
   - Provider-specific implementations
   - Tests written
   Map each task to the actual file that implements it.

4. **Final Phase (Polish)**: Documentation, wiki updates, release notes,
   any cross-cutting work.

5. **All task IDs** follow the `T001`, `T002`, ... convention.
6. **All tasks** include exact file paths.
7. **Parallel markers** `[P]` for tasks that touch different files.
8. **Story labels** `[US1]`, `[US2]`, etc. for user story phase tasks.
9. **Every checkbox** is `[x]` — the implementation is complete.

Write to `SPECIFY_FEATURE_DIRECTORY/tasks.md`.

### Step 8: Generate requirements checklist

Create `SPECIFY_FEATURE_DIRECTORY/checklists/requirements.md` following
the checklist pattern from speckit-specify. Mark all items as `[x]`
(complete) with notes explaining how each requirement is satisfied by
the existing code.

### Step 9: Validate and report

1. **Cross-reference validation**:
   - Every entity in data-model.md appears in the codebase
   - Every contract in contracts/ maps to actual code
   - If tasks.md was generated: every file mentioned exists in the repository
     and every user story in spec.md has corresponding tasks

2. **Report to user**:
   ```
   ## Retrospec Complete: [Feature Name]

   **Spec directory**: SPECIFY_FEATURE_DIRECTORY
   **Files generated**:
   - spec.md — [N] user stories, [N] functional requirements
   - plan.md — technical plan with [tech stack summary]
   - data-model.md — [N] entities
   - contracts/ — [N] contract files
   - research.md — [N] technical decisions
   - quickstart.md — usage guide
   - checklists/requirements.md — quality checklist (all pass)
   - tasks.md — [N] tasks (all complete)  ← only if --tasks was used

   **Source analysis**: [N] files analyzed across [N] projects
   **Coverage**: [summary of what was and wasn't captured]

   **Next steps**:
   - Review the generated specs for accuracy
   - Use these specs as a reference for future modifications
   - Run `/speckit-analyze` to validate cross-artifact consistency
   ```

### Step 10: Post-execution hooks

Check if `.specify/extensions.yml` exists in the project root.
- If it exists, read it and look for entries under the `hooks.after_retrospec` key.
- If the YAML cannot be parsed or is invalid, skip hook checking silently and continue normally.
- Filter out hooks where `enabled` is explicitly `false`. Treat hooks without an `enabled` field as enabled by default.
- For each remaining hook, do **not** attempt to interpret or evaluate hook `condition` expressions:
  - If the hook has no `condition` field, or it is null/empty, treat the hook as executable
  - If the hook defines a non-empty `condition`, skip the hook and leave condition evaluation to the HookExecutor implementation
- When constructing slash commands from hook command names, replace dots (`.`) with hyphens (`-`).
- For each executable hook, output based on its `optional` flag:
  - **Optional hook** (`optional: true`): Show prompt and command for user to execute.
  - **Mandatory hook** (`optional: false`): Execute automatically via `EXECUTE_COMMAND`.
- If no hooks are registered or `.specify/extensions.yml` does not exist, skip silently.

## Key Rules

- **Read, don't invent**: Every spec item must trace back to actual code. Do not
  invent requirements that the code doesn't satisfy.
- **Technology-agnostic spec**: spec.md describes WHAT and WHY, not HOW. Keep
  implementation details in plan.md.
- **Tasks are optional**: tasks.md is only generated when `--tasks` is in the input. When generated, every checkbox is `[x]`.
- **Accurate file paths**: Every file path in generated artifacts must point to a real file.
- **No hooks bypass**: Respect extension hooks like any other speckit command.
- **User confirmation**: Wait for user approval of the feature map before
  generating artifacts. This prevents wasted work on incomplete analysis.
- **Preserve existing specs**: If a `specs/` directory already has a spec for
  this feature (check by name similarity), warn the user and ask whether to
  overwrite or create a new one.
- **Security**: Never read, display, or reference `.env` files or secrets.
- Use absolute paths for filesystem operations; use project-relative paths
  for references in documentation.
