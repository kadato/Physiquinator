#!/bin/sh
# Configure git to use the .githooks folder and mark the hooks executable.
# Runs in any POSIX shell, including git bash on Windows.
set -e

git config core.hooksPath .githooks
git update-index --add --chmod=+x .githooks/pre-commit
git update-index --add --chmod=+x .githooks/commit-msg

echo "Git hooks installed."
