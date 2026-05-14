<!--
Sync Impact Report
- Version change: 0.0.0 → 1.0.0 (initial reverse-engineered constitution)
- Modified principles: N/A (first version)
- Added sections:
  - Core Principles (7 principles derived from AGENTS.md + codebase)
  - Technology & Quality Standards
  - Development Workflow
  - Governance
- Removed sections: N/A
- Templates requiring updates:
  - .specify/templates/plan-template.md — ✅ no changes needed (Constitution Check section is generic)
  - .specify/templates/spec-template.md — ✅ no changes needed (requirements structure compatible)
  - .specify/templates/tasks-template.md — ✅ no changes needed (phase structure compatible)
- Follow-up TODOs: none
-->

# Cisharpai Constitution

## Core Principles

### I. Unified Abstraction

Every LLM provider MUST implement the same `IChatCompletionClient` and
`IEmbeddingClient` interfaces. Switching providers MUST be a configuration
change, never a code change in consuming applications. Optional capabilities
MUST be discovered via the Feature Collection pattern
(`IHasFeatures.Features.Get<T>()`), not provider-specific types.

### II. No Exceptions for API Errors (NON-NEGOTIABLE)

Client methods MUST return `IsSuccess=false` with a populated `ErrorMessage`
for all API-level errors (4xx, 5xx, malformed responses). Exceptions are
reserved exclusively for network failures and configuration errors. Callers
MUST be able to handle failure via property checks, never try/catch for
business logic.

### III. Debuggability First

Every response object MUST expose `RawResponseJson` and `RawRequestJson`
so developers can inspect the exact wire-level payloads. The
`ExtraParameters` deep-merge mechanism MUST allow arbitrary JSON injection
into requests so users can access bleeding-edge provider features without
waiting for library updates.

### IV. Immutability

All request and response DTOs MUST be immutable C# records. No mutable
state in models. This ensures thread safety and predictable behavior across
async pipelines.

### V. Test-Driven Quality (NON-NEGOTIABLE)

Every new feature or fix MUST include tests. A task is not complete until
its tests are green. Unit tests use NSubstitute for mocking. The
`Cisharpai.Testing` package MUST provide fakes (`FakeChatCompletionClient`,
`FakeEmbeddingClient`, `FakeResponses`) for every feature interface so
downstream consumers can test without real API calls.

### VI. Multi-Target Compatibility

All library projects MUST target both .NET 8.0 and .NET 10. The single
unit-test project MUST also multi-target both frameworks. Integration tests
compile for .NET 10 only. No framework-specific APIs without conditional
compilation.

### VII. Documentation as Deliverable

The `wiki/` folder, `RELEASE_NOTES.md`, and `memories/project_overview.md`
MUST be updated as part of any feature delivery. A feature without updated
documentation is incomplete. The post-change checklist in AGENTS.md is the
authoritative list of artifacts to update.

## Technology & Quality Standards

- **Language**: C# on .NET 8.0 / .NET 10 (multi-target).
- **HTTP**: Raw `HttpClient` with `IHttpMessageHandlerFactory` for DI and
  resilience. No provider SDKs (except `Azure.Identity` for AAD auth).
- **Serialization**: `System.Text.Json` exclusively. Deep-merge via
  `JsonDeepMerge`.
- **DI**: `Microsoft.Extensions.DependencyInjection` with keyed service
  support for multi-provider registration.
- **CI**: GitHub Actions — build + unit tests (both TFMs) + integration
  tests (.NET 10). SonarCloud quality gate MUST pass.
- **Versioning**: GitVersion in ContinuousDeployment mode. Labels: `alpha`
  (develop/feature), `beta` (release/hotfix).
- **Security**: `.env` files MUST never be read, displayed, or committed.
  All secrets flow through environment variables.

## Development Workflow

- Source lives in `src/`, organized as one project per provider plus a core
  abstractions project, a testing-fakes project, and a console demo.
- All PRs MUST pass the Post-Change Checklist (AGENTS.md §Post-Change
  Checklist) before merge.
- Environment variable changes MUST update the five files listed in
  AGENTS.md §Environment Variable Maintenance.
- New publishable projects MUST be added to `scripts/build.ps1`
  `$packProjects`.
- When a new feature interface is added to the core, the `Cisharpai.Testing`
  package MUST be updated with corresponding fakes, queues, defaults, and
  capture.

## Governance

This constitution is the authoritative source of project principles.
It supersedes ad-hoc conventions when conflicts arise.

- **Amendments** require: (1) a description of the change and rationale,
  (2) version bump per semantic versioning, (3) update to AGENTS.md if
  principles or rules change.
- **Compliance** is verified via the Post-Change Checklist and CI quality
  gates. All PRs and reviews MUST verify adherence to these principles.
- **Runtime guidance** for day-to-day development lives in `AGENTS.md`;
  this constitution captures the non-negotiable principles behind those
  rules.

**Version**: 1.0.0 | **Ratified**: 2026-01-29 | **Last Amended**: 2026-05-14
