# Shared helpers for the speckit-full scripts. Dot-source this file.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    $root = git rev-parse --show-toplevel 2>$null
    if ($LASTEXITCODE -ne 0 -or -not $root) { throw 'Not inside a git repository.' }
    return (Resolve-Path $root).Path
}

function Get-CurrentBranchName {
    $branch = git rev-parse --abbrev-ref HEAD 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Cannot read the current git branch.' }
    return $branch
}

function Test-CleanTree {
    $dirty = git status --porcelain
    return [string]::IsNullOrWhiteSpace(($dirty | Out-String))
}

# Every features/NN-name.md file, in numeric order.
function Get-FeatureCatalog {
    param([Parameter(Mandatory)][string]$RepoRoot)

    $dir = Join-Path $RepoRoot 'features'
    if (-not (Test-Path $dir)) { throw "No features folder at $dir" }

    $items = foreach ($file in Get-ChildItem $dir -Filter '*.md' -File) {
        if ($file.Name -notmatch '^(\d{2,3})-(.+)\.md$') { continue }
        $number = $Matches[1]
        $slug = $Matches[2].ToLowerInvariant()

        $title = $slug
        $heading = Get-Content $file.FullName -TotalCount 20 | Where-Object { $_ -match '^#\s+' } | Select-Object -First 1
        if ($heading) {
            $title = ($heading -replace '^#\s+', '') -replace '^\d{2,3}\s*[—–:-]\s*', ''
        }

        $id = "$number-$slug"
        [pscustomobject]@{
            Number  = $number
            Value   = [int]$number
            Slug    = $slug
            Id      = $id
            Title   = $title.Trim()
            File    = "features/$($file.Name)"
            Branch  = "feature/$id"
            SpecDir = "specs/$id"
        }
    }
    return @($items | Sort-Object Value)
}

# Range syntax: "all", "3", "00-03", "02,04", "00-02 07". A range (a-b, all) never
# pulls in files numbered 90 or above: those hold deferred candidates, so they run
# only when named on their own.
function Select-TokenEntries {
    param(
        [Parameter(Mandatory)][object[]]$Catalog,
        [Parameter(Mandatory)][string]$Token
    )
    if ($Token -eq 'all') {
        return @($Catalog | Where-Object { $_.Value -lt 90 })
    }
    if ($Token -match '^(\d+)-(\d+)$') {
        $from = [int]$Matches[1]; $to = [int]$Matches[2]
        if ($from -gt $to) { throw "Range '$Token' goes backwards." }
        $hits = @($Catalog | Where-Object { $_.Value -ge $from -and $_.Value -le $to -and $_.Value -lt 90 })
        if ($hits.Count -eq 0) { throw "Range '$Token' matches no feature file." }
        return $hits
    }
    if ($Token -match '^\d+$') {
        $value = [int]$Token
        $hit = @($Catalog | Where-Object { $_.Value -eq $value })
        if ($hit.Count -eq 0) {
            $known = ($Catalog | ForEach-Object { $_.Number }) -join ', '
            throw "No feature file numbered $Token. Available: $known"
        }
        return $hit
    }
    throw "Cannot read '$Token' in the feature range. Use forms like 00-03, 5, 02,04 or all."
}

function Select-FeatureRange {
    param(
        [Parameter(Mandatory)][object[]]$Catalog,
        [Parameter(Mandatory)][string]$Range
    )

    $normalized = $Range -replace '\s*(\.\.|-)\s*', '-'
    $tokens = @($normalized -split '[,;\s]+' | Where-Object { $_ })
    if ($tokens.Count -eq 0) { throw 'Empty feature range.' }

    $picked = [System.Collections.Generic.List[object]]::new()
    foreach ($token in $tokens) {
        Select-TokenEntries -Catalog $Catalog -Token $token | ForEach-Object { $picked.Add($_) }
    }
    return @($picked | Sort-Object Value -Unique)
}

function Test-BranchExists {
    param([Parameter(Mandatory)][string]$Branch)
    git show-ref --verify --quiet "refs/heads/$Branch"
    return ($LASTEXITCODE -eq 0)
}

# Reads a repo-relative file from the working tree when $Branch is checked out,
# otherwise from the tip of $Branch. Returns $null when the file is absent.
function Get-BranchFile {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][string]$Branch,
        [Parameter(Mandatory)][string]$Path
    )
    if ((Get-CurrentBranchName) -eq $Branch) {
        $full = Join-Path $RepoRoot $Path
        if (Test-Path $full) { return (Get-Content $full -Raw) }
        return $null
    }
    $content = git show "${Branch}:$Path" 2>$null
    if ($LASTEXITCODE -ne 0) { return $null }
    return ($content -join "`n")
}

# Where a feature stands: which artifacts exist and which step comes next.
function Get-FeatureState {
    param(
        [Parameter(Mandatory)][string]$RepoRoot,
        [Parameter(Mandatory)][object]$Feature,
        [Parameter(Mandatory)][string]$BaseBranch
    )

    $closed = git log $BaseBranch --format=%s --fixed-strings --grep "feat($($Feature.Id)):" 2>$null
    $done = -not [string]::IsNullOrWhiteSpace(($closed | Out-String))
    $branchExists = Test-BranchExists $Feature.Branch

    $hasSpec = $false; $hasPlan = $false; $hasTasks = $false
    $open = 0; $checked = 0
    $phases = @()
    if ($branchExists) {
        $hasSpec = $null -ne (Get-BranchFile $RepoRoot $Feature.Branch "$($Feature.SpecDir)/spec.md")
        $hasPlan = $null -ne (Get-BranchFile $RepoRoot $Feature.Branch "$($Feature.SpecDir)/plan.md")
        $tasks = Get-BranchFile $RepoRoot $Feature.Branch "$($Feature.SpecDir)/tasks.md"
        if ($null -ne $tasks) {
            $hasTasks = $true
            $open = [regex]::Matches($tasks, '(?m)^\s*[-*] \[ \]').Count
            $checked = [regex]::Matches($tasks, '(?m)^\s*[-*] \[[xX]\]').Count
            $phases = @(Get-TaskPhases -Text $tasks)
        }
    }

    $next =
        if ($done) { 'done' }
        elseif (-not $hasSpec) { 'specify' }
        elseif (-not $hasPlan) { 'plan' }
        elseif (-not $hasTasks) { 'tasks' }
        elseif ($open -gt 0 -or $checked -eq 0) { 'implement' }
        else { 'verify' }

    [pscustomobject]@{
        number       = $Feature.Number
        id           = $Feature.Id
        title        = $Feature.Title
        file         = $Feature.File
        branch       = $Feature.Branch
        specDir      = $Feature.SpecDir
        done         = $done
        branchExists = $branchExists
        hasSpec      = $hasSpec
        hasPlan      = $hasPlan
        hasTasks     = $hasTasks
        tasksOpen    = $open
        tasksChecked = $checked
        phases       = $phases
        nextStep     = $next
    }
}

# Phases of tasks.md: every "## Phase <n>: <title>" heading, with the complexity the tasks
# step records on a line "Complexity: low|medium|high - <reason>" under it, and the open and
# checked tasks up to the next "## " heading. A phase without the line has complexity null.
function Update-PhaseFromLine {
    param(
        [Parameter(Mandatory)][pscustomobject]$Phase,
        [Parameter(Mandatory)][AllowEmptyString()][string]$Line
    )
    $complexity = [regex]::Match($Line, '^\s*\**Complexity\**\s*:\s*\**(?<level>low|medium|high)\**\s*(?:[-:]+\s*(?<reason>.*))?$', 'IgnoreCase')
    if ($complexity.Success -and $null -eq $Phase.complexity) {
        $Phase.complexity = $complexity.Groups['level'].Value.ToLowerInvariant()
        $Phase.reason = $complexity.Groups['reason'].Value.Trim()
        return
    }
    $task = [regex]::Match($Line, '^\s*[-*] \[(?<mark>[ xX])\]\s+(?<id>T\d+)')
    if ($task.Success) {
        if ($null -eq $Phase.firstTask) { $Phase.firstTask = $task.Groups['id'].Value }
        $Phase.lastTask = $task.Groups['id'].Value
        if ($task.Groups['mark'].Value -eq ' ') { $Phase.open++ } else { $Phase.checked++ }
    }
}

function Get-TaskPhases {
    param([Parameter(Mandatory)][AllowEmptyString()][string]$Text)

    $result = [System.Collections.Generic.List[object]]::new()
    $current = $null
    foreach ($line in ($Text -split "`r?`n")) {
        $heading = [regex]::Match($line, '^##\s+Phase\s+(?<n>\d+)\s*[:.-]\s*(?<title>.+?)\s*$')
        if ($heading.Success) {
            $current = [pscustomobject]@{
                phase      = [int]$heading.Groups['n'].Value
                title      = $heading.Groups['title'].Value
                complexity = $null
                reason     = $null
                firstTask  = $null
                lastTask   = $null
                open       = 0
                checked    = 0
            }
            $result.Add($current)
            continue
        }
        if ($line -match '^##\s') { $current = $null; continue }
        if ($null -eq $current) { continue }
        Update-PhaseFromLine -Phase $current -Line $line
    }
    return $result
}

# Model Codex uses to answer clarification questions. An explicit -Model wins, then
# the SPECKIT_FULL_CODEX_MODEL environment variable, then this default. The value is
# a Codex model slug (bare family names such as "sol" are rejected by the CLI).
$script:DefaultCodexModel = 'gpt-6.2-sol'

function Resolve-CodexModel {
    param([string]$Model)
    if ($Model) { return $Model }
    if ($env:SPECKIT_FULL_CODEX_MODEL) { return $env:SPECKIT_FULL_CODEX_MODEL }
    return $script:DefaultCodexModel
}
