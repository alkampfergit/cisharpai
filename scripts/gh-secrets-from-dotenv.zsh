#!/usr/bin/env zsh
set -euo pipefail

allowlist=(
  OPENAI_TEST_API_KEY
  ANTHROPIC_TEST_API_KEY
  AZURE_OPENAI_TEST_ENDPOINT
  AZURE_OPENAI_TEST_API_KEY
  AZURE_OPENAI_TEST_DEPLOYMENTS
  COHERE_TEST_API_KEY
)

trim() {
  local s="$1"
  s="${s#"${s%%[![:space:]]*}"}"
  s="${s%"${s##*[![:space:]]}"}"
  echo "$s"
}

is_allowed() {
  local key="$1"
  local allowed
  for allowed in "${allowlist[@]}"; do
    if [[ "$allowed" == "$key" ]]; then
      return 0
    fi
  done
  return 1
}

find_env_file() {
  local dir="$PWD"
  while true; do
    if [[ -f "$dir/.env" ]]; then
      echo "$dir/.env"
      return 0
    fi
    if [[ "$dir" == "/" ]]; then
      return 1
    fi
    dir="$(dirname "$dir")"
  done
}

if ! command -v gh >/dev/null 2>&1; then
  echo "gh CLI not found in PATH." >&2
  exit 1
fi

if ! gh auth status >/dev/null 2>&1; then
  echo "gh CLI not authenticated. Run 'gh auth login' first." >&2
  exit 1
fi

env_file="$(find_env_file || true)"
if [[ -z "$env_file" ]]; then
  echo "No .env file found in current or parent directories." >&2
  exit 1
fi

set_count=0

while IFS= read -r line || [[ -n "$line" ]]; do
  line="$(trim "$line")"
  [[ -z "$line" || "$line" == \#* ]] && continue

  line="${line#export }"
  [[ "$line" != *"="* ]] && continue

  key="${line%%=*}"
  value="${line#*=}"

  key="$(trim "$key")"
  value="$(trim "$value")"

  if [[ "$value" == \"*\" && "$value" == *\" ]]; then
    value="${value:1:-1}"
  elif [[ "$value" == \'*\' && "$value" == *\' ]]; then
    value="${value:1:-1}"
  fi

  if is_allowed "$key"; then
    if [[ "$key" == "AZURE_OPENAI_TEST_DEPLOYMENTS" ]]; then
      value="${value//, /,}"
      value="${value// ,/,}"
      value="${value//\t/}"
    fi
    if [[ -z "$value" ]]; then
      echo "Skipping $key (empty value)." >&2
      continue
    fi
    gh secret set "$key" --body "$value" >/dev/null
    echo "Set secret $key."
    set_count=$((set_count + 1))
  fi
done < "$env_file"

if [[ $set_count -eq 0 ]]; then
  echo "No secrets were set. Ensure your .env contains the expected keys." >&2
  exit 1
fi

echo "Done. $set_count secrets set from $env_file."