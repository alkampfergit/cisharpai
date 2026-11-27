<#
.SYNOPSIS
  Close a finished feature: squash its branch into one unsigned commit on the base
  branch, then delete the feature branch.
.DESCRIPTION
  Refuses to run unless the feature branch is checked out with a clean tree. The
  commit subject is "feat(<NN-slug>): <title>"; resolve-features.ps1 recognises a
  closed feature by that subject, so do not reword it.
.EXAMPLE
  pwsh -NoProfile -File close-feature.ps1 -Number 03 -BodyFile C:\temp\body.txt
#>
param(
    [Parameter(Mandatory)][string]$Number,
    [string]$BaseBranch = 'develop',
    # Optional file with the commit body (summary, trailers).
    [string]$BodyFile
)

. (Join-Path $PSScriptRoot 'common.ps1')

try {
    $root = Get-RepoRoot
    $catalog = Get-FeatureCatalog -RepoRoot $root
    $feature = @(Select-FeatureRange -Catalog $catalog -Range $Number)
    if ($feature.Count -ne 1) { throw "'$Number' must name exactly one feature." }
    $feature = $feature[0]

    if ((Get-CurrentBranchName) -ne $feature.Branch) { throw "Check out $($feature.Branch) before closing it." }
    if (-not (Test-CleanTree)) { throw 'Working tree has uncommitted changes; commit them on the feature branch first.' }

    $ahead = git rev-list --count "$BaseBranch..$($feature.Branch)"
    if ([int]$ahead -eq 0) { throw "$($feature.Branch) has no commits beyond $BaseBranch." }

    $messageFile = Join-Path ([System.IO.Path]::GetTempPath()) "speckit-local-full-commit-$($feature.Id).txt"
    $message = "feat($($feature.Id)): $($feature.Title)`n`nSpec: $($feature.SpecDir)`nBacklog: $($feature.File)`n"
    if ($BodyFile) {
        if (-not (Test-Path $BodyFile)) { throw "Body file not found: $BodyFile" }
        $message += "`n" + (Get-Content $BodyFile -Raw).Trim() + "`n"
    }
    Set-Content -Path $messageFile -Value $message -Encoding utf8NoBOM

    git switch $BaseBranch | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "git switch $BaseBranch failed." }

    git merge --squash $feature.Branch | Out-Null
    if ($LASTEXITCODE -ne 0) {
        git reset --hard HEAD | Out-Null
        git switch $feature.Branch | Out-Null
        throw "Squash merge of $($feature.Branch) into $BaseBranch hit conflicts; nothing was committed and the branch is intact."
    }

    git commit --no-gpg-sign -F $messageFile | Out-Null
    if ($LASTEXITCODE -ne 0) {
        git reset --hard HEAD | Out-Null
        git switch $feature.Branch | Out-Null
        throw "git commit on $BaseBranch failed; the squash was rolled back and the branch is intact."
    }
    Remove-Item $messageFile -Force -ErrorAction SilentlyContinue

    $commit = git rev-parse --short HEAD
    git branch -D $feature.Branch | Out-Null

    [pscustomobject]@{
        closed        = $feature.Id
        commit        = $commit
        subject       = "feat($($feature.Id)): $($feature.Title)"
        baseBranch    = $BaseBranch
        branchDeleted = $feature.Branch
    } | ConvertTo-Json
}
catch {
    [Console]::Error.WriteLine("close-feature: $($_.Exception.Message)")
    exit 1
}
