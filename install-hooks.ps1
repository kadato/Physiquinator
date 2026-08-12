# Configure Git to use custom .githooks folder
Write-Host "Configuring git hooks path..."
git config core.hooksPath .githooks

# Mark hook scripts as executable in git index
Write-Host "Setting execution permissions on git hooks..."
git update-index --add --chmod=+x .githooks/pre-commit
git update-index --add --chmod=+x .githooks/commit-msg

Write-Host "Git hooks installed."
