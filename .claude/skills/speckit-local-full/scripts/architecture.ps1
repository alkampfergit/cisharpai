<#
.SYNOPSIS
  Check the project-level architecture rules (P1 to P9 of docs/architecture-rules.md).
.DESCRIPTION
  Reads architecture-rules.json at the repository root and checks the folder layout,
  the set of projects, their project / package / framework references, project
  cycles, and that the architecture tests for the type rules exist. No build is
  needed. Each problem names the rule, the file and what to change.

  Exit code 0 when there are no violations, 1 otherwise.
.EXAMPLE
  pwsh -NoProfile -File architecture.ps1
#>
param([switch]$AsObject)

. (Join-Path $PSScriptRoot 'common.ps1')

$problems = [System.Collections.Generic.List[string]]::new()
$guide = 'See docs/architecture-rules.md.'

function Test-AnyPattern {
    param([string]$Value, [object[]]$Patterns)
    foreach ($pattern in @($Patterns)) { if ($pattern -and $Value -like $pattern) { return $true } }
    return $false
}

try {
    $root = Get-RepoRoot
    $rulesPath = Join-Path $root 'architecture-rules.json'
    if (-not (Test-Path $rulesPath)) { throw 'architecture-rules.json is missing at the repository root. Restore it from git; do not recreate it by hand.' }
    $rules = Get-Content $rulesPath -Raw | ConvertFrom-Json

    # Files git knows about or would add: ignored build output is not part of the layout.
    $files = @(git -C $root ls-files --cached --others --exclude-standard | Where-Object { $_ })

    # P1: root entries.
    $rootEntries = @($files | ForEach-Object { ($_ -split '/')[0] } | Sort-Object -Unique)
    foreach ($entry in $rootEntries) {
        if (-not (Test-AnyPattern $entry $rules.allowedRootEntries)) {
            $problems.Add("P1: '$entry' at the repository root is not an approved entry. Move it under src/, tests/ or docs/, or delete it. $guide")
        }
    }

    # P2: the set of projects and where they live.
    $projectFiles = @($files | Where-Object { $_ -match '\.(cs|vb|fs)proj$' })
    $known = @{}
    foreach ($rule in $rules.projects) { $known[$rule.name] = $rule }
    $loaded = @{}
    foreach ($path in $projectFiles) {
        $name = [System.IO.Path]::GetFileNameWithoutExtension($path)
        if (-not $known.ContainsKey($name)) {
            $problems.Add("P2: project '$path' is not one of the approved projects ($(($known.Keys | Sort-Object) -join ', ')). Put the code in one of them. $guide")
            continue
        }
        $expected = "$($known[$name].folder)/$name/$name.csproj"
        if ($path -ne $expected) {
            $problems.Add("P2: project '$path' must be at '$expected'.")
        }
        [xml]$xml = Get-Content (Join-Path $root $path) -Raw
        $loaded[$name] = [pscustomobject]@{
            Path       = $path
            Dir        = (Split-Path $path -Parent) -replace '\\', '/'
            Projects   = @($xml.SelectNodes('//ProjectReference') | ForEach-Object { [System.IO.Path]::GetFileNameWithoutExtension(($_.Include -replace '\\', '/')) })
            Packages   = @($xml.SelectNodes('//PackageReference') | ForEach-Object { $_.Include } | Where-Object { $_ })
            Frameworks = @($xml.SelectNodes('//FrameworkReference') | ForEach-Object { $_.Include } | Where-Object { $_ })
            Sdk        = "$($xml.Project.Sdk)"
        }
    }

    # P3: source files belong to a project.
    $projectDirs = @($loaded.Values | ForEach-Object { "$($_.Dir)/" })
    $skipSource = '^(\.claude|\.specify|specs|features|docs)/'
    foreach ($file in ($files | Where-Object { $_ -match '\.(cs|razor)$' -and $_ -notmatch $skipSource })) {
        if (-not ($projectDirs | Where-Object { $file.StartsWith($_) })) {
            $problems.Add("P3: source file '$file' is outside every project folder, so it is not built or tested. Move it into the project it belongs to.")
        }
    }

    # P4 to P7: references.
    foreach ($name in $loaded.Keys) {
        $project = $loaded[$name]; $rule = $known[$name]
        foreach ($reference in $project.Projects) {
            if ($reference -notin @($rule.allowedProjectReferences)) {
                $allowed = if (@($rule.allowedProjectReferences).Count) { @($rule.allowedProjectReferences) -join ', ' } else { 'no other project' }
                $problems.Add("P4: $($project.Path) references project '$reference'; $name may reference only: $allowed. Move the code that needs it, or invert the dependency with an interface in Core.")
            }
        }
        foreach ($package in $project.Packages) {
            if (Test-AnyPattern $package $rules.forbiddenPackagesEverywhere) {
                $problems.Add("P7: $($project.Path) references package '$package', which is banned in this repository (decisions D8 / D35). Remove it.")
            }
            elseif (Test-AnyPattern $package $rule.forbiddenPackages) {
                $ruleId = if ($name -like '*.Web') { 'P6' } else { 'P5' }
                $problems.Add("${ruleId}: $($project.Path) references package '$package', which $name must not use. Put that code in the project that owns the concern. $guide")
            }
        }
        foreach ($framework in $project.Frameworks) {
            if (Test-AnyPattern $framework $rule.forbiddenFrameworkReferences) {
                $problems.Add("P5: $($project.Path) has <FrameworkReference Include=`"$framework`" />; $name must build without the web framework. Reference the individual package you need instead.")
            }
        }
        if (@($rule.forbiddenFrameworkReferences) -contains 'Microsoft.AspNetCore.App' -and $project.Sdk -match '(?i)\.Web$|Razor') {
            $problems.Add("P5: $($project.Path) uses Sdk `"$($project.Sdk)`", which brings in the web framework; $name must use Microsoft.NET.Sdk.")
        }
    }

    # P8: project cycles.
    $state = @{}
    function Find-Cycle([string]$node, [string[]]$trail) {
        if ($state[$node] -eq 'done') { return }
        if ($state[$node] -eq 'open') {
            $start = [array]::IndexOf($trail, $node)
            $problems.Add("P8: project cycle: $((@($trail[$start..($trail.Count - 1)]) + $node) -join ' -> '). Break it by moving the shared code into Core.")
            return
        }
        $state[$node] = 'open'
        if ($loaded.ContainsKey($node)) {
            foreach ($next in $loaded[$node].Projects) { Find-Cycle $next ($trail + $node) }
        }
        $state[$node] = 'done'
    }
    foreach ($name in $loaded.Keys) { Find-Cycle $name @() }

    # P9: the architecture tests for the type rules exist.
    $archTests = $rules.architectureTests
    if ($loaded.ContainsKey($archTests.project)) {
        $testProject = $loaded[$archTests.project]
        if (-not (Test-AnyPattern $archTests.package $testProject.Packages) -and $archTests.package -notin $testProject.Packages) {
            $problems.Add("P9: $($testProject.Path) does not reference the package '$($archTests.package)'. Add it; the type rules A1 to A5 are checked with it.")
        }
        $testFiles = @($files | Where-Object { $_.StartsWith("$($testProject.Dir)/") -and (Split-Path $_ -Leaf) -like $archTests.filePattern })
        if ($testFiles.Count -eq 0) {
            $problems.Add("P9: no '$($archTests.filePattern)' file in $($testProject.Dir). Create ArchitectureTests.cs with one test per type rule; docs/architecture-rules.md has the shape.")
        }
        else {
            $text = ($testFiles | ForEach-Object { Get-Content (Join-Path $root $_) -Raw }) -join "`n"
            foreach ($id in $archTests.requiredRuleIds) {
                if ($text -notmatch "(?m)\bvoid\s+${id}_\w+\s*\(") {
                    $problems.Add("P9: no architecture test for rule $id. Add a test method named ${id}_<what it checks> to $($testFiles[0]); the rule is described in docs/architecture-rules.md.")
                }
            }
            if ($text -match '(?m)^\s*\[(Ignore|Explicit)\b') {
                $problems.Add("P9: an architecture test is marked [Ignore] or [Explicit] in $($testFiles[0]). Architecture tests always run: remove the attribute and fix the violation it was hiding.")
            }
        }
    }
}
catch {
    $problems.Add("architecture: $($_.Exception.Message)")
}

$outcome = [pscustomobject]@{ ok = ($problems.Count -eq 0); problems = @($problems) }
if ($AsObject) { return $outcome }
$outcome | ConvertTo-Json -Depth 4
exit ($outcome.ok ? 0 : 1)
