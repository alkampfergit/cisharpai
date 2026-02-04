#!/bin/bash
# Post-creation setup script for AI Document Management devcontainer
# This script runs after the container is created to install dependencies and configure the environment

set -e  # Exit on error

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

# Install Claude Code CLI
echo "Installing Claude Code CLI..."
npm install -g @anthropic-ai/claude-code

# Install beads
echo "Installing beads..."
curl -fsSL https://raw.githubusercontent.com/steveyegge/beads/main/scripts/install.sh | bash
