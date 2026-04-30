#!/usr/bin/env zsh
set -euo pipefail

allowlist=(
  OPENAI_TEST_API_KEY
  ANTHROPIC_TEST_API_KEY
  AZURE_OPENAI_TEST_ENDPOINT
  AZURE_OPENAI_TEST_API_KEY
  AZURE_OPENAI_TEST_DEPLOYMENTS
  AZURE_OPENAI_TEST_EMBEDDING_DEPLOYMENT
  AZURE_INFERENCE_TEST_ENDPOINT
  AZURE_INFERENCE_TEST_API_KEY
  AZURE_INFERENCE_TEST_MODELS
  AZURE_INFERENCE_TEST_EMBEDDING_ENDPOINT
  AZURE_INFERENCE_TEST_EMBEDDING_KEY
  AZURE_INFERENCE_TEST_EMBEDDING_MODEL
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

echo "Reading secrets from: $env_file"

set_count=0
set_keys=()
debug=${DEBUG:-0}

while IFS= read -r line || [[ -n "$line" ]]; do
  line="$(trim "$line")"
  [[ -z "$line" || "$line" == \#* ]] && continue

  line="${line#export }"
  [[ "$line" != *"="* ]] && continue

  key="${line%%=*}"
  value="${line#*=}"

  key="$(trim "$key")"
  value="$(trim "$value")"

  [[ $debug -eq 1 ]] && echo "[DEBUG] Found key: '$key'" >&2

  if [[ ${#value} -ge 2 ]]; then
    if [[ "$value" == \"*\" && "$value" == *\" ]]; then
      value="${value:1:-1}"
    elif [[ "$value" == \'*\' && "$value" == *\' ]]; then
      value="${value:1:-1}"
    fi
  fi

  if is_allowed "$key"; then
    if [[ "$key" == "AZURE_OPENAI_TEST_DEPLOYMENTS" ]]; then
      value="${value//, /,}"
      value="${value// ,/,}"
      value="${value//$'\t'/}"
    fi
    if [[ -z "$value" ]]; then
      echo "Skipping $key (empty value)." >&2
      continue
    fi
    # Set secret for GitHub Actions
    gh secret set "$key" --body "$value" >/dev/null
    # Set secret for Codespaces
    gh secret set "$key" --body "$value" --app codespaces >/dev/null
    # Display value (truncate API keys for security)
    if [[ "$key" == *"API_KEY"* ]]; then
      display_value="${value:0:10}..."
    else
      display_value="$value"
    fi
    echo "Set $key = $display_value (actions + codespaces)"
    set_keys+=("$key")
    set_count=$((set_count + 1))
  fi
done < "$env_file"

# Check for missing variables from allowlist
missing_keys=()
for allowed in "${allowlist[@]}"; do
  found=0
  for set_key in "${set_keys[@]}"; do
    if [[ "$allowed" == "$set_key" ]]; then
      found=1
      break
    fi
  done
  if [[ $found -eq 0 ]]; then
    missing_keys+=("$allowed")
  fi
done

if [[ ${#missing_keys[@]} -gt 0 ]]; then
  echo ""
  echo "Warning: The following variables are in the allowlist but missing from .env:" >&2
  for missing in "${missing_keys[@]}"; do
    echo "  - $missing" >&2
  done
fi

if [[ $set_count -eq 0 ]]; then
  echo "No secrets were set. Ensure your .env contains the expected keys." >&2
  exit 1
fi

echo "Done. $set_count secrets set for both Actions and Codespaces from $env_file."