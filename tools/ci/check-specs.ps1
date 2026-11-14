<#
.SYNOPSIS
  Checks AGENTS.md and active specs/ Markdown files for broken links and retired legacy symbols.

.DESCRIPTION
  Runs cross-platform in both Windows PowerShell 5.1 and PowerShell 7 (pwsh).
  Excludes historical snapshot folder specs/**/_review/.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')

$files = [System.Collections.Generic.List[string]]::new()
$agentsPath = Join-Path $repoRoot 'AGENTS.md'
if (Test-Path $agentsPath) {
    $files.Add((Resolve-Path $agentsPath).Path)
}

$specsRoot = Join-Path $repoRoot 'specs'
if (Test-Path $specsRoot) {
    Get-ChildItem -Path $specsRoot -Filter '*.md' -Recurse -File |
        Where-Object { $_.FullName -notmatch '([\\/])_review([\\/]|$)' } |
        ForEach-Object { $files.Add($_.FullName) }
}

$brokenLinks = [System.Collections.Generic.List[string]]::new()
$legacyHits  = [System.Collections.Generic.List[string]]::new()

$inlineCodePattern = [regex]'`[^`]*`'
$linkPattern       = [regex]'\[[^\]]+\]\(([^)]+)\)'
$legacyPatterns    = @(
    @{ Name = 'ResultEnvelope<'; Regex = [regex]'ResultEnvelope<' },
    @{ Name = 'ProjectDbContext'; Regex = [regex]'\bProjectDbContext\b' },
    @{ Name = 'IStorageService byte[]'; Regex = [regex]'IStorageService\.[A-Za-z]+\(byte\[\]' }
)

foreach ($file in $files) {
    $lines = Get-Content -Path $file -Encoding UTF8
    $dir = Split-Path -Path $file -Parent
    $inFence = $false

    for ($i = 0; $i -lt $lines.Count; $i++) {
        $line = $lines[$i]
        if ($line -match '^\s*```') {
            $inFence = -not $inFence
            continue
        }
        if ($inFence) {
            continue
        }

        $lineWithoutCode = $inlineCodePattern.Replace($line, '')
        foreach ($m in $linkPattern.Matches($lineWithoutCode)) {
            $rawTarget = $m.Groups[1].Value.Trim()
            if ([string]::IsNullOrWhiteSpace($rawTarget)) { continue }
            if ($rawTarget -match '^(https?://|mailto:|file://|#)') { continue }
            if ($rawTarget -match '^\{[^}]+\}$') { continue }

            $targetPart = ($rawTarget -split '#', 2)[0]
            $targetPart = ($targetPart -split '\s+', 2)[0].Trim('<', '>')
            if ([string]::IsNullOrWhiteSpace($targetPart)) { continue }

            $resolved = Join-Path $dir $targetPart
            if (-not (Test-Path $resolved)) {
                $relFile = $file.Substring($repoRoot.Path.Length).TrimStart('\', '/')
                $brokenLinks.Add("${relFile}:$($i + 1) -> $rawTarget")
            }
        }

        foreach ($lp in $legacyPatterns) {
            if ($lp.Regex.IsMatch($line)) {
                $relFile = $file.Substring($repoRoot.Path.Length).TrimStart('\', '/')
                $legacyHits.Add("${relFile}:$($i + 1) [$($lp.Name)]: $($line.Trim())")
            }
        }
    }
}

Write-Host "Checked $($files.Count) specification Markdown files." -ForegroundColor Cyan

if ($brokenLinks.Count -gt 0) {
    Write-Host 'Broken relative links found:' -ForegroundColor Red
    $brokenLinks | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

if ($legacyHits.Count -gt 0) {
    Write-Host 'Retired legacy symbols found outside _review/:' -ForegroundColor Red
    $legacyHits | ForEach-Object { Write-Host "  - $_" -ForegroundColor Red }
}

if ($brokenLinks.Count -gt 0 -or $legacyHits.Count -gt 0) {
    exit 1
}

Write-Host 'ALL_LINKS_VALID (0 broken links, 0 legacy symbol hits).' -ForegroundColor Green
