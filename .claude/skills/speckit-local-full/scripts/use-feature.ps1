<#
.SYNOPSIS
  Make one feature the active one: switch to (or create) its branch and point
  Spec Kit at its spec directory through .specify/feature.json.
.DESCRIPTION
  Spec Kit's plan, tasks and implement commands find the feature through
  .specify/feature.json, not through the branch name, so this must run before
  every step of a feature, including after a resume.
.EXAMPLE
  pwsh -NoProfile -File use-feature.ps1 -Number 03
#>
param(
    [Parameter(Mandatory)][string]$Number,
    [string]$BaseBranch = 'develop'
)

. (Join-Path $PSScriptRoot 'common.ps1')

try {
    $root = Get-RepoRoot
    $catalog = Get-FeatureCatalog -RepoRoot $root
    $feature = @(Select-FeatureRange -Catalog $catalog -Range $Number)
    if ($feature.Count -ne 1) { throw "'$Number' must name exactly one feature." }
    $feature = $feature[0]

    $current = Get-CurrentBranchName
    if ($current -ne $feature.Branch) {
        if (-not (Test-CleanTree)) {
            throw "Working tree has uncommitted changes on '$current'. Commit or stash them before switching to $($feature.Branch)."
        }
        if (Test-BranchExists $feature.Branch) {
            git switch $feature.Branch | Out-Null
        }
        else {
            git switch -c $feature.Branch $BaseBranch | Out-Null
        }
        if ($LASTEXITCODE -ne 0) { throw "git switch to $($feature.Branch) failed." }
    }

    $pointer = Join-Path $root '.specify/feature.json'
    (@{ feature_directory = $feature.SpecDir } | ConvertTo-Json -Compress) | Set-Content -Path $pointer -Encoding utf8NoBOM

    Get-FeatureState -RepoRoot $root -Feature $feature -BaseBranch $BaseBranch | ConvertTo-Json -Depth 5
}
catch {
    [Console]::Error.WriteLine("use-feature: $($_.Exception.Message)")
    exit 1
}
