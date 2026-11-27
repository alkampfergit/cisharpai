<#
.SYNOPSIS
  Mutation-test the code changed since the base branch and print a compact verdict.
.DESCRIPTION
  A passing test suite only shows that the tests run. Stryker.NET changes the code
  under test in small ways (a mutant: > becomes >=, a block is emptied) and reruns
  the tests; a mutant that no test notices is behaviour nobody checks. This script
  runs Stryker for each unit-test project against the source projects it references,
  restricted to what changed since -Since, and lists the mutants that survived so
  that tests can be written for them.

  Settings (threshold, excluded files) are in stryker-config.json at the repository
  root. Integration-test projects are skipped: a database round trip per mutant is
  too slow to be useful.

  Exit code 0 when the mutation score reaches the break threshold or nothing
  changed; 1 otherwise.
.EXAMPLE
  pwsh -NoProfile -File mutation.ps1 -Since develop
#>
param(
    [string]$Since = 'develop',
    [string]$LogDir,
    [int]$MaxListed = 40
)

. (Join-Path $PSScriptRoot 'common.ps1')

$result = [ordered]@{
    ok        = $false
    score     = $null
    threshold = $null
    killed    = 0
    survived  = 0
    noTests   = 0
    projects  = @()
    problems  = @()
    survivors = @()
    logs      = @()
}

function Complete-Mutation {
    $result.ok = ($result.problems.Count -eq 0)
    [pscustomobject]$result | ConvertTo-Json -Depth 6
    exit ($result.ok ? 0 : 1)
}

try {
    $root = Get-RepoRoot
    if (-not $LogDir) { $LogDir = Join-Path ([System.IO.Path]::GetTempPath()) 'speckit-local-full' }
    $outRoot = Join-Path $LogDir 'stryker'
    if (Test-Path $outRoot) { Remove-Item $outRoot -Recurse -Force }
    New-Item -ItemType Directory -Force $outRoot | Out-Null

    $config = Join-Path $root 'stryker-config.json'
    if (-not (Test-Path $config)) {
        $result.problems += 'stryker-config.json is missing at the repository root. Restore it; it holds the mutation threshold.'
        Complete-Mutation
    }
    $result.threshold = [int](Get-Content $config -Raw | ConvertFrom-Json).'stryker-config'.thresholds.break

    Push-Location $root
    try { dotnet tool restore *> (Join-Path $LogDir 'tool-restore.log') } finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) {
        $result.problems += 'dotnet tool restore failed; dotnet-stryker must be listed in the tool manifest (dotnet-tools.json).'
        $result.logs += (Join-Path $LogDir 'tool-restore.log')
        Complete-Mutation
    }

    $skip = '[\\/](bin|obj|node_modules|\.git|\.specify|\.claude)[\\/]'
    $testProjects = @(Get-ChildItem $root -Recurse -Depth 4 -File -Filter '*Tests.csproj' |
        Where-Object { $_.FullName -notmatch $skip -and $_.Name -notmatch '(?i)Integration' })
    if ($testProjects.Count -eq 0) {
        $result.problems += 'No unit-test project (*Tests.csproj, not an integration one) was found, so nothing can be mutation-tested.'
        Complete-Mutation
    }

    $tested = 0
    foreach ($testProject in $testProjects) {
        [xml]$xml = Get-Content $testProject.FullName -Raw
        $references = @($xml.SelectNodes('//ProjectReference') | ForEach-Object { Split-Path $_.Include -Leaf } | Where-Object { $_ })
        foreach ($sourceProject in $references) {
            $name = "$($testProject.BaseName)--$([System.IO.Path]::GetFileNameWithoutExtension($sourceProject))"
            $outDir = Join-Path $outRoot $name
            $log = Join-Path $LogDir "stryker-$name.log"
            $result.projects += $sourceProject
            $result.logs += $log

            Push-Location $testProject.DirectoryName
            try {
                dotnet stryker --config-file $config --project $sourceProject "--since:$Since" --output $outDir *> $log
            }
            finally { Pop-Location }

            $report = Get-ChildItem $outDir -Recurse -Filter 'mutation-report.json' -ErrorAction SilentlyContinue | Select-Object -First 1
            if (-not $report) {
                $tail = (Get-Content $log -Tail 6 | ForEach-Object { $_.Trim() } | Where-Object { $_ }) -join ' | '
                if ($tail -match '(?i)no (mutants|changes)|nothing to (mutate|test)') { continue }
                $result.problems += "Stryker produced no report for $sourceProject. Last lines: $tail"
                continue
            }

            $data = Get-Content $report.FullName -Raw | ConvertFrom-Json
            foreach ($file in $data.files.PSObject.Properties) {
                $relative = [System.IO.Path]::GetRelativePath($root, $file.Name) -replace '\\', '/'
                $lines = $null
                foreach ($mutant in $file.Value.mutants) {
                    switch ($mutant.status) {
                        { $_ -in 'Killed', 'Timeout' } { $result.killed++; $tested++ }
                        'Survived' { $result.survived++; $tested++ }
                        'NoCoverage' { $result.noTests++; $tested++ }
                    }
                    if ($mutant.status -in 'Survived', 'NoCoverage') {
                        if ($null -eq $lines -and (Test-Path $file.Name)) { $lines = @(Get-Content $file.Name) }
                        $line = [int]$mutant.location.start.line
                        $code = if ($lines -and $line -le $lines.Count) { $lines[$line - 1].Trim() } else { '' }
                        $why = if ($mutant.status -eq 'NoCoverage') { 'no test executes this code' } else { 'tests still pass with this change' }
                        $result.survivors += "${relative}:$line [$($mutant.mutatorName)] `"$code`" -> `"$($mutant.replacement)`" ($why)"
                    }
                }
            }
        }
    }

    if ($tested -eq 0) {
        # Nothing changed in mutable code (for example a documentation-only feature).
        if ($result.problems.Count -eq 0) { $result.score = 100 }
        Complete-Mutation
    }

    $result.score = [math]::Round(100.0 * $result.killed / $tested, 1)
    $total = $result.survivors.Count
    if ($total -gt $MaxListed) {
        $result.survivors = @($result.survivors | Select-Object -First $MaxListed) + "... and $($total - $MaxListed) more; see the Stryker logs."
    }
    if ($result.score -lt $result.threshold) {
        $result.problems += "Mutation score $($result.score)% is below the $($result.threshold)% threshold: $($result.survived) mutant(s) survived and $($result.noTests) are in code no test runs. Add or strengthen unit tests so that each listed change makes a test fail (assert the boundary or the result the mutant alters). Do not delete the code, exclude files in stryker-config.json or lower the threshold."
    }
    Complete-Mutation
}
catch {
    $result.problems += "mutation: $($_.Exception.Message)"
    Complete-Mutation
}
