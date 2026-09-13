# Skeleton templates

Starting structures, not forms to fill in mechanically — adapt freely, but notice what each section is *for* before cutting it.

## Roadmap document

```markdown
# <Initiative> Roadmap

Status date: <date>. Baseline: `<branch>` at `<commit>`.

## Guiding principle

<One or two sentences framing the domain, then the literal filter as a quoted question:>

> Does this <the actual test — e.g. "wrap a provider API surface, or shape the payload we send to a model">?

Everything that answers yes is in scope. <Name the categories that answer no> are explicitly
deferred (see "Out of scope").

## 1. What exists today

<Inventory with evidence, not recall. Prefer a table with a Type/What-it-does shape for
concrete APIs, and call out anything surprising — work that exists but isn't where people
expect, or a widely-assumed capability that turns out absent.>

### Gaps

<Bullet list, each gap tied back to the filter — why it matters, not just that it's missing.>

## 2. Timeline

<Phases, each independently shippable, ordered by value-per-unit-of-work under the filter.>

### Phase 1 — <name> (highest priority)

<Why this phase is first — one sentence tying it to the filter.>

1. **<Item>.** <What it is, why it matters, roughly how big.>
2. ...

*Deferred out of this phase:* <anything that was a candidate but didn't make the cut, with
why — this is what stops the same idea getting re-proposed later without knowing it was
already considered.>

### Phase 2 — ...

## 3. Out of scope (deliberately)

| Not doing | Why |
|---|---|
| <candidate> | <the filter's verdict on it, in one line> |

## 4. Dependency order at a glance

<A small diagram or list showing which phases/items block which — helps a reader see what
can run in parallel versus what's genuinely sequential.>
```

## Epic (tracking) issue

```markdown
Tracking issue for <phase/initiative>. Roadmap: `<path to roadmap doc>`.

## Principle

<Restate the filter in one line — anyone landing on the epic without context needs it here too.>

## Where we actually are

<Current state, updated as work lands — this section should be treated as living, corrected
in place with a visible note when something in it turns out wrong, not left stale.>

## Children

<Link via the platform's native sub-issue mechanism if available. If not, a table:>

| # | Title | Depends on | Status |
|---|---|---|---|
| #N | <title> | — | open |
| #M | <title> | #N merged | open |
```

## Child issue

```markdown
<One paragraph: what this delivers and why it's in this phase.>

## Dependency

<Either "Independent of the other children in this phase." or the specific blocking
condition — "must not start until #N and #M are merged, because <reason>.">

## Decided

<Bullet list of settled design decisions, stated as fact, each with enough of the "why"
that a reader won't feel entitled to relitigate it. E.g.:>

- **<Decision>.** <Why — the tradeoff that was weighed, or the constraint that forced it.>

## Open question

<If something is genuinely undecided, state the actual fork — not "figure out the best
approach" but the concrete options and what's at stake in choosing between them. Omit this
section entirely if there's nothing genuinely open; don't manufacture a question to seem
thorough.>

## Explicitly deferred

<If this issue's scope was trimmed from some larger version of the idea, say what was cut
and where it went (a later phase, an escape-hatch mechanism, "not doing" entirely) — so
nobody re-adds it here by accident.>
```
