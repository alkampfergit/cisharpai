<#
.SYNOPSIS
  Turn a feature range into the ordered list of features and where each one stands.
.EXAMPLE
  pwsh -NoProfile -File resolve-features.ps1 -Range "00-03"
#>
param(
    [Parameter(Mandatory)][string]$Range,
    [string]$BaseBranch = 'develop'
)

. (Join-Path $PSScriptRoot 'common.ps1')

try {
    $root = Get-RepoRoot
    $catalog = Get-FeatureCatalog -RepoRoot $root
    $selected = Select-FeatureRange -Catalog $catalog -Range $Range

    $features = @($selected | ForEach-Object { Get-FeatureState -RepoRoot $root -Feature $_ -BaseBranch $BaseBranch })

    [pscustomobject]@{
        repoRoot      = $root
        baseBranch    = $BaseBranch
        currentBranch = Get-CurrentBranchName
        cleanTree     = Test-CleanTree
        features      = $features
    } | ConvertTo-Json -Depth 5
}
catch {
    [Console]::Error.WriteLine("resolve-features: $($_.Exception.Message)")
    exit 1
}
