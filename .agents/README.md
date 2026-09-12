# Shared Claude skills for Codex

Run from the repository root (Python 3, no third-party dependencies):

```sh
python3 .agents/link_claude_skills.py
```

On Windows use `py -3` instead of `python3`; creating directory symlinks
requires Developer Mode or symlink privileges. Windows execution has not
been tested here.

The script creates `.agents/skills/<name>` pointing relatively to
`../../.claude/skills/<name>` for each folder containing `SKILL.md`.
It works independently of the current working directory, preserves reference
files through the directory link, and can be rerun when Claude skills are added.
Correct existing links are skipped. Conflicting files, directories and broken
links cause an error without being overwritten. Stale links are not removed.
Generated links are local and ignored by Git; run the script after cloning.

[Codex documentation](https://learn.chatgpt.com/docs/build-skills) documents
repository discovery through `.agents/skills` and support for symlinked folders.
If skills do not appear, restart Codex from this repository. A running hosted
session may need to read the linked `SKILL.md` directly instead of refreshing
its skill catalog.

The links expose instructions; they do not install Claude-specific tools,
subagents, MCP servers, `gh`, or other dependencies referenced by those skills.
Read each skill before use and check its requirements against the active runtime.

Tests:

```sh
python3 -m unittest discover -s .agents -p 'test_*.py'
```
