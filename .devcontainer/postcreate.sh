#!/bin/bash
# Post-creation setup script for AI Document Management devcontainer
# This script runs after the container is created to install dependencies and configure the environment

set -euo pipefail

echo "=========================================="
echo "Starting devcontainer post-creation setup"
echo "=========================================="

# Fix apt sources issue with yarn
echo "Cleaning up apt sources..."
sudo rm -f /etc/apt/sources.list.d/yarn.list

# Update apt and install git-flow
echo "Installing system dependencies..."
sudo apt-get update
sudo apt-get install -y git-flow

# Setup git aliases
echo "Configuring git aliases..."
bash .devcontainer/setup-git-aliases.sh

# Install Claude Code via the official installer
echo "Installing Claude Code CLI..."
curl -fsSL https://claude.ai/install.sh | bash || true

# Install CLI tools distributed via npm
if command -v npm >/dev/null 2>&1; then
    echo "Installing OpenAI Codex..."
    npm install -g @openai/codex || true
else
    echo "npm not available, skipping npm-based CLI installs."
fi

# Install beads
echo "Installing beads..."
curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash || true

# Install uv and GitHub spec-kit
if ! command -v uv >/dev/null 2>&1; then
    echo "Installing uv..."
    curl -LsSf https://astral.sh/uv/install.sh | sh
else
    echo "uv already installed, skipping."
fi

if command -v uv >/dev/null 2>&1; then
    echo "Installing github spec-kit via uv..."
    uv tool install specify-cli --from git+https://github.com/github/spec-kit.git || true
else
    echo "uv not available, cannot install spec-kit."
fi

# Install tokensave and configure agent integrations
echo "Installing tokensave..."
TOKENSAVE_TAG=$(curl -sI https://github.com/aovestdipaperino/tokensave/releases/latest | grep -i '^location:' | sed 's|.*/tag/||;s/\r//')
TOKENSAVE_VERSION="${TOKENSAVE_TAG#v}"
ARCH=$(uname -m)
if [ "$ARCH" = "aarch64" ] || [ "$ARCH" = "arm64" ]; then
    TOKENSAVE_ARCH="aarch64-linux"
else
    TOKENSAVE_ARCH="x86_64-linux"
fi
TOKENSAVE_URL="https://github.com/aovestdipaperino/tokensave/releases/download/${TOKENSAVE_TAG}/tokensave-${TOKENSAVE_TAG}-${TOKENSAVE_ARCH}.tar.gz"
echo "  Downloading tokensave ${TOKENSAVE_VERSION} (${TOKENSAVE_ARCH})..."
curl -sL "$TOKENSAVE_URL" -o /tmp/tokensave.tar.gz || true
if [ -f /tmp/tokensave.tar.gz ]; then
    tar xzf /tmp/tokensave.tar.gz -C /tmp || true
    if [ -f /tmp/tokensave ]; then
        sudo mv /tmp/tokensave /usr/local/bin/tokensave
        echo "  tokensave installed."
    fi
    rm -f /tmp/tokensave.tar.gz
fi

if command -v tokensave >/dev/null 2>&1; then
    echo "  Configuring tokensave for Claude Code..."
    tokensave install --agent claude || true
    echo "  Configuring tokensave for Codex CLI..."
    tokensave install --agent codex || true
    tokensave enable-upload-counter || true
    echo "  Indexing repository..."
    tokensave sync || true
else
    echo "tokensave install failed or is unavailable, skipping configuration."
fi

# Install Homebrew and rtk
append_if_missing() {
    local line="$1"
    local file="$2"

    touch "$file"
    if ! grep -Fqx "$line" "$file"; then
        echo "$line" >>"$file"
    fi
}

if ! command -v brew >/dev/null 2>&1; then
    echo "Installing Homebrew..."
    NONINTERACTIVE=1 bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)" || true
else
    echo "Homebrew already installed, skipping."
fi

if [ -x /home/linuxbrew/.linuxbrew/bin/brew ]; then
    BREW_BIN="/home/linuxbrew/.linuxbrew/bin/brew"
elif [ -x /opt/homebrew/bin/brew ]; then
    BREW_BIN="/opt/homebrew/bin/brew"
else
    BREW_BIN=""
fi

if [ -n "$BREW_BIN" ]; then
    BREW_SHELLENV_LINE="eval \"\$($BREW_BIN shellenv)\""
    append_if_missing "$BREW_SHELLENV_LINE" "$HOME/.zprofile"
    append_if_missing "$BREW_SHELLENV_LINE" "$HOME/.zshrc"
    append_if_missing "$BREW_SHELLENV_LINE" "$HOME/.bashrc"
    eval "$("$BREW_BIN" shellenv)"

    if brew list --formula rtk >/dev/null 2>&1; then
        echo "rtk already installed, skipping."
    else
        echo "Installing rtk via Homebrew..."
        brew install rtk || true
    fi

    if command -v rtk >/dev/null 2>&1; then
        echo "Configuring rtk for Claude Code..."
        rtk init --global --auto-patch || true
        echo "Configuring rtk for Codex CLI..."
        rtk init --global --codex --auto-patch || true
    else
        echo "rtk install failed or is unavailable, skipping configuration."
    fi
else
    echo "Homebrew install did not expose brew on a known path, skipping rtk."
fi

# ─────────────────────────────────────────────────────────────
# gstack prerequisites and install
# ─────────────────────────────────────────────────────────────
# gstack (https://github.com/garrytan/gstack) is a set of Claude Code skills
# that ship a Playwright-backed browser. It requires:
#   1. bun runtime (to build and run the browse binary)
#   2. Playwright Chromium system libs (so the downloaded browser can launch)
#   3. The gstack repo cloned under ~/.claude/skills/gstack with ./setup run

# 1. Install bun
export BUN_INSTALL="${BUN_INSTALL:-$HOME/.bun}"
if ! command -v bun >/dev/null 2>&1 && [ ! -x "$BUN_INSTALL/bin/bun" ]; then
    echo "Installing bun..."
    BUN_TMPFILE=$(mktemp)
    if curl -fsSL "https://bun.sh/install" -o "$BUN_TMPFILE"; then
        BUN_VERSION="1.3.10" bash "$BUN_TMPFILE" || true
        rm -f "$BUN_TMPFILE"
    else
        echo "  bun installer download failed, skipping."
        rm -f "$BUN_TMPFILE"
    fi
else
    echo "bun already installed, skipping."
fi

# Ensure bun is on PATH for this script and future shells
if [ -x "$BUN_INSTALL/bin/bun" ]; then
    export PATH="$BUN_INSTALL/bin:$PATH"
    BUN_PATH_LINE='export PATH="$HOME/.bun/bin:$PATH"'
    append_if_missing "$BUN_PATH_LINE" "$HOME/.bashrc"
    append_if_missing "$BUN_PATH_LINE" "$HOME/.zshrc"
fi

# 2. Install Playwright Chromium system libraries (required to launch the
#    Chromium that gstack's browse binary downloads).
if command -v bun >/dev/null 2>&1; then
    echo "Installing Playwright Chromium system deps..."
    sudo -E env "PATH=$PATH" bunx --bun playwright install-deps chromium || true
fi

# 3. Clone gstack and run its setup (idempotent: skip if already installed)
GSTACK_DIR="$HOME/.claude/skills/gstack"
if [ -d "$GSTACK_DIR/.git" ]; then
    echo "gstack already cloned at $GSTACK_DIR, skipping clone."
else
    echo "Cloning gstack..."
    mkdir -p "$HOME/.claude/skills"
    git clone --single-branch --depth 1 https://github.com/garrytan/gstack.git "$GSTACK_DIR" || true
fi

if [ -x "$GSTACK_DIR/setup" ] && command -v bun >/dev/null 2>&1; then
    echo "Running gstack setup..."
    (cd "$GSTACK_DIR" && ./setup) || echo "gstack setup failed, continuing anyway."
else
    echo "gstack setup script or bun unavailable, skipping gstack setup."
fi
