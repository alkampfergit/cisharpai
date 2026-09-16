#!/usr/bin/env bash
# Diagnose the in-container Automata scheduler and optionally reconcile Git.
set -uo pipefail

REPAIR=0
case "${1:-}" in
  "") ;;
  --repair) REPAIR=1 ;;
  -h|--help)
    printf 'Usage: %s [--repair]\n' "${0##*/}"
    printf '  default   report scheduler, log, and branch health without changing Git\n'
    printf '  --repair  preserve the local tip and merge the upstream branch with --no-ff\n'
    exit 0
    ;;
  *)
    printf 'Unknown option: %s\n' "$1" >&2
    exit 2
    ;;
esac

ERRORS=0
warn() {
  ERRORS=$((ERRORS + 1))
  printf 'FAIL: %s\n' "$1"
}
ok() { printf 'OK:   %s\n' "$1"; }
info() { printf 'INFO: %s\n' "$1"; }

ROOT=$(git rev-parse --show-toplevel 2>/dev/null) || {
  printf 'FAIL: not inside a Git repository\n' >&2
  exit 1
}
cd "$ROOT" || exit 1

printf 'Automata do-work health check (%s)\n' "$(date -Is)"
printf 'Repository: %s\n' "$ROOT"

# Do not reconcile a checkout while a model run is using it.
ACTIVE_RUNS=$(pgrep -af '(^|/|[[:space:]])automata do-work([[:space:]]|$)' 2>/dev/null || true)
if [[ -n "$ACTIVE_RUNS" ]]; then
  warn "an Automata do-work process is active; do not repair this checkout concurrently"
  printf '%s\n' "$ACTIVE_RUNS"
else
  ok 'no active Automata do-work process'
fi

if pgrep -x cron >/dev/null 2>&1; then
  ok 'cron daemon is running'
else
  warn 'cron daemon is not running'
fi

CRON_FILE=/etc/cron.d/automata-do-work
RUNNER=/usr/local/bin/automata-do-work-cron
if [[ -r "$CRON_FILE" ]] && grep -Fq "$RUNNER" "$CRON_FILE"; then
  ok "cron entry exists: $CRON_FILE"
else
  warn "cron entry is missing or does not invoke $RUNNER"
fi
if [[ -x "$RUNNER" ]]; then
  ok "runner is executable: $RUNNER"
else
  warn "runner is missing or not executable: $RUNNER"
fi

if command -v automata >/dev/null 2>&1; then
  AUTOMATA_VERSION=$(automata --version 2>/dev/null || true)
  if [[ -n "$AUTOMATA_VERSION" ]]; then
    ok "Automata CLI available: $AUTOMATA_VERSION"
  else
    warn 'Automata CLI is present but --version failed'
  fi
else
  warn 'Automata CLI is not on the scheduler PATH'
fi

LOG="$HOME/.local/state/automata-do-work/cron.log"
if [[ -f "$LOG" ]]; then
  python3 - "$LOG" <<'PY'
from pathlib import Path
import sys, time

path = Path(sys.argv[1])
text = path.read_text(errors="replace")
lines = text.splitlines()
age = max(0, int(time.time() - path.stat().st_mtime))
print(f"OK:   cron log exists ({path.stat().st_size} bytes, {len(lines)} lines, {age}s old)")
recent = "\n".join(lines[-250:])
for marker in ("pull-failed", "failed", "error", "skipped"):
    count = recent.lower().count(marker)
    if count:
        print(f"INFO: last 250 log lines contain {count}x {marker}")
for line in lines[-12:]:
    print(f"LOG:  {line}")
PY
else
  warn "cron log does not exist: $LOG"
fi

BRANCH=$(git symbolic-ref --quiet --short HEAD 2>/dev/null || true)
if [[ -z "$BRANCH" ]]; then
  warn 'checkout is detached; Automata needs a branch checkout'
else
  info "branch: $BRANCH"
fi

# Ignore only this untracked diagnostic script; all other worktree changes stop repair.
DIRTY=0
while IFS= read -r status_line; do
  [[ "$status_line" == "?? .devcontainer/check-do-work-cron.sh" ]] && continue
  DIRTY=1
  printf 'INFO: worktree change: %s\n' "$status_line"
done < <(git status --porcelain)
if [[ "$DIRTY" -eq 0 ]]; then
  ok 'worktree is clean (diagnostic script excluded if untracked)'
else
  warn 'worktree has changes; branch repair is disabled'
fi

if [[ -n "$BRANCH" ]]; then
  UPSTREAM=$(git rev-parse --abbrev-ref --symbolic-full-name '@{u}' 2>/dev/null || true)
  if [[ -z "$UPSTREAM" ]]; then
    warn "branch has no upstream: $BRANCH"
  else
    REMOTE=${UPSTREAM%%/*}
    if git fetch "$REMOTE" "$BRANCH" >/dev/null 2>&1; then
      ok "fetched $UPSTREAM"
      COUNTS=$(git rev-list --left-right --count "$UPSTREAM...HEAD" 2>/dev/null || true)
      BEHIND=${COUNTS%%[[:space:]]*}
      AHEAD=${COUNTS##*[[:space:]]}
      [[ "$BEHIND" =~ ^[0-9]+$ ]] || BEHIND=unknown
      [[ "$AHEAD" =~ ^[0-9]+$ ]] || AHEAD=unknown
      info "branch divergence: ahead=$AHEAD behind=$BEHIND"

      if [[ "$BEHIND" == 0 ]]; then
        ok 'local branch contains the upstream tip'
      elif [[ "$AHEAD" == 0 ]]; then
        if [[ "$REPAIR" -eq 1 && "$DIRTY" -eq 0 && -z "$ACTIVE_RUNS" ]]; then
          if git merge --ff-only "$UPSTREAM" >/dev/null; then
            ok "fast-forwarded local branch to $UPSTREAM"
          else
            warn "fast-forward repair failed for $UPSTREAM"
          fi
        else
          warn 'local branch is behind upstream; run with --repair to fast-forward'
        fi
      else
        warn 'local and upstream branches diverged; Automata fast-forward pull will fail'
        if [[ "$REPAIR" -eq 1 && "$DIRTY" -eq 0 && -z "$ACTIVE_RUNS" ]]; then
          SAFE_BRANCH="automata-recovery/${BRANCH//\//-}-$(date -u +%Y%m%dT%H%M%SZ)"
          if git branch "$SAFE_BRANCH" HEAD; then
            info "preserved local tip as $SAFE_BRANCH"
            if git merge --no-ff --no-edit "$UPSTREAM"; then
              ok "merged $UPSTREAM with --no-ff; new local tip: $(git rev-parse --short HEAD)"
            else
              git merge --abort >/dev/null 2>&1 || true
              warn "merge conflict while reconciling $UPSTREAM; merge aborted"
            fi
          else
            warn "could not create recovery branch $SAFE_BRANCH"
          fi
        elif [[ "$REPAIR" -eq 1 ]]; then
          warn 'repair requested but blocked by an active run or dirty worktree'
        fi
      fi
    else
      warn "could not fetch $UPSTREAM"
    fi
  fi
fi

if [[ "$ERRORS" -eq 0 ]]; then
  printf 'RESULT: healthy\n'
  exit 0
fi
printf 'RESULT: %s problem(s) detected\n' "$ERRORS"
exit 1
