#!/usr/bin/env python3
"""Expose this repository's Claude skills to Codex using relative symlinks."""

import os
from pathlib import Path
import sys


def link_skills(root: Path) -> int:
    source = root / '.claude' / 'skills'
    destination = root / '.agents' / 'skills'
    if not source.is_dir():
        raise ValueError(f'Missing Claude skills directory: {source}')
    skills = sorted(p for p in source.iterdir() if (p / 'SKILL.md').is_file())
    if not skills:
        raise ValueError(f'No skills found in {source}')
    if destination.is_symlink():
        raise ValueError(f'Refusing symlink destination directory: {destination}')
    destination.mkdir(parents=True, exist_ok=True)

    # Check every conflict before creating any links. Never replace user content.
    pending = []
    for skill in skills:
        target = destination / skill.name
        if target.is_symlink() and target.resolve() == skill.resolve():
            continue
        if target.exists() or target.is_symlink():
            raise ValueError(f'Conflicting path (left untouched): {target}')
        pending.append((target, os.path.relpath(skill, destination)))

    for target, relative in pending:
        target.symlink_to(relative, target_is_directory=True)
    print(f'{len(pending)} links created; {len(skills)} Claude skills available.')
    return len(pending)


def main() -> int:
    try:
        link_skills(Path(__file__).resolve().parent.parent)
        return 0
    except (OSError, ValueError) as error:
        print(f'Error: {error}', file=sys.stderr)
        if isinstance(error, OSError) and getattr(error, 'winerror', None) == 1314:
            print('Windows requires Developer Mode or symlink privileges.', file=sys.stderr)
        return 1


if __name__ == '__main__':
    sys.exit(main())
