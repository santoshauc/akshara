<#
.SYNOPSIS
    Measures test coverage across the whole backend and reports ONE figure.

.DESCRIPTION
    The unit and integration suites cover different halves of the codebase: the
    calculators and validators are unit-tested, while the handlers, the API
    pipeline and anything touching the database are covered by integration tests
    against Testcontainers. Measuring either alone understates the suite badly,
    and the two runs each emit their own cobertura file, so a single number only
    exists once they are merged. That merge is what this script is for.

    Run it from the repository root. This dev box has no pwsh, so locally:

        powershell -ExecutionPolicy Bypass -File scripts/coverage.ps1

    CI invokes the very same file as `pwsh scripts/coverage.ps1` (ubuntu runners
    ship PowerShell 7), so the number in a pull request and the number on a
    developer's machine come out of identical steps rather than two copies that
    drift. It is written to the 5.1 language level for that reason.

    Prerequisite, once per machine:

        dotnet tool install -g dotnet-reportgenerator-globaltool

    KEEP THIS FILE ASCII-ONLY. Windows PowerShell 5.1 reads a file with no
    byte-order mark as CP1252, so a UTF-8 em dash arrives as three characters,
    the last of which is a curly double quote - which 5.1 treats as a string
    delimiter. The parser desynchronises and reports a cascade of errors from
    lines that are perfectly fine. Four em dashes in these comments cost a run.

    NOTE ON THE DENOMINATOR: coverage is measured over the five backend/src
    projects only. The Blazor admin portal is not covered - no test project
    references it and bUnit is not in the solution - so a figure quoted from
    this script means "backend", never "the product". The per-assembly table it
    prints exists so that is impossible to forget.

.PARAMETER Configuration
    Build configuration. CI uses Release; Debug is the faster local default.

.PARAMETER ResultsDirectory
    Where raw per-project cobertura files and the merged report are written.

.PARAMETER NoBuild
    Skip the build. Only safe when the solution was just built.

.PARAMETER MinimumLineRate
    Fail the run if the merged line rate falls below this percentage. Left unset
    the script only reports, which is what you want until a baseline is agreed.
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Debug',
    [string]$ResultsDirectory = 'TestResults',
    [switch]$NoBuild,
    [double]$MinimumLineRate = 0
)

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
Push-Location $repoRoot
try {
    $settings = Join-Path $repoRoot 'coverlet.runsettings'
    $rawDir = Join-Path $repoRoot $ResultsDirectory
    $mergedDir = Join-Path $rawDir 'merged'

    # Neither of these can be assumed to be on PATH. On this project's Windows
    # dev box dotnet lives outside PATH entirely, and global tools sit in a
    # profile directory that only some shells pick up.
    function Resolve-Tool {
        param([string]$Name, [string[]]$Fallbacks, [string]$InstallHint)

        $found = Get-Command $Name -ErrorAction SilentlyContinue
        if ($found) { return $found.Source }

        foreach ($path in $Fallbacks) {
            if (Test-Path $path) { return $path }
        }

        throw "$Name not found. $InstallHint"
    }

    $dotnet = Resolve-Tool -Name 'dotnet' -InstallHint 'Install the .NET 8 SDK.' -Fallbacks @(
        'C:\Program Files\dotnet\dotnet.exe',
        '/usr/bin/dotnet',
        '/usr/share/dotnet/dotnet'
    )

    # ReportGenerator is what turns two cobertura files into one figure. It is a
    # global tool rather than a package reference, so it can be missing.
    $reportGenerator = Resolve-Tool -Name 'reportgenerator' -Fallbacks @(
        (Join-Path $HOME '.dotnet/tools/reportgenerator.exe'),
        (Join-Path $HOME '.dotnet/tools/reportgenerator')
    ) -InstallHint 'Install it with: dotnet tool install -g dotnet-reportgenerator-globaltool'

    # A stale results directory is worse than no results: the merge globs every
    # cobertura file it finds, so yesterday's run would silently inflate today's
    # (it briefly did, reporting four files for a two-project run).
    #
    # Delete the CONTENTS, not the directory. Windows refuses to remove a
    # directory that any process has open, and "any process" includes a shell
    # sitting in it or an explorer window - neither of which is a reason to fail
    # a coverage run.
    if (Test-Path $rawDir) {
        Get-ChildItem -Path $rawDir -Force | Remove-Item -Recurse -Force
    }

    Write-Host "Running unit + integration tests with coverage ($Configuration)..." -ForegroundColor Cyan
    Write-Host "Integration tests start a PostgreSQL container per test class; this takes a while." -ForegroundColor DarkGray

    $testArgs = @(
        'test', 'SchoolErp.sln',
        '--configuration', $Configuration,
        '--collect:XPlat Code Coverage',
        '--settings', $settings,
        '--results-directory', $rawDir,
        # Kept from the previous CI step: trx is what makes a failing test
        # legible in the run summary rather than only in raw console output.
        '--logger', 'trx'
    )
    if ($NoBuild) { $testArgs += '--no-build' }

    & $dotnet @testArgs
    $testExitCode = $LASTEXITCODE

    # Collection failing is historically SILENT here: a malformed runsettings
    # made VSTest drop the collector, print one line among the test output and
    # exit successfully, leaving an empty results directory that read as "0.6%".
    # Never infer a number without checking the files exist first.
    # VSTest writes every attachment TWICE: once into the per-run GUID folder and
    # once into the trx staging area at <run>\In\<MACHINE>\. Both copies are
    # byte-identical, so a naive search reports four files for a two-project run.
    # ReportGenerator would merge the duplicates harmlessly, but a check nobody
    # can read at face value is not a check.
    $reports = @(Get-ChildItem -Path $rawDir -Recurse -Filter 'coverage.cobertura.xml' -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notmatch '\\In\\' })
    if ($reports.Count -eq 0) {
        throw "No coverage files were produced. The collector did not run - check the " +
              "test output above for 'Settings file provided does not conform to required format'."
    }
    if ($reports.Count -lt 2) {
        Write-Warning ("Only $($reports.Count) coverage file(s) found. Expected one per test project " +
                       "(unit and integration). The merged figure below is incomplete.")
    }
    foreach ($report in $reports) {
        if ($report.Length -eq 0) { throw "Coverage file is empty: $($report.FullName)" }
    }

    # Named individually rather than counted: a bare count cannot tell a healthy
    # two-project run from one that also swept up files another run left behind,
    # and this check exists precisely to be believed.
    Write-Host "Collected $($reports.Count) coverage file(s):" -ForegroundColor DarkGray
    foreach ($report in $reports) {
        Write-Host ("  {0}  ({1:N0} KB)" -f $report.FullName.Replace("$rawDir\", ''), ($report.Length / 1KB)) -ForegroundColor DarkGray
    }

    Write-Host 'Merging into a single report...' -ForegroundColor Cyan
    & $reportGenerator `
        "-reports:$rawDir/**/coverage.cobertura.xml" `
        "-targetdir:$mergedDir" `
        '-reporttypes:Cobertura;TextSummary;Html' `
        '-verbosity:Warning'
    if ($LASTEXITCODE -ne 0) { throw "reportgenerator failed with exit code $LASTEXITCODE." }

    [xml]$merged = Get-Content (Join-Path $mergedDir 'Cobertura.xml')
    $linesCovered = [int]$merged.coverage.'lines-covered'
    $linesValid = [int]$merged.coverage.'lines-valid'
    $lineRate = if ($linesValid -gt 0) { 100.0 * $linesCovered / $linesValid } else { 0.0 }
    $branchRate = 100.0 * [double]$merged.coverage.'branch-rate'

    Write-Host ''
    Write-Host 'Backend coverage (unit + integration merged)' -ForegroundColor Green
    Write-Host ('  lines   {0}/{1} = {2:N1}%' -f $linesCovered, $linesValid, $lineRate)
    Write-Host ('  branches{0,-8}{1:N1}%' -f '', $branchRate)
    Write-Host ''
    Write-Host 'Per assembly:'
    foreach ($package in $merged.coverage.packages.package) {
        $covered = ($package.classes.class.lines.line | Where-Object { [int]$_.hits -gt 0 }).Count
        $total = ($package.classes.class.lines.line).Count
        Write-Host ('  {0,-28} {1,6:N1}%   ({2}/{3} lines)' -f `
            $package.name, (100.0 * [double]$package.'line-rate'), $covered, $total)
    }
    Write-Host ''
    Write-Host "HTML report: $(Join-Path $mergedDir 'index.html')" -ForegroundColor DarkGray

    # GitHub renders this on the workflow run page, so the number is visible
    # without downloading an artifact.
    if ($env:GITHUB_STEP_SUMMARY) {
        $summary = @(
            '## Backend test coverage',
            '',
            ('**Lines: {0:N1}%** ({1}/{2}) &nbsp;&nbsp; Branches: {3:N1}%' -f $lineRate, $linesCovered, $linesValid, $branchRate),
            '',
            'Merged from the unit and integration runs. Backend projects only - the',
            'Blazor portal has no test infrastructure and is not in the denominator.',
            '',
            '| Assembly | Line rate |',
            '| --- | ---: |'
        )
        foreach ($package in $merged.coverage.packages.package) {
            $summary += ('| {0} | {1:N1}% |' -f $package.name, (100.0 * [double]$package.'line-rate'))
        }
        # Written through .NET rather than Out-File because the two PowerShell
        # versions disagree: 5.1's "utf8" means WITH a byte-order mark, 7's means
        # without. A BOM landing in front of the leading "##" can stop GitHub
        # rendering it as a heading, and this file is only ever exercised in CI -
        # where nobody would connect the broken heading to the local shell.
        [System.IO.File]::AppendAllText(
            $env:GITHUB_STEP_SUMMARY,
            ($summary -join "`n") + "`n",
            (New-Object System.Text.UTF8Encoding $false))
    }

    if ($MinimumLineRate -gt 0 -and $lineRate -lt $MinimumLineRate) {
        throw ('Coverage {0:N1}% is below the required {1:N1}%.' -f $lineRate, $MinimumLineRate)
    }

    # A failing test must still fail the run, even though the report was produced.
    if ($testExitCode -ne 0) {
        throw "Tests failed (exit code $testExitCode). The coverage figure above is from a red suite."
    }
}
finally {
    Pop-Location
}
