<#
.SYNOPSIS
    Run integration tests for one or more providers.
.DESCRIPTION
    Runs integration tests scoped to specific providers so you only spend API
    credits on the providers you actually changed.
.PARAMETER Provider
    One or more provider names: OpenAi, Anthropic, Cohere, AzureOpenAi, AzureAiInference.
    Omit to run all providers.
.EXAMPLE
    .\scripts\run-integration-tests.ps1 -Provider OpenAi
.EXAMPLE
    .\scripts\run-integration-tests.ps1 -Provider Cohere,Anthropic
.EXAMPLE
    .\scripts\run-integration-tests.ps1
    # Runs all providers
#>
param(
    [ValidateSet("OpenAi", "Anthropic", "Cohere", "AzureOpenAi", "AzureAiInference")]
    [string[]] $Provider
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)
$project = "$repoRoot/src/Cisharpai.Integration.Tests/Cisharpai.Integration.Tests.csproj"

if ($Provider.Count -eq 0) {
    Write-Host "Running ALL integration tests..." -ForegroundColor Cyan
    dotnet test $project --framework net10.0 -v q --logger "console;verbosity=normal"
} else {
    $filter = ($Provider | ForEach-Object {
        "FullyQualifiedName~Cisharpai.Integration.Tests.$_"
    }) -join " | "

    Write-Host "Running integration tests for: $($Provider -join ', ')" -ForegroundColor Cyan
    Write-Host "Filter: $filter" -ForegroundColor DarkGray
    dotnet test $project --framework net10.0 --filter "$filter" -v q --logger "console;verbosity=normal"
}

exit $LASTEXITCODE
