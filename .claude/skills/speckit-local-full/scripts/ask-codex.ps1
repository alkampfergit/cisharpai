<#
.SYNOPSIS
  Have the Codex CLI answer Spec Kit clarification questions in place of the user.
.DESCRIPTION
  Codex runs read-only inside the repository, reads the backlog (features/README.md
  holds the settled decisions) and the feature file, and writes one answer per
  question to -AnswersFile. Exit code 0 means the answers file was written.
.EXAMPLE
  pwsh -NoProfile -File ask-codex.ps1 -QuestionsFile specs/03-full-import/clarifications/questions.md `
       -AnswersFile specs/03-full-import/clarifications/answers.md -FeatureFile features/03-full-import.md
#>
param(
    [Parameter(Mandatory)][string]$QuestionsFile,
    [Parameter(Mandatory)][string]$AnswersFile,
    [Parameter(Mandatory)][string]$FeatureFile,
    [string]$SpecFile,
    [string]$Model
)

. (Join-Path $PSScriptRoot 'common.ps1')

try {
    $root = Get-RepoRoot
    if (-not (Get-Command codex -ErrorAction SilentlyContinue)) { throw 'The codex CLI is not on PATH.' }
    if (-not (Test-Path $QuestionsFile)) { throw "Questions file not found: $QuestionsFile" }

    $questions = Get-Content $QuestionsFile -Raw
    if ([string]::IsNullOrWhiteSpace($questions)) { throw 'The questions file is empty.' }

    $specLine = if ($SpecFile) { "- $SpecFile : the specification being written; the questions come from it." } else { '' }

    $prompt = @"
You are standing in for the product owner of this repository, who is not available.
One backlog feature is being specified, planned, built or reviewed, and the author
has questions. Answer every one of them: nobody else will, so "ask the user" or
"it depends" is not an acceptable answer.

One kind of question you must not settle: a change to the architecture or to the
build and test harness (docs/architecture-rules.md, architecture-rules.json, the
architecture tests, Directory.Build.props, the analyzers, BannedSymbols.txt,
.editorconfig, stryker-config.json, global.json, .specify/, .claude/skills/, or the
limits and gates of the run itself). For such a question answer with Choice
"Needs ADR" and say in the Answer what the Architecture Decision Record must
decide; the author will stop and write it for the product owner.

Read these files in the repository before answering:
- features/README.md : the backlog index. Its "Fixed constraints" and the decisions
  D1 to D35 are settled and outrank any suggested option.
- $FeatureFile : the feature being specified, with what was already decided for it.
$specLine
You may read other files under features/ when a question touches another feature.

How to choose:
1. If a recorded decision or the feature file settles the question, give that answer
   and cite it (for example "D12" or "03-full-import.md, Decided").
2. Otherwise pick the option that keeps this feature's scope smallest while staying
   consistent with the recorded decisions, and say it is your own judgment.
3. If none of the suggested options fits, answer "Custom" and write the answer.

Do not modify any file. Reply with only this, one block per question, in order:

## Q<number>
**Choice**: <option letter, Custom, or Needs ADR>
**Answer**: <the answer in one or two sentences, written so it can be pasted into the spec>
**Basis**: <decision id / file section, or "judgment: " followed by one line of reasoning>

The questions:

$questions
"@

    $answersFull = [System.IO.Path]::GetFullPath($AnswersFile, (Get-Location).Path)
    $answersDir = Split-Path $answersFull -Parent
    if (-not (Test-Path $answersDir)) { New-Item -ItemType Directory -Force $answersDir | Out-Null }
    if (Test-Path $answersFull) { Remove-Item $answersFull -Force }

    $codexArgs = @('exec', '--sandbox', 'read-only', '--ephemeral', '--color', 'never', '-C', $root, '-o', $answersFull)
    $Model = Resolve-CodexModel $Model
    $codexArgs += @('-m', $Model)
    $codexArgs += '-'

    $log = $prompt | codex @codexArgs 2>&1 | Out-String
    $code = $LASTEXITCODE

    $written = (Test-Path $answersFull) -and -not [string]::IsNullOrWhiteSpace((Get-Content $answersFull -Raw))
    if ($code -ne 0 -or -not $written) {
        $errors = @($log -split "`n" | Where-Object { $_ -match '^ERROR' } | ForEach-Object { $_.Trim() } | Select-Object -Unique -First 3)
        if ($errors.Count -eq 0) { $errors = @($log -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 5) }
        throw "codex exec failed (exit $code) or wrote no answer. $($errors -join ' ')"
    }

    Write-Output "Answers written to $answersFull (model: $Model)"
    Get-Content $answersFull -Raw
}
catch {
    [Console]::Error.WriteLine("ask-codex: $($_.Exception.Message)")
    exit 1
}
