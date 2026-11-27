<#
.SYNOPSIS
  Build with warnings treated as errors and run the tests; print a compact verdict.
.DESCRIPTION
  Full build and test output goes to log files so it never floods the caller's
  context; only the verdict and the offending lines are printed. Exit code 0 only
  when warnings-as-errors is configured, the build is clean and the tests pass.
.EXAMPLE
  pwsh -NoProfile -File verify.ps1
#>
param(
    # Debug on purpose: the ArchUnitNET architecture tests read the compiled assemblies,
    # and Release optimisations can remove dependencies they are meant to see.
    [string]$Configuration = 'Debug',
    [string]$LogDir
)

. (Join-Path $PSScriptRoot 'common.ps1')

$result = [ordered]@{
    ok                  = $false
    solution            = $null
    architecture        = 'not run'
    warningsAsErrorsSet = $false
    build               = 'not run'
    tests               = 'not run'
    testsDetected       = $false
    problems            = @()
    details             = @()
    logs                = @()
}

function Complete-Verify {
    $result.ok = ($result.problems.Count -eq 0)
    [pscustomobject]$result | ConvertTo-Json -Depth 5
    exit ($result.ok ? 0 : 1)
}

try {
    $root = Get-RepoRoot
    if (-not $LogDir) { $LogDir = Join-Path ([System.IO.Path]::GetTempPath()) 'speckit-local-full' }
    New-Item -ItemType Directory -Force $LogDir | Out-Null
    $skip = '[\\/](bin|obj|node_modules|\.git|\.specify|\.claude)[\\/]'

    # Architecture rules first: they need no build, and a structural mistake is cheaper
    # to fix before the code is compiled around it.
    $architecture = pwsh -NoProfile -File (Join-Path $PSScriptRoot 'architecture.ps1') | Out-String | ConvertFrom-Json
    $result.architecture = $architecture.ok ? 'pass' : 'fail'
    if (-not $architecture.ok) { $result.problems += @($architecture.problems) }

    $solutions = @(Get-ChildItem $root -Recurse -Depth 3 -File -Include *.sln, *.slnx |
        Where-Object { $_.FullName -notmatch $skip } |
        Sort-Object { $_.FullName.Split([char[]]'\/').Count }, FullName)
    if ($solutions.Count -eq 0) {
        $result.problems += 'No .sln or .slnx solution found, so there is nothing to build.'
        Complete-Verify
    }
    $solution = $solutions[0].FullName
    $result.solution = [System.IO.Path]::GetRelativePath($root, $solution)

    # The setting has to live in the repository, so that every build fails on a
    # warning, not only the one this script runs.
    $props = @(Get-ChildItem $root -Recurse -Depth 3 -File -Filter 'Directory.Build.props' |
        Where-Object { $_.FullName -notmatch $skip })
    $propsText = ($props | ForEach-Object { Get-Content $_.FullName -Raw }) -join "`n"
    # Drop XML comments so a commented-out setting does not count as present.
    $propsText = [regex]::Replace($propsText, '(?s)<!--.*?-->', '')

    # The build-time sensors. Each entry: property, the value pattern that keeps the
    # sensor on, and the line to restore when it is missing or weakened.
    $required = @(
        @{ Name = 'TreatWarningsAsErrors';   Pattern = 'true';                        Fix = '<TreatWarningsAsErrors>true</TreatWarningsAsErrors>' }
        @{ Name = 'Nullable';                Pattern = 'enable';                      Fix = '<Nullable>enable</Nullable>' }
        @{ Name = 'AnalysisLevel';           Pattern = '[\w.]+-(recommended|all)';    Fix = '<AnalysisLevel>10.0-recommended</AnalysisLevel>' }
        @{ Name = 'AnalysisLevelSecurity';   Pattern = '[\w.]+-all';                  Fix = '<AnalysisLevelSecurity>10.0-all</AnalysisLevelSecurity>' }
        @{ Name = 'EnforceCodeStyleInBuild'; Pattern = 'true';                       Fix = '<EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>' }
    )
    foreach ($setting in $required) {
        $found = $propsText -match "(?i)<$($setting.Name)>\s*$($setting.Pattern)\s*</$($setting.Name)>"
        if ($setting.Name -eq 'TreatWarningsAsErrors') { $result.warningsAsErrorsSet = $found }
        if (-not $found) {
            $result.problems += "Build sensor missing or weakened: Directory.Build.props at the repository root must contain $($setting.Fix). Restore that line; do not relax it to make the build pass."
        }
    }
    # Banned-API sensor: the list, its wiring and the analyzer package must all be present.
    if (-not (Test-Path (Join-Path $root 'BannedSymbols.txt'))) {
        $result.problems += 'Build sensor missing: BannedSymbols.txt was removed from the repository root. Restore it from git.'
    }
    if ($propsText -notmatch '(?i)<AdditionalFiles\s+Include="[^"]*BannedSymbols\.txt"') {
        $result.problems += 'Build sensor missing: Directory.Build.props must contain <AdditionalFiles Include="$(MSBuildThisFileDirectory)BannedSymbols.txt" />. Restore that line.'
    }
    $packagesProps = Join-Path $root 'Directory.Packages.props'
    $packagesText = if (Test-Path $packagesProps) { [regex]::Replace((Get-Content $packagesProps -Raw), '(?s)<!--.*?-->', '') } else { '' }
    if ($packagesText -notmatch '(?i)<GlobalPackageReference\s+Include="Microsoft\.CodeAnalysis\.BannedApiAnalyzers"') {
        $result.problems += 'Build sensor missing: Directory.Packages.props must contain <GlobalPackageReference Include="Microsoft.CodeAnalysis.BannedApiAnalyzers" Version="..." />. Restore it; without the analyzer BannedSymbols.txt is not enforced.'
    }

    if ($packagesText -notmatch '(?i)<GlobalPackageReference\s+Include="SonarAnalyzer\.CSharp"') {
        $result.problems += 'Build sensor missing: Directory.Packages.props must contain <GlobalPackageReference Include="SonarAnalyzer.CSharp" Version="..." />. Restore it; it runs the Sonar rules in the build.'
    }

    foreach ($weakener in 'NoWarn', 'WarningsNotAsErrors', 'CodeAnalysisTreatWarningsAsErrors', 'EnableNETAnalyzers', 'RunAnalyzers') {
        if ($propsText -match "(?i)<$weakener>") {
            $result.problems += "Directory.Build.props contains <$weakener>, which switches a build sensor off for every project. Remove it and fix the diagnostics it was hiding."
        }
    }

    $buildLog = Join-Path $LogDir 'build.log'
    dotnet build $solution -c $Configuration -warnaserror --nologo -v minimal *> $buildLog
    $result.logs += $buildLog
    if ($LASTEXITCODE -eq 0) {
        $result.build = 'pass'
    }
    else {
        $result.build = 'fail'
        $result.problems += 'Build failed (warnings count as errors).'
        $result.details += @(Get-Content $buildLog | Where-Object { $_ -match ':\s*(error|warning)\s' } |
            ForEach-Object { $_.Trim() } | Select-Object -Unique -First 30)
        Complete-Verify
    }

    $testLog = Join-Path $LogDir 'test.log'
    dotnet test $solution -c $Configuration --no-build --nologo *> $testLog
    $testExit = $LASTEXITCODE
    $result.logs += $testLog
    $testLines = @(Get-Content $testLog)
    $result.testsDetected = [bool]($testLines | Where-Object { $_ -match '(?i)(passed!|failed!|total tests:\s*\d+|total:\s*\d+)' })

    if ($testExit -eq 0) {
        $result.tests = 'pass'
        $result.details += @($testLines | Where-Object { $_ -match '(?i)(passed!|total|skipped)' } |
            ForEach-Object { $_.Trim() } | Select-Object -Last 8)
    }
    else {
        $result.tests = 'fail'
        $result.problems += 'Tests failed.'
        $result.details += @($testLines | Where-Object { $_ -match '(?i)(failed|error|passed!|total)' } |
            ForEach-Object { $_.Trim() } | Select-Object -Unique -First 40)
    }
    if (-not $result.testsDetected) {
        $result.problems += 'The test run reported no tests; unit tests are mandatory in this project.'
    }

    Complete-Verify
}
catch {
    $result.problems += "verify: $($_.Exception.Message)"
    Complete-Verify
}
