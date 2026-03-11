@../AGENTS.md

# cisharp ai

A small library to interact with various llm using simply HttpClient in C#.

# Key principles

I want to create a library where the user can use the same interface based on HttpClient configuration to interact with different llm providers or models, so each specific client will receive a common parameter will translate in internal request, use the httpclient and then return a common object to the caller.

# Security rules

- NEVER read, cat, or display .env files. These contain secrets and API keys that must not be exposed.

# General rule

- Source code is in src folder, both project and tests.
- Projects multitarget .NET 8.0 and .NET 10
- Single test project multitarget .NET 8.0 and .NET 10
- You will write test for every functionality you add.
- Do not consider task finished if tests are not green.
- After you modifiy the code if needed update  [project_overview.md](../memories/project_overview.md) file in memory folder to reflect the changes you made.

# Testing rules

- If you wan to mockup use NSubstitute

## Project structure

Project structure can be find here: [project_overview.md](../memories/project_overview.md)

@../memories/project_overview.md

## Post-Change Checklist

When modifying core features (new feature interface, new provider, new model support, changed API):
1. Update `wiki/` — relevant feature guide(s), `wiki/provider-features.md` matrix, `wiki/index.md` TOC if new page
2. Update `wiki/testing.md` — if new feature interfaces are added, document how to fake them
3. Update `memories/project_overview.md` — reflect structural changes
4. Update `Cisharpai.Testing` — add queues/defaults/capture for any new feature interface on fake clients
5. Update `scripts/build.ps1` `$packProjects` if a new publishable project is added

## Beade integration

If the prompt is related to beads tool you can find the documentation here: [beads-guide.md](../memories/beads-guide.md)