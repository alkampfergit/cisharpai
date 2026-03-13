---
name: pr-expert
description: >
  Diagnose failing pull request checks across   GitHub Actions and external status
  providers such as SonarCloud and CodeQL. Use  when a user asks why a PR is
  red, which check is blocking merge, why CI  passed but the pull request still
  fails, why branch protection is not   satisfied, or what file needs to change to
  fix a failing status. Determines whether the  failing check belongs to
  github-actions or an external app, traces the   exact root cause, and identifies
  the smallest correct fix.
---

# PR Expert

## Overview

Use this skill when the user asks why a pull request is failing, why a check is red, which status is blocking merge, why a workflow passed but the PR is still red, or what needs to be fixed in CI.

## Scope

This skill is for diagnosing pull request failures end-to-end.

It covers:
- GitHub Actions workflow failures
- External status checks attached to a PR commit
- SonarCloud quality gate failures
- CodeQL and security workflow issues
- Branch protection blockers caused by check configuration or permissions

It does not assume that a red PR means a GitHub Actions job failed. External providers can publish failing statuses even when the workflow that triggered them succeeded.

It does not cover implementing the code fix unless the user asks for that follow-up work.

## Quick Start

Run the investigation in this order:

1. Identify the active PR, base branch, head SHA, and URL.
2. List every check attached to the head commit.
3. Separate `github-actions` checks from external app statuses.
4. Inspect the failing provider directly.
5. Map the failure to the exact file, line, rule, or command.
6. Recommend the smallest correct fix.

Do not stop at "the workflow failed" or "SonarCloud is red." The goal is the concrete cause.

## Activation Signals

Use this skill for prompts containing terms like:
- PR failing
- failing check
- red pull request
- GitHub checks
- branch protection
- SonarCloud
- quality gate
- CodeQL
- workflow failed
- why is CI red
- merge blocked
- check suite failed
- required status check
- workflow passed but PR failed
- what is blocking merge

## Investigation Workflow

1. Identify the PR.
- Determine the current branch and repository.
- Find the active PR number, title, base branch, and URL.
- Record the head SHA because checks are attached to the commit, not to the PR abstractly.

2. List the checks attached to the PR head commit.
- Start with GitHub check summaries.
- Record which checks are failing, skipped, pending, or cancelled.
- Do not assume the failing check is a workflow job.
- Record the check name, conclusion or state, and the owner app such as `github-actions`, `SonarCloud`, or `CodeQL`.

3. Separate GitHub Actions runs from external status providers.
- If the failing item is from `github-actions`, inspect the matching workflow run, job, and logs.
- If the failing item is from another app such as SonarCloud, query that provider directly for PR-specific status and issues.
- If branch protection is the blocker, verify whether the required check name matches the actually reported check name.

4. Trace the failure to a concrete cause.
- For workflow failures, identify the failing step, command, and file involved.
- For external providers, identify the exact rule, issue type, severity, file, and line when possible.
- Prefer provider-native evidence over summaries copied into GitHub.

5. Cross-check the relevant config in the repository.
- Read the workflow file or source file implicated by the failure.
- Verify whether the failure is a real product defect, a CI configuration problem, or an external policy violation.
- Distinguish between repository code, workflow configuration, and branch protection settings.

6. Summarize the result precisely.
- Name the failing check.
- State whether the failure is internal to GitHub Actions or external.
- Give the exact root cause.
- Point to the impacted file and line.
- Propose the smallest correct fix.

## Decision Tree

### If the failing check owner is `github-actions`

1. Find the workflow run that produced the failing check.
2. Open the failing job.
3. Identify the first failing step, not just the last noisy log line.
4. Capture the exact command, error message, and affected file or test.
5. Read the implicated workflow or source file to confirm the root cause.

### If the failing check owner is an external app

1. Treat the GitHub check as a pointer, not the source of truth.
2. Query the provider directly for PR-specific status and issues.
3. Capture the provider rule key, severity, file, line, and message.
4. Open the implicated file in the repository and verify that the provider finding matches the current code.
5. Determine whether the problem is application code, workflow config, or repository policy.

## SonarCloud-specific playbook

When the failing check is `SonarCloud Code Analysis`:

1. Confirm whether the GitHub Actions `SonarCloud Analysis` job passed.
- If the workflow passed but `SonarCloud Code Analysis` is red, the issue is usually in SonarCloud's PR analysis or quality gate, not in the workflow execution.

2. Query SonarCloud PR status.
- Fetch the pull request entry from SonarCloud using the project key and PR number.
- Read `qualityGateStatus` and the counts for bugs, vulnerabilities, and code smells.

3. Query SonarCloud issues for the PR.
- Filter by issue type such as `VULNERABILITY` when the gate reports a vulnerability.
- Capture the rule key, severity, file, line, and message.

4. Map the issue back to repository config.
- Open the flagged file and inspect the exact line.
- For workflow security rules, verify whether permissions are declared too broadly at workflow level instead of job level.

5. Report the distinction clearly.
- State whether the failing status comes from the SonarCloud quality gate or from the GitHub Actions job execution.
- If the Actions job passed, say so explicitly to avoid a false workflow diagnosis.

## CodeQL-specific playbook

When the failing check is a CodeQL analysis or security result:

1. Determine whether the failure is a workflow execution failure or a published security result.
2. If the workflow failed, inspect the failing CodeQL setup, autobuild, or analyze step.
3. If the workflow succeeded but the PR is still blocked, inspect the Code Scanning alert or security result attached to the PR.
4. Capture the rule, severity, file, line, and remediation guidance.
5. Verify whether the issue is in application code, test code, or workflow configuration.

## Specific lesson from this repository

In this repository, PR `#12` failed because SonarCloud reported one vulnerability in the CI workflow, even though the GitHub Actions Sonar job succeeded.

The concrete issue was:
- Rule: `githubactions:S8233`
- File: `.github/workflows/ci.yml`
- Problem: `security-events: write` was granted at workflow scope
- Fix direction: move write permissions from workflow level to only the jobs that require them, and remove permissions that are not needed

This pattern should be treated as a first-class diagnostic case for future PR investigations.

## Evidence Standards

- Prefer direct, minimal evidence over speculation.
- If GitHub CLI opens a pager or hides output, fall back to the GitHub API.
- External checks often require querying the provider API, not just reading GitHub workflow logs.
- A successful workflow does not guarantee a successful PR status.
- Security tooling frequently fails PRs on workflow configuration, not application code.
- If a check name is ambiguous, include the app owner so the user can distinguish similarly named statuses.
- Do not claim a root cause until you can tie it to a step, rule, file, or branch protection requirement.

## Expected output

When using this skill, produce a concise diagnosis that includes:
- failing check name
- owner of the check (`github-actions` or external app)
- exact root cause
- affected file and line when known
- minimal fix recommendation

Use this response shape:

```text
Failing check: <name>
Owner: <github-actions | app name>
Root cause: <specific cause>
Affected file: <path:line if known>
Minimal fix: <smallest correct change>
```

## Troubleshooting

**Problem: The PR shows red, but all visible workflow jobs passed**
Cause: The failing status is likely published by an external app such as SonarCloud or CodeQL.
Action: List check owners and inspect the external provider directly.

**Problem: Branch protection says a required check is missing**
Cause: The required status name may not match the actual reported check name, or the check did not run on the head SHA.
Action: Compare the required check name with the check suite attached to the PR head commit.

**Problem: A GitHub Actions workflow failed, but the logs are noisy**
Cause: The actionable failure is usually the first failing step or command, not the last emitted stack trace.
Action: Identify the first failed step, extract the exact command, then inspect the referenced file or test.

## Example prompts

- Why is this PR still red?
- GitHub shows one failing check on the pull request, can you diagnose it?
- SonarCloud is failing but the workflow passed. What is wrong?
- Which check is blocking merge and what file do I need to change?
- Can you inspect the PR checks and tell me the real root cause?
- CI is green but branch protection still fails. What is blocking merge?