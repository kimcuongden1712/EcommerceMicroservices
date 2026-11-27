[CmdletBinding()]
param(
    [ValidateRange(0, 100)]
    [double]$MinimumLineCoverage = 100,
    [ValidateRange(0, 100)]
    [double]$MinimumBranchCoverage = 100
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'Product.API.Tests.csproj'
$results = Join-Path $PSScriptRoot 'TestResults/coverage'
$reportPath = Join-Path $results 'coverage.cobertura.xml'
[System.IO.Directory]::CreateDirectory($results) | Out-Null

& dotnet test --project $project --coverage --coverage-output-format cobertura `
    --coverage-output coverage.cobertura.xml --results-directory $results --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    throw "Product API tests failed (exit code $LASTEXITCODE). Coverage gate was not evaluated."
}

[xml]$report = Get-Content -LiteralPath $reportPath -Raw
$packages = @($report.coverage.packages.package | Where-Object { $_.name -eq 'Product.API' })
if ($packages.Count -ne 1) {
    throw 'Expected exactly one Product.API module in the coverage report.'
}
$module = $packages[0]
$culture = [System.Globalization.CultureInfo]::InvariantCulture
$lineCoverage = [double]::Parse($module.'line-rate', $culture) * 100
$branchCoverage = [double]::Parse($module.'branch-rate', $culture) * 100

# Combine duplicate source-line entries from async state machines and compiler-generated lambdas.
$files = @{}
foreach ($class in $module.classes.class) {
    $source = [string]$class.filename
    if (-not $files.ContainsKey($source)) { $files[$source] = @{} }
    foreach ($line in $class.lines.line) {
        $number = [int]$line.number
        $hits = [long]$line.hits
        if (-not $files[$source].ContainsKey($number) -or $hits -gt $files[$source][$number]) {
            $files[$source][$number] = $hits
        }
    }
}
$uncovered = @(
    foreach ($source in ($files.Keys | Sort-Object)) {
        $missing = @($files[$source].Keys | Where-Object { $files[$source][$_] -eq 0 } | Sort-Object)
        if ($missing.Count -gt 0) {
            [pscustomobject]@{ File = $source; MissingLines = $missing }
        }
    }
)
$passed = $lineCoverage -ge $MinimumLineCoverage -and $branchCoverage -ge $MinimumBranchCoverage
[pscustomobject]@{
    Module = 'Product.API'
    LineCoverage = $lineCoverage
    BranchCoverage = $branchCoverage
    MinimumLineCoverage = $MinimumLineCoverage
    MinimumBranchCoverage = $MinimumBranchCoverage
    Passed = $passed
    ExcludedProductSourceFiles = @()
    UncoveredFiles = $uncovered
} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $results 'coverage-summary.json') -Encoding UTF8

Write-Host ('Product.API coverage: lines {0:F2}%, branches {1:F2}%' -f $lineCoverage, $branchCoverage)
Write-Host "Report: $reportPath"
if (-not $passed) {
    throw ('Coverage gate failed: required lines {0}%, branches {1}%.' -f $MinimumLineCoverage, $MinimumBranchCoverage)
}
