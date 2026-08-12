# Generate release notes between git tags or HEAD for local testing
param(
    [string]$FromTag,
    [string]$ToRef = "HEAD"
)

if (-not $FromTag) {
    $FromTag = git tag --sort=-version:refname | Select-Object -First 1
}

$range = if ($FromTag) { "${FromTag}..${ToRef}" } else { $ToRef }
Write-Host "Generating release notes for range: $range"

$logs = git log $range --no-merges --pretty=format:"%s"

$features = @()
$fixes = @()
$perf = @()
$refactor = @()

foreach ($line in $logs) {
    if ($line -match '^feat(\([^)]*\))?:\s*(.+)$') {
        $features += $Matches[2]
    }
    elseif ($line -match '^fix(\([^)]*\))?:\s*(.+)$') {
        $fixes += $Matches[2]
    }
    elseif ($line -match '^perf(\([^)]*\))?:\s*(.+)$') {
        $perf += $Matches[2]
    }
    elseif ($line -match '^refactor(\([^)]*\))?:\s*(.+)$') {
        $refactor += $Matches[2]
    }
}

$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine("## Release notes")
[void]$sb.AppendLine()

if ($features.Count -gt 0) {
    [void]$sb.AppendLine("### Features")
    foreach ($item in $features) {
        [void]$sb.AppendLine("- $item")
    }
    [void]$sb.AppendLine()
}

if ($fixes.Count -gt 0) {
    [void]$sb.AppendLine("### Bug fixes")
    foreach ($item in $fixes) {
        [void]$sb.AppendLine("- $item")
    }
    [void]$sb.AppendLine()
}

if ($perf.Count -gt 0) {
    [void]$sb.AppendLine("### Performance")
    foreach ($item in $perf) {
        [void]$sb.AppendLine("- $item")
    }
    [void]$sb.AppendLine()
}

if ($refactor.Count -gt 0) {
    [void]$sb.AppendLine("### Refactoring")
    foreach ($item in $refactor) {
        [void]$sb.AppendLine("- $item")
    }
    [void]$sb.AppendLine()
}

if ($features.Count -eq 0 -and $fixes.Count -eq 0 -and $perf.Count -eq 0 -and $refactor.Count -eq 0) {
    [void]$sb.AppendLine("Maintenance updates and general improvements.")
}

$notes = $sb.ToString()
Write-Host $notes
return $notes
