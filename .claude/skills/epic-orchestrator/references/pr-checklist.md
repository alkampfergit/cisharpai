# PR checklist: C1 → C6

Work through these strictly in order. Each gate assumes the ones before it are genuinely satisfied — not "probably fine," genuinely checked. Merging (C5) is irreversible, so the earlier gates exist to make that final decision safe to make quickly.

## C1 — CI is green

```
gh pr checks <PR>
```

If a check is `pending`, **stop and report that** — do not poll or sleep-loop waiting for it. The next tick will see it resolved (or still stuck, which is its own signal). Treating "pending" as "nothing to do yet" is correct; treating it as "keep checking every few seconds" wastes the turn and produces no new information.

If a check is genuinely `fail`, fetch the actual error before saying anything to the bot:

```
gh run view <run-id> --log
```

Never paraphrase from the check's name alone — "SonarCloud Analysis failed" could mean a real code problem, a transient scanner outage, or a build break, and the fix (or non-fix) differs completely between those. See "Distinguishing a real failure from an outage" below before concluding it's the bot's problem to fix.

## C2 — SonarCloud has zero new issues (not just a passing gate)

This is the gate people get wrong: **a passing "Quality Gate" banner does not mean zero new issues.** The gate only checks the specific conditions configured for it (ratings, coverage threshold, duplication threshold, security-hotspot review percentage) — it does not check "are there zero new code smells." A PR can show a green gate while carrying several new MINOR/INFO findings that nobody asked about, or can fail the gate on a condition (duplication density, security rating) that never shows up in a plain issues listing. Query all three, every time:

```
curl -s "https://sonarcloud.io/api/measures/component?component=<project-key>&pullRequest=<PR>&metricKeys=new_coverage,new_lines_to_cover,new_uncovered_lines,new_violations,new_blocker_violations,new_critical_violations,new_duplicated_lines_density"

curl -s "https://sonarcloud.io/api/issues/search?componentKeys=<project-key>&pullRequest=<PR>&resolved=false"

curl -s "https://sonarcloud.io/api/qualitygates/project_status?projectKey=<project-key>&pullRequest=<PR>"
```

The third call is what pinpoints *which specific condition* failed when the issues list comes back empty — e.g. `new_duplicated_lines_density` or `new_security_rating` failing on their own, with no corresponding entry in `issues/search`. When that happens, dig one level further:

- **Duplication**: `curl -s "https://sonarcloud.io/api/measures/component_tree?component=<project-key>&pullRequest=<PR>&metricKeys=new_duplicated_lines,new_lines&strategy=leaves&ps=100"` to find which files carry the duplicated lines.
- **Security rating**: filter the issues search to `&types=VULNERABILITY` — a single MAJOR/CRITICAL vulnerability (e.g. an unbounded regex, a ReDoS risk) can single-handedly drag the rating down even when `new_violations` total looks small.

**Before asking the bot to add tests for a coverage shortfall**, check the file-level breakdown (`component_tree` with `strategy=leaves`) to see which files the uncovered lines are actually in. If they're in a file that should be excluded from coverage accounting (generated code, a console/demo project, an internal tooling directory) rather than genuinely-untested product code, the fix is to check the project's Sonar exclusion configuration, not to demand more tests — asking the bot to write tests for something that should simply be excluded wastes a review cycle. Note that a scanner invoked with command-line `/d:sonar.coverage.exclusions=...` overrides anything configured in the SonarCloud UI, so a UI-side exclusion silently not taking effect is a real, previously-seen failure mode worth checking for.

Only when `new_violations`, `new_blocker_violations`, `new_critical_violations` are all `0`, the issues search returns `total: 0`, and the quality gate status is `OK` on every condition, is C2 actually satisfied.

## C3 — request a Copilot review

**The only reliable way to do this:**

```
gh pr edit <PR> --add-reviewer @copilot
```

This requires `gh` CLI ≥ 2.88.0. **Do not** pass the review's comment-author login (typically something like `copilot-pull-request-reviewer[bot]`) or the plain display name (`Copilot`) to `--add-reviewer` or to the REST `requested_reviewers` endpoint — both fail with a GraphQL error resembling `Could not resolve user with login '...'`. This isn't a typo or wrong-guess problem: Copilot's review-requestable identity is typed as a `Bot` in GitHub's schema, and the `requestReviews`/`requestReviewsByLogin` mutations only accept `User`/`Team` node types. No amount of retrying with a different string representation of "Copilot" will work except the literal `@copilot` handle — that one routes through a different, dedicated path. If a request call fails with a generic GraphQL server error (not a "could not resolve user" message), that's a transient issue — retry once.

A requested review typically takes a few minutes to actually land as a completed review with comments. Don't loop waiting for it — request it, report that you did, and check again on the next tick.

**Cap requests at 3 per PR.** Ask the user if they'd like a different number, but absent a stated preference, stop at 3. Track how many times you've actually requested a review on this exact PR (via `gh api repos/<owner>/<repo>/issues/<PR>/timeline` filtering for `review_requested` events, or just your own memory of the session). After the 3rd round of fixes, do the final verification **yourself** — fetch the changed files' content at the current commit sha directly (`gh api repos/<owner>/<repo>/contents/<path>?ref=<sha> --jq '.content' | base64 -d`) and read them — rather than requesting a 4th review. If something still looks wrong after your own check, that's the point to escalate to the user, not to keep requesting reviews indefinitely.

## C4 — verify every review finding against actual current source before acting on it

When a review lands, its top-level body is often a *truncated summary* — the real findings, especially anything marked as a "suppressed comment," live in the review's own inline comments:

```
gh api repos/<owner>/<repo>/pulls/<PR>/reviews/<review-id>/comments
```

Fetch both. Then, for every finding that isn't obviously trivial, **check it against the file's actual content at the current commit** before deciding it's real:

```
gh api repos/<owner>/<repo>/contents/<path>?ref=<full-commit-sha> --jq '.content' | base64 -d
```

This step is not optional politeness — automated reviews get things wrong in specific, recurring ways worth watching for:

- **Stale findings.** A review can describe a bug that was already fixed in an earlier commit in the same PR — it's reviewing against context that no longer matches the tip. Diff what it says against what's actually there before relaying it.
- **Findings that contradict CI's own evidence.** A review claiming a "compile-blocking" error is directly falsifiable if `Build & Test` already passed on that exact commit — that's a stronger signal than re-deriving the language-syntax rule from memory. When a finding and an independently-verifiable fact disagree, trust the fact.
- **Findings that contradict a locked design decision.** If a finding's premise conflicts with something the user or an earlier design discussion already settled, go re-read that discussion before accepting the finding at face value — the review may be right that something looks off, but wrong about which side (the code, or a subsequent doc/wording change) is the actual bug. In one observed case, the real issue turned out to be that a wiki page's wording implied a stricter global guarantee than the design had actually specified — the correct fix was a wording change, not a behavior change, and asking the bot to "fix" the working code would have been actively wrong.
- **Findings that are real and severe.** Don't over-correct into reflexive skepticism — plenty of findings are exactly right, including ones a first read makes look minor. A cache-control marker landing on the wrong element of a request, or an offset computed before a later string-mutation step shifts everything after it, can both look like small details but genuinely defeat the point of the feature. When in doubt, trace the actual logic by hand against a concrete example rather than pattern-matching on how severe the finding *sounds*.

When you reply to the PR, be explicit about which findings you're asking the bot to act on and which you've determined are false alarms (and why, citing your evidence) — this stops the bot from "fixing" code that was already correct.

## C5 — merge

```
gh pr merge <PR> --squash --delete-branch
```

Only when C1–C4 are all genuinely satisfied: CI green, SonarCloud truly clean (not just gate-passed), and either a Copilot review has no remaining genuine findings or you've hit the 3-request cap and done your own direct final verification. Do a last sanity pass over the full file list (`gh pr diff <PR> --name-only`) to confirm nothing unexpected is in scope before merging.

Be conservative given this is irreversible — but once it's genuinely clean, merge with confidence. Holding a clean PR open indefinitely "just in case" is its own failure mode; the whole point of the earlier gates is to make this decision safe to make promptly.

## C6 — after merging, notify siblings

The base branch moved. Every other open PR belonging to this epic needs:

> `develop` moved (PR #N merged). Please rebase onto `develop` and push, then re-run the checks.

Post it once per PR (check the waiting discipline — don't post it twice). Then confirm each sibling's `mergeable`/`mergeStateStatus` reflects the conflict; this can take a few seconds to update after the merge, so a `git fetch --prune` plus a short pause before re-checking is normal, not a bug in your tooling.

---

## Distinguishing a real CI failure from an outage

Not every red check is the bot's problem to fix. If a job fails at a step that talks to an external service (SonarCloud's scanner "begin" step failing with `Downloading from .../api/server/version failed. Http status code is ServiceUnavailable`, for example), verify independently before looping the bot in:

```
curl -s -o /dev/null -w "%{http_code}\n" "https://sonarcloud.io/api/server/version"
```

If the service itself is returning 5xx directly (not just failing inside CI), it's an outage — there is nothing for the bot to fix. Keep checking passively on later ticks without commenting on the PR about it. Once the service recovers, re-run only the failed job rather than asking for a new push:

```
gh run rerun <run-id> --failed
```

This is meaningfully different from a code-caused failure and should never turn into a comment asking the bot to "investigate" something outside its control.
