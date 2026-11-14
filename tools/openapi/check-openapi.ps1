<#
.SYNOPSIS
  OpenAPI contract gate: regenerate -> drift check -> lint -> breaking-change check.

.DESCRIPTION
  specs/openapi.json is generated from code by `dotnet build` (Microsoft.Extensions.ApiDescription.Server).
  This script fails (non-zero exit) when:
    1. the regenerated spec differs from what is committed/staged      (forgot to commit spec)
    2. `redocly lint` reports an error                                  (contract rule violated)
    3. `oasdiff breaking` finds a breaking change vs the base ref       (when installed or -RequireOasdiff)

  It never runs docker and never touches git state (no add/commit/push).
#>
[CmdletBinding()]
param(
    [string] $BaseRef = 'origin/main',
    [string] $Configuration = 'Debug',
    [switch] $SkipBuild,
    [switch] $NoRestore,
    [switch] $RequireOasdiff
)

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')
$specPath = 'specs/openapi.json'
$project  = 'backend-v2/src/SmkDoc.Api/SmkDoc.Api.csproj'
$failures = [System.Collections.Generic.List[string]]::new()

Push-Location $repoRoot
try {
    # Step 1: Regenerate the spec from code
    if (-not $SkipBuild) {
        Write-Host '==> [1/4] Regenerating spec (dotnet build)' -ForegroundColor Cyan
        $buildArgs = @('build', $project, '-c', $Configuration, '--nologo', '-v', 'quiet', '-nr:false', '-m:1')
        if ($NoRestore) {
            $buildArgs += '--no-restore'
        }
        & dotnet @buildArgs
        if ($LASTEXITCODE -ne 0) { throw 'dotnet build failed' }
    }

    # Step 2: Drift — generated spec must equal the working-tree/committed one
    Write-Host '==> [2/4] Drift check' -ForegroundColor Cyan
    git diff --exit-code --stat -- $specPath
    if ($LASTEXITCODE -ne 0) {
        $failures.Add("Spec drift: $specPath changed after build. Review the diff and commit it.")
    }

    # Step 3: Lint
    Write-Host '==> [3/4] Redocly lint' -ForegroundColor Cyan
    $redoclyCmd = Get-Command redocly -ErrorAction SilentlyContinue
    if ($redoclyCmd) {
        redocly lint $specPath
    }
    else {
        npx --yes @redocly/cli@latest lint $specPath
    }
    if ($LASTEXITCODE -ne 0) { $failures.Add('Redocly lint reported errors.') }

    # Step 4: Breaking changes vs baseline
    Write-Host "==> [4/4] Breaking-change check vs $BaseRef" -ForegroundColor Cyan
    $oasdiff = Get-Command oasdiff -ErrorAction SilentlyContinue
    if (-not $oasdiff) {
        $msg = 'oasdiff not installed — breaking-change check could not run.'
        if ($RequireOasdiff) {
            $failures.Add($msg)
        }
        else {
            Write-Warning "$msg Install: https://github.com/oasdiff/oasdiff#installation"
        }
    }
    else {
        $baseline = Join-Path ([System.IO.Path]::GetTempPath()) 'openapi.base.json'
        $baseContent = git show "${BaseRef}:${specPath}" 2>$null
        if ($LASTEXITCODE -ne 0) {
            Write-Warning "No baseline $specPath at $BaseRef — breaking-change check SKIPPED."
        }
        else {
            [System.IO.File]::WriteAllLines($baseline, $baseContent, [System.Text.UTF8Encoding]::new($false))
            oasdiff breaking $baseline $specPath --fail-on ERR
            if ($LASTEXITCODE -ne 0) { $failures.Add("Breaking API change vs $BaseRef.") }
        }
    }
}
finally {
    Pop-Location
}

if ($failures.Count -gt 0) {
    Write-Host ''
    $failures | ForEach-Object { Write-Host "FAIL: $_" -ForegroundColor Red }
    exit 1
}

Write-Host 'OpenAPI contract checks passed.' -ForegroundColor Green
