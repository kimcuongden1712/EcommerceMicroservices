# Prepares local Garage secrets only. Does not start Docker or provision buckets.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$garageDirectory = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))

$environmentFile = Join-Path $garageDirectory '.env.garage'
if (Test-Path -LiteralPath $environmentFile) {
    Write-Host "Garage secrets already exist; keeping the existing file unchanged."
    return
}
$null = New-Item -ItemType File -Path $environmentFile
$identity = (whoami.exe).Trim()
$null = & icacls.exe $environmentFile /inheritance:r /grant:r "${identity}:(F)"
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $environmentFile -Force
    throw "Could not restrict the Garage secret file to the current Windows user."
}
$aclOutput = & icacls.exe $environmentFile
if ($LASTEXITCODE -ne 0) {
    Remove-Item -LiteralPath $environmentFile -Force
    throw "Could not verify the Garage secret file permissions."
}
$permissionLines = @($aclOutput | Where-Object { [string]$_ -match ':\([A-Z,]+\)' })
$expectedGrant = [regex]::Escape($identity) + ':\(F\)'
if ($permissionLines.Count -ne 1 -or [string]$permissionLines[0] -notmatch $expectedGrant) {
    Remove-Item -LiteralPath $environmentFile -Force
    throw "The Garage secret file ACL is broader than the current Windows user."
}
$random = [System.Security.Cryptography.RandomNumberGenerator]::Create()
try {
    $entries = @()
    foreach ($name in @("GARAGE_RPC_SECRET", "GARAGE_ADMIN_TOKEN", "GARAGE_METRICS_TOKEN")) {
        $bytes = New-Object byte[] 32
        $random.GetBytes($bytes)
        $value = [BitConverter]::ToString($bytes).Replace("-", "").ToLowerInvariant()
        $entries += "$name=$value"
    }
    [System.IO.File]::WriteAllLines($environmentFile, $entries, [System.Text.Encoding]::ASCII)
}
finally {
    $random.Dispose()
}
Write-Host 'Garage local secrets initialized.'
