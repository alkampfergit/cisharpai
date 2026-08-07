# PR Check Fixer Memory

- Use the `github-alk:gh-actions-debug` and `github-alk:sonarcloud` plugin skills as the diagnostic workflow before implementing PR fixes.
- Operate only on the PR for the current branch; the remediation loop includes local validation, commit, push, and remote check watching.
