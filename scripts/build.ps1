param(
    [string] $nugetApiKey = "",
    [bool]   $nugetPublish = $false,
    [switch] $skiptest
)

# Halt on any error
$ErrorActionPreference = "Stop"

$runningDirectory = Split-Path -Parent -Path $MyInvocation.MyCommand.Definition
$rootDirectory = Split-Path -Parent -Path $runningDirectory
$solutionFile = "$rootDirectory/Cisharpai.sln"

$nugetTempDir = "$rootDirectory/artifacts/NuGet"
$testResultsDir = "$rootDirectory/artifacts/TestResults"

# Clean artifact directories
foreach ($dir in @($nugetTempDir, $testResultsDir)) {
    if (Test-Path $dir) {
        Write-Host "Cleaning $dir"
        Remove-Item $dir -Recurse -Force
    }
}

# ==================== TOOL RESTORE ====================
Write-Host "`n`n*******************RESTORING TOOLS*******************"
Push-Location $rootDirectory
try {
    dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "Unable to restore dotnet tools." }
}
finally {
    Pop-Location
}

# ==================== GITVERSION ====================
Write-Host "`n`n*******************RUNNING GITVERSION*******************"
Push-Location $rootDirectory
try {
    $gitVersionOutput = dotnet tool run dotnet-gitversion /output json /config GitVersion.yml | Out-String
    if ($LASTEXITCODE -ne 0) { throw "GitVersion failed." }
}
finally {
    Pop-Location
}

$version = $gitVersionOutput | ConvertFrom-Json
Write-Host "GitVersion output:"
Write-Host ($version | ConvertTo-Json -Depth 5)

$assemblyVer = $version.AssemblySemVer
$assemblyFileVersion = $version.AssemblySemFileVer
$nugetPackageVersion = $version.SemVer
$assemblyInformationalVersion = $version.InformationalVersion

Write-Host ""
Write-Host "assemblyVer                    = $assemblyVer"
Write-Host "assemblyFileVersion            = $assemblyFileVersion"
Write-Host "nugetPackageVersion            = $nugetPackageVersion"
Write-Host "assemblyInformationalVersion   = $assemblyInformationalVersion"

# ==================== RESTORE ====================
Write-Host "`n`n*******************RESTORING PACKAGES*******************"
dotnet restore $solutionFile
if ($LASTEXITCODE -ne 0) { throw "Error restoring packages." }

# ==================== BUILD ====================
Write-Host "`n`n*******************BUILDING SOLUTION*******************"
dotnet build $solutionFile `
    --configuration Release `
    --no-restore `
    /p:AssemblyVersion=$assemblyVer `
    /p:FileVersion=$assemblyFileVersion `
    /p:InformationalVersion=$assemblyInformationalVersion
if ($LASTEXITCODE -ne 0) { throw "Error building solution." }

# ==================== TEST ====================
Write-Host "`n`n*******************TESTING SOLUTION*******************"
if (-not $skiptest) {
    $frameworkList = @("net8.0", "net10.0")

    # Only run unit tests (integration tests require API keys)
    $testProjects = @(
        "$rootDirectory/src/Cisharpai.Tests/Cisharpai.Tests.csproj"
    )

    foreach ($tfm in $frameworkList) {
        Write-Host "`nRunning tests for framework: $tfm"
        $resultsDir = "$testResultsDir/$tfm"
        New-Item -ItemType Directory -Force -Path $resultsDir | Out-Null

        foreach ($proj in $testProjects) {
            $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
            Write-Host "Running tests for project: $projName ($tfm)"

            dotnet test $proj `
                --configuration Release `
                --no-build `
                -f $tfm `
                --logger "trx;LogFilePrefix=$projName-$tfm" `
                --results-directory $resultsDir

            if ($LASTEXITCODE -ne 0) { throw "Tests failed for $projName ($tfm)." }
        }

        Write-Host "Tests completed for framework: $tfm"
    }
}
else {
    Write-Host "Skipping tests due to -skiptest flag"
}

# ==================== PACK ====================
Write-Host "`n`n*******************PACKING NUGET PACKAGES*******************"
New-Item -ItemType Directory -Force -Path $nugetTempDir | Out-Null

$packProjects = @(
    "$rootDirectory/src/Cisharpai/Cisharpai.csproj",
    "$rootDirectory/src/Cisharpai.OpenAi/Cisharpai.OpenAi.csproj",
    "$rootDirectory/src/Cisharpai.Azure/Cisharpai.Azure.csproj",
    "$rootDirectory/src/Cisharpai.Anthropic/Cisharpai.Anthropic.csproj",
    "$rootDirectory/src/Cisharpai.Cohere/Cisharpai.Cohere.csproj",
    "$rootDirectory/src/Cisharpai.Testing/Cisharpai.Testing.csproj"
)

foreach ($proj in $packProjects) {
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj)
    Write-Host "Packing $projName (version: $nugetPackageVersion)"

    dotnet pack $proj `
        --configuration Release `
        --no-build `
        --output $nugetTempDir `
        /p:PackageVersion=$nugetPackageVersion `
        /p:AssemblyVersion=$assemblyVer `
        /p:FileVersion=$assemblyFileVersion `
        /p:InformationalVersion=$assemblyInformationalVersion

    if ($LASTEXITCODE -ne 0) { throw "Error packing $projName." }
}

Write-Host "`nPackages created in $nugetTempDir"
Get-ChildItem -Path $nugetTempDir -Filter "*.nupkg" | ForEach-Object { Write-Host "  $_" }
Get-ChildItem -Path $nugetTempDir -Filter "*.snupkg" | ForEach-Object { Write-Host "  $_ (symbols)" }

# ==================== PUBLISH ====================
if ($true -eq $nugetPublish) {
    Write-Host "`n`n*******************PUBLISHING NUGET PACKAGES*******************"
    if ([string]::IsNullOrEmpty($nugetApiKey)) {
        throw "nugetApiKey is required when publishing."
    }
    dotnet nuget push "$nugetTempDir/*.nupkg" `
        --source https://api.nuget.org/v3/index.json `
        --api-key $nugetApiKey `
        --skip-duplicate
    if ($LASTEXITCODE -ne 0) { throw "Error pushing NuGet packages." }
    Write-Host "Packages published successfully."
}

Write-Host "`n`nBuild completed successfully!"
Write-Host "  Version:  $nugetPackageVersion"
Write-Host "  Assembly: $assemblyVer"
Write-Host "  File:     $assemblyFileVersion"
