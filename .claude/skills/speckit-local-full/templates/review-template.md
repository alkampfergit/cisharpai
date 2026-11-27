# Review record: feature <NN> <Title>

- Branch: `<branch>` at <short commit>, base `develop`
- Round: <1 or 2> of 2
- Gates at review time: verify <ok / failed>; mutation <score>% (threshold <n>%)
- Outcome: <closed / stopped, with the ADR path when there is one>

<!--
Written by the orchestrator from the last reviewer's report. Keep every heading, in this
order; write "none" under a heading that has nothing. One finding per numbered item:
file and line, what is wrong, what would fix it. Remove this comment.
-->

## Earlier findings

<Each blocking finding of the previous round: FIXED or NOT FIXED, with the evidence. "none" in round 1.>

## BLOCKING

1. `<file>:<line>`: <what is wrong>. Fix: <what would fix it>.

## NON-BLOCKING

1. `<file>:<line>`: <what is wrong or worth knowing>. <Decision taken, and by whom, if any.>

## Mutants

Final run: <score>% (threshold <n>%); <killed> killed, <survived> survived, <n> without unit coverage.

- `<file>:<line>` (<mutator>): <equivalent / real gap>, <one line of reasoning>.

## Not run

- <Manual check or quickstart section that was skipped, why, and the requirement left without evidence.>

## After this review

<Changes committed after the reviewer finished, which no review has seen. "none" otherwise.>
