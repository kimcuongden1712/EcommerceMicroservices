[CmdletBinding()]
param(
    [string]$AllowedOrigins = $env:PRODUCT_STAGING_CORS_ALLOWED_ORIGINS,
    [string]$GarageEndpoint = $env:GARAGE_S3_PUBLIC_ENDPOINT,
    [string]$Region = $env:GARAGE_S3_REGION,
    [string]$Bucket = 'product-staging'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

if ([string]::IsNullOrWhiteSpace($GarageEndpoint)) {
    $GarageEndpoint = 'http://localhost:3900'
}
if ([string]::IsNullOrWhiteSpace($Region)) {
    $Region = 'garage'
}
if ([string]::IsNullOrWhiteSpace($AllowedOrigins)) {
    throw 'PRODUCT_STAGING_CORS_ALLOWED_ORIGINS must contain one or more exact MFE origins.'
}
$garageUri = $null
if (-not [Uri]::TryCreate($GarageEndpoint, [UriKind]::Absolute, [ref]$garageUri) -or
    $garageUri.Scheme -notin @('http', 'https') -or
    [string]::IsNullOrWhiteSpace($garageUri.Host) -or
    $GarageEndpoint.IndexOf('*') -ge 0) {
    throw "Garage S3 endpoint '$GarageEndpoint' is invalid."
}

function Read-LocalEnvironmentFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        throw "Garage Product API credentials were not found: $Path. Run provision-local.ps1 first."
    }

    $values = @{}
    foreach ($line in [System.IO.File]::ReadAllLines($Path)) {
        if ([string]::IsNullOrWhiteSpace($line) -or $line.StartsWith('#')) {
            continue
        }

        $separator = $line.IndexOf('=')
        if ($separator -le 0) {
            throw "Invalid entry in local credentials file: $Path"
        }

        $values[$line.Substring(0, $separator)] = $line.Substring($separator + 1)
    }

    return $values
}

function ConvertTo-ExactOriginList {
    param([Parameter(Mandatory = $true)][string]$Value)

    $origins = [System.Collections.Generic.List[string]]::new()
    foreach ($candidateValue in $Value.Split(',', [StringSplitOptions]::None)) {
        $candidate = $candidateValue.Trim()
        $uri = $null
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            throw 'The CORS origin list contains an empty value.'
        }
        if ($candidate.IndexOf('*') -ge 0 -or
            -not [Uri]::TryCreate($candidate, [UriKind]::Absolute, [ref]$uri) -or
            $uri.Scheme -notin @('http', 'https') -or
            [string]::IsNullOrWhiteSpace($uri.Host) -or
            -not [string]::IsNullOrEmpty($uri.UserInfo) -or
            -not [string]::IsNullOrEmpty($uri.Query) -or
            -not [string]::IsNullOrEmpty($uri.Fragment) -or
            $uri.AbsolutePath -notin @('', '/')) {
            throw "CORS origin '$candidate' is invalid. Use an exact http(s) origin without a path or wildcard."
        }

        $normalized = $uri.GetLeftPart([UriPartial]::Authority).TrimEnd('/')
        $alreadyAdded = @($origins | Where-Object {
            [string]::Equals([string]$_, $normalized, [StringComparison]::OrdinalIgnoreCase)
        }).Count -gt 0
        if (-not $alreadyAdded) {
            $null = $origins.Add($normalized)
        }
    }

    return @($origins)
}

function Invoke-AwsCli {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $command = Get-Command aws.exe -ErrorAction SilentlyContinue
    if ($null -eq $command) {
        throw 'AWS CLI (aws.exe) is required to apply Garage bucket CORS. Install it and retry.'
    }

    $output = & $command.Source @Arguments 2>&1
    if ($LASTEXITCODE -ne 0) {
        throw "AWS CLI failed for s3api $($Arguments[1]): $([string]::Join(' ', [string[]]$output))"
    }

    return [string]::Join([Environment]::NewLine, [string[]]$output)
}

$garageDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$credentialFile = Join-Path $garageDirectory '.env.product-api-local'
$credentials = Read-LocalEnvironmentFile -Path $credentialFile
foreach ($name in @('GARAGE_S3_ACCESS_KEY_ID', 'GARAGE_S3_SECRET_ACCESS_KEY')) {
    if ([string]::IsNullOrWhiteSpace($credentials[$name])) {
        throw "The local credentials file does not contain $name. Run provision-local.ps1 first."
    }
}

$origins = @(ConvertTo-ExactOriginList -Value $AllowedOrigins)
$corsDocument = [ordered]@{
    CORSRules = @([ordered]@{
        AllowedOrigins = $origins
        AllowedMethods = @('PUT')
        AllowedHeaders = @('Content-Type')
    })
}
$corsFile = Join-Path $env:TEMP ('garage-cors-' + [guid]::NewGuid().ToString('N') + '.json')
[System.IO.File]::WriteAllText(
    $corsFile,
    ($corsDocument | ConvertTo-Json -Depth 5),
    [System.Text.Encoding]::UTF8)

$previousAccessKey = $env:AWS_ACCESS_KEY_ID
$previousSecretKey = $env:AWS_SECRET_ACCESS_KEY
$previousRegion = $env:AWS_DEFAULT_REGION
try {
    $env:AWS_ACCESS_KEY_ID = $credentials['GARAGE_S3_ACCESS_KEY_ID']
    $env:AWS_SECRET_ACCESS_KEY = $credentials['GARAGE_S3_SECRET_ACCESS_KEY']
    $env:AWS_DEFAULT_REGION = $Region

    $commonArguments = @('--endpoint-url', $GarageEndpoint, '--region', $Region)
    Invoke-AwsCli -Arguments (@(
        's3api', 'put-bucket-cors', '--bucket', $Bucket,
        '--cors-configuration', "file://$corsFile"
    ) + $commonArguments) | Out-Null

    $actualJson = Invoke-AwsCli -Arguments (@(
        's3api', 'get-bucket-cors', '--bucket', $Bucket,
        '--output', 'json'
    ) + $commonArguments)
    $actual = $actualJson | ConvertFrom-Json
    $actualRule = @($actual.CORSRules) | Select-Object -First 1
    if ($null -eq $actualRule) {
        throw "Garage returned no CORS rule for bucket '$Bucket'."
    }

    $actualOrigins = @($actualRule.AllowedOrigins | ForEach-Object { [string]$_ } | Sort-Object)
    $expectedOrigins = @($origins | Sort-Object)
    if (($actualOrigins -join "`n") -cne ($expectedOrigins -join "`n") -or
        (@($actualRule.AllowedMethods) -join ',') -cne 'PUT' -or
        (@($actualRule.AllowedHeaders) -join ',') -cne 'Content-Type') {
        throw "Garage returned a CORS rule that does not match the requested Product staging policy."
    }
}
finally {
    if ($null -eq $previousAccessKey) { Remove-Item Env:AWS_ACCESS_KEY_ID -ErrorAction SilentlyContinue } else { $env:AWS_ACCESS_KEY_ID = $previousAccessKey }
    if ($null -eq $previousSecretKey) { Remove-Item Env:AWS_SECRET_ACCESS_KEY -ErrorAction SilentlyContinue } else { $env:AWS_SECRET_ACCESS_KEY = $previousSecretKey }
    if ($null -eq $previousRegion) { Remove-Item Env:AWS_DEFAULT_REGION -ErrorAction SilentlyContinue } else { $env:AWS_DEFAULT_REGION = $previousRegion }
    if (Test-Path -LiteralPath $corsFile) { Remove-Item -LiteralPath $corsFile -Force }
}

Write-Output "Garage CORS applied and verified for '$Bucket': origins=$($origins -join ', '), method=PUT, request-header=Content-Type."
