<#
.SYNOPSIS
  Work out which Claude model runs each step of a speckit-local-full run.
.DESCRIPTION
  Defaults come from models.json next to SKILL.md. The run's arguments may override
  them with a "models:" part, for example
      02-03 models: specify=opus tasks=haiku implement=sonnet
  Steps: constitution, specify, plan, tasks, implement, fix, review.
  Models: opus, sonnet, haiku, fable; "auto" is accepted for implement only and means
  one model per phase of tasks.md, chosen from the phase's complexity
  (low -> haiku, medium or high -> sonnet), escalating on a failed attempt.
  Prints the effective map as JSON, with "overridden" listing the steps the user set.
.EXAMPLE
  pwsh -NoProfile -File resolve-models.ps1 -Arguments "02-03 models: implement=sonnet"
#>
param([string]$Arguments = '')

$ErrorActionPreference = 'Stop'

try {
    $defaultsPath = Join-Path (Split-Path $PSScriptRoot -Parent) 'models.json'
    $defaults = Get-Content $defaultsPath -Raw | ConvertFrom-Json -AsHashtable
    $steps = @('constitution', 'specify', 'plan', 'tasks', 'implement', 'fix', 'review')
    $models = @('opus', 'sonnet', 'haiku', 'fable')

    foreach ($step in $steps) {
        if (-not $defaults.ContainsKey($step)) { throw "models.json has no entry for '$step'." }
    }

    $effective = [ordered]@{}
    foreach ($step in $steps) { $effective[$step] = $defaults[$step] }
    $overridden = @()

    $match = [regex]::Match($Arguments, '(?i)\bmodels\s*:\s*(?<pairs>.*)$')
    if ($match.Success) {
        foreach ($pair in ($match.Groups['pairs'].Value -split '[\s,]+' | Where-Object { $_ })) {
            $parts = $pair -split '=', 2
            if ($parts.Count -ne 2) { throw "'$pair' is not step=model." }
            $step = $parts[0].Trim().ToLowerInvariant()
            $model = $parts[1].Trim().ToLowerInvariant()
            if ($step -notin $steps) { throw "Unknown step '$step'. Steps: $($steps -join ', ')." }
            $allowed = if ($step -eq 'implement') { $models + 'auto' } else { $models }
            if ($model -notin $allowed) { throw "Model '$model' is not allowed for $step. Allowed: $($allowed -join ', ')." }
            $effective[$step] = $model
            $overridden += $step
        }
    }

    foreach ($step in $steps) {
        $allowed = if ($step -eq 'implement') { $models + 'auto' } else { $models }
        if ($effective[$step] -notin $allowed) { throw "models.json: '$($effective[$step])' is not allowed for $step." }
    }

    [pscustomobject]@{
        models     = $effective
        overridden = $overridden
        autoRule   = 'implement=auto: phase complexity low -> haiku, medium or high -> sonnet, no marker -> sonnet; a failed attempt escalates haiku -> sonnet -> opus'
    } | ConvertTo-Json -Depth 4
}
catch {
    [Console]::Error.WriteLine("resolve-models: $($_.Exception.Message)")
    exit 1
}
