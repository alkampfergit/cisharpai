<#
.SYNOPSIS
  Prove that the Codex CLI can really answer, by sending it a one-word request.
.DESCRIPTION
  `codex login status` reports "Logged in" even when the refresh token has expired,
  so the only reliable check is a real call. Exit code 0 means Codex answered.
.EXAMPLE
  pwsh -NoProfile -File check-codex.ps1
#>
param([string]$Model)

. (Join-Path $PSScriptRoot 'common.ps1')

try {
    if (-not (Get-Command codex -ErrorAction SilentlyContinue)) { throw 'The codex CLI is not on PATH.' }

    $out = Join-Path ([System.IO.Path]::GetTempPath()) "speckit-local-full-codex-probe-$PID.txt"
    $codexArgs = @('exec', '--sandbox', 'read-only', '--ephemeral', '--skip-git-repo-check', '--color', 'never', '-o', $out)
    $Model = Resolve-CodexModel $Model
    $codexArgs += @('-m', $Model)
    $codexArgs += '-'

    $log = 'Reply with the single word READY and nothing else.' | codex @codexArgs 2>&1 | Out-String
    $code = $LASTEXITCODE
    $answer = if (Test-Path $out) { (Get-Content $out -Raw) } else { '' }
    Remove-Item $out -Force -ErrorAction SilentlyContinue

    if ($code -ne 0 -or [string]::IsNullOrWhiteSpace($answer)) {
        $errors = @($log -split "`n" | Where-Object { $_ -match '^ERROR' } | ForEach-Object { $_.Trim() } | Select-Object -Unique -First 3)
        if ($errors.Count -eq 0) { $errors = @($log -split "`n" | Where-Object { $_.Trim() } | Select-Object -Last 3) }
        throw "Codex did not answer (exit $code). $($errors -join ' ')"
    }

    [pscustomobject]@{ ok = $true; model = $Model; reply = $answer.Trim() } | ConvertTo-Json -Compress
}
catch {
    [Console]::Error.WriteLine("check-codex: $($_.Exception.Message)")
    exit 1
}
