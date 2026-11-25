$ErrorActionPreference = 'Stop'

$garageContainer = 'ecommerce_garage'
$expectedImage = 'dxflrs/garage:v2.5.0'
$productBuckets = @('product-media', 'product-staging')
$garageDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$secretFile = Join-Path $garageDirectory '.env.product-api-local'
$keyName = 'product-api-local'
$script:checks = New-Object System.Collections.Generic.List[object]

function Add-Check {
    param(
        [string]$Check,
        [string]$Expected,
        [string]$Actual,
        [bool]$Passed
    )

    $script:checks.Add([pscustomobject]@{
        Check    = $Check
        Expected = $Expected
        Actual   = $Actual
        Result   = if ($Passed) { 'PASS' } else { 'FAIL' }
    })
}

function Write-CheckResults {
    $script:checks | Format-Table -Property Check, Expected, Actual, Result -AutoSize | Out-String | Write-Output
    if (@($script:checks | Where-Object { $_.Result -eq 'FAIL' }).Count -gt 0) {
        exit 1
    }
}

function Invoke-GarageCli {
    param([Parameter(Mandatory = $true)][string[]]$Arguments)

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $null = & docker exec $garageContainer /garage @Arguments 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "Garage CLI operation failed: /garage $($Arguments -join ' ')"
    }
}

function Invoke-GarageApi {
    param(
        [Parameter(Mandatory = $true)][string]$Operation,
        [object]$Body
    )

    $dockerArguments = @('exec', $garageContainer, '/garage', 'json-api', $Operation)
    if ($null -ne $Body) {
        $jsonArgument = ConvertTo-Json -InputObject $Body -Compress -Depth 10
        $dockerArguments += $jsonArgument.Replace('"', '\"')
    }

    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $output = & docker @dockerArguments 2>$null
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($exitCode -ne 0) {
        throw "Garage API operation failed: $Operation"
    }

    $json = [string]::Join([Environment]::NewLine, [string[]]$output)
    if ([string]::IsNullOrWhiteSpace($json)) {
        throw "Garage API operation returned no data: $Operation"
    }

    try {
        return (ConvertFrom-Json -InputObject $json)
    }
    catch {
        throw "Garage API operation returned invalid JSON: $Operation"
    }
}

function Set-PrivateFileAcl {
    param([Parameter(Mandatory = $true)][string]$Path)

    $identity = (whoami.exe).Trim()
    $previousErrorActionPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try {
        $null = & icacls.exe $Path /inheritance:r /grant:r "${identity}:(F)" 2>$null
        $aclExitCode = $LASTEXITCODE
        $aclOutput = & icacls.exe $Path 2>$null
        $verifyExitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    if ($aclExitCode -ne 0 -or $verifyExitCode -ne 0) {
        throw "Could not set or verify the local credentials file ACL: $Path"
    }

    $permissionLines = @($aclOutput | Where-Object { [string]$_ -match ':\([A-Z,]+\)' })
    $expectedGrant = [regex]::Escape($identity) + ':\(F\)'
    if ($permissionLines.Count -ne 1 -or [string]$permissionLines[0] -notmatch $expectedGrant) {
        throw "The local credentials file ACL is broader than the current Windows user: $Path"
    }
}

function Read-LocalEnvironmentFile {
    param([Parameter(Mandatory = $true)][string]$Path)

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

function ConvertTo-CurlConfigValue {
    param([Parameter(Mandatory = $true)][string]$Value)

    $escaped = $Value.Replace('\', '\\').Replace('"', '\"')
    $escaped = $escaped.Replace("`r", '\r').Replace("`n", '\n')
    return '"' + $escaped + '"'
}

function Invoke-S3Request {
    param(
        [Parameter(Mandatory = $true)][string]$Bucket,
        [Parameter(Mandatory = $true)][string]$Object,
        [Parameter(Mandatory = $true)][ValidateSet('GET', 'PUT', 'DELETE')][string]$Method,
        [bool]$Anonymous = $false,
        [string]$AccessKeyId,
        [string]$SecretAccessKey
    )

    $curlConfigPath = Join-Path $env:TEMP ('garage-s3-check-' + [guid]::NewGuid().ToString('N') + '.cfg')
    $null = New-Item -ItemType File -Path $curlConfigPath
    try {
        Set-PrivateFileAcl -Path $curlConfigPath
        $configLines = @(
            'url = ' + (ConvertTo-CurlConfigValue -Value "http://localhost:3900/$Bucket/$Object")
            'request = ' + (ConvertTo-CurlConfigValue -Value $Method)
            'output = "NUL"'
            'write-out = "%{http_code}"'
            'silent'
            'show-error'
        )
        if (-not $Anonymous) {
            $configLines += 'aws-sigv4 = "aws:amz:garage:s3"'
            $configLines += 'user = ' + (ConvertTo-CurlConfigValue -Value "$AccessKeyId`:$SecretAccessKey")
        }
        if ($Method -eq 'PUT') {
            $configLines += 'data = "phase2-verification-probe"'
        }

        [System.IO.File]::WriteAllLines($curlConfigPath, $configLines, [System.Text.Encoding]::ASCII)
        $previousErrorActionPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        try {
            $output = & curl.exe --config $curlConfigPath 2>$null
            $curlExitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }
        $statusCode = [string]::Join('', [string[]]$output).Trim()
        if ($curlExitCode -ne 0) {
            return "CURL_ERROR_$curlExitCode"
        }

        return $statusCode
    }
    finally {
        if (Test-Path -LiteralPath $curlConfigPath) {
            Remove-Item -LiteralPath $curlConfigPath -Force
        }
    }
}

if ($env:OS -ne 'Windows_NT') {
    throw 'This local verification script is designed for Windows and Windows file ACLs.'
}

$null = & docker info --format '{{.ServerVersion}}' 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is unavailable. Start Docker Desktop and retry.'
}

$containerImage = & docker inspect --format '{{.Config.Image}}' $garageContainer 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Garage container '$garageContainer' does not exist."
}
$containerState = & docker inspect --format '{{.State.Status}}' $garageContainer 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Could not inspect Garage container '$garageContainer'."
}

$imageValue = [string]($containerImage | Select-Object -First 1)
$stateValue = [string]($containerState | Select-Object -First 1)
Add-Check -Check 'Garage container' -Expected 'Running; dxflrs/garage:v2.5.0' `
    -Actual "$stateValue; $imageValue" `
    -Passed ($stateValue -eq 'running' -and $imageValue -eq $expectedImage)

if ($stateValue -ne 'running' -or $imageValue -ne $expectedImage) {
    Write-CheckResults
    exit 1
}

$health = Invoke-GarageApi -Operation 'GetClusterHealth'
Add-Check -Check 'Garage health' -Expected 'healthy' -Actual ([string]$health.status) `
    -Passed ($health.status -eq 'healthy')
if ($health.status -ne 'healthy') {
    Write-CheckResults
    exit 1
}

$buckets = @(Invoke-GarageApi -Operation 'ListBuckets')
foreach ($bucketName in $productBuckets) {
    $bucketExists = @($buckets | Where-Object {
        @($_.globalAliases) -contains $bucketName
    }).Count -gt 0
    Add-Check -Check $bucketName -Expected 'Exists' `
        -Actual $(if ($bucketExists) { 'Exists' } else { 'Missing' }) -Passed $bucketExists

    if ($bucketExists) {
        $bucketInfo = Invoke-GarageApi -Operation 'GetBucketInfo' -Body @{ globalAlias = $bucketName }
        $isPrivate = -not [bool]$bucketInfo.websiteAccess
        Add-Check -Check "$bucketName website access" -Expected 'Disabled/private' `
            -Actual ([string]$bucketInfo.websiteAccess) -Passed $isPrivate
    }
}

$keys = @(Invoke-GarageApi -Operation 'ListKeys')
$matchingKeys = @($keys | Where-Object { $_.name -ceq $keyName })
Add-Check -Check 'Product key' -Expected 'Exactly one product-api-local key' `
    -Actual "$($matchingKeys.Count) matching keys" -Passed ($matchingKeys.Count -eq 1)
if ($matchingKeys.Count -ne 1) {
    Write-CheckResults
    exit 1
}
if ($matchingKeys[0].expired) {
    Add-Check -Check 'Product key expiry' -Expected 'Not expired' -Actual 'Expired' -Passed $false
    Write-CheckResults
    exit 1
}

$keyInfo = Invoke-GarageApi -Operation 'GetKeyInfo' -Body @{
    id = [string]$matchingKeys[0].id
    showSecretKey = $true
}
$createBucketAllowed = [bool]$keyInfo.permissions.createBucket
Add-Check -Check 'Global bucket creation' -Expected 'Denied' `
    -Actual $(if ($createBucketAllowed) { 'Allowed' } else { 'Denied' }) `
    -Passed (-not $createBucketAllowed)

$unexpectedAccess = @($keyInfo.buckets | Where-Object {
    $aliases = @($_.globalAliases) + @($_.localAliases)
    $hasTarget = @($aliases | Where-Object { $productBuckets -contains $_ }).Count -gt 0
    $hasPermission = $_.permissions.read -or $_.permissions.write -or $_.permissions.owner
    $hasPermission -and -not $hasTarget
})
Add-Check -Check 'Other bucket access' -Expected 'No grants outside Product buckets' `
    -Actual $(if ($unexpectedAccess.Count -eq 0) { 'No other grants' } else { "$($unexpectedAccess.Count) unexpected grants" }) `
    -Passed ($unexpectedAccess.Count -eq 0)

foreach ($bucketName in $productBuckets) {
    $permission = @($keyInfo.buckets | Where-Object {
        @($_.globalAliases) -contains $bucketName
    }) | Select-Object -First 1
    $readWrite = $null -ne $permission -and
        [bool]$permission.permissions.read -and
        [bool]$permission.permissions.write
    $ownerDenied = $null -ne $permission -and -not [bool]$permission.permissions.owner

    Add-Check -Check "$bucketName key access" -Expected 'Read + Write' `
        -Actual $(if ($readWrite) { 'Read + Write' } else { 'Not Read + Write' }) -Passed $readWrite
    Add-Check -Check "$bucketName owner permission" -Expected 'Denied' `
        -Actual $(if ($ownerDenied) { 'Denied' } else { 'Allowed/unknown' }) -Passed $ownerDenied
}

if (-not (Test-Path -LiteralPath $secretFile)) {
    Add-Check -Check 'Local secret file' -Expected 'Present; ignored; current-user ACL' `
        -Actual 'Missing' -Passed $false
    Write-CheckResults
    exit 1
}

Set-PrivateFileAcl -Path $secretFile
$storedValues = Read-LocalEnvironmentFile -Path $secretFile
$secretMatches = $storedValues['GARAGE_S3_ACCESS_KEY_ID'] -ceq [string]$keyInfo.accessKeyId -and
    $storedValues['GARAGE_S3_SECRET_ACCESS_KEY'] -ceq [string]$keyInfo.secretAccessKey
Add-Check -Check 'Local secret file' -Expected 'Present; ignored; current-user ACL' `
    -Actual $(if ($secretMatches) { 'Present; ACL restricted; matches Garage' } else { 'Missing fields or mismatch' }) `
    -Passed $secretMatches

$curlVersion = & curl.exe -V 2>$null
if ($LASTEXITCODE -ne 0 -or -not ($curlVersion -match 'libcurl/(\d+\.){2,3}\d+')) {
    throw 'curl.exe is unavailable; S3 permission checks cannot run.'
}

foreach ($bucketName in $productBuckets) {
    $objectName = 'phase2-verify-' + [guid]::NewGuid().ToString('N')
    $putStatus = Invoke-S3Request -Bucket $bucketName -Object $objectName -Method 'PUT' `
        -AccessKeyId ([string]$keyInfo.accessKeyId) -SecretAccessKey ([string]$keyInfo.secretAccessKey)
    $putPassed = $putStatus -eq '200'
    Add-Check -Check "$bucketName signed PUT" -Expected '200' -Actual $putStatus -Passed $putPassed

    if ($putPassed) {
        $getStatus = Invoke-S3Request -Bucket $bucketName -Object $objectName -Method 'GET' `
            -AccessKeyId ([string]$keyInfo.accessKeyId) -SecretAccessKey ([string]$keyInfo.secretAccessKey)
        Add-Check -Check "$bucketName signed GET" -Expected '200' -Actual $getStatus -Passed ($getStatus -eq '200')

        $deleteStatus = Invoke-S3Request -Bucket $bucketName -Object $objectName -Method 'DELETE' `
            -AccessKeyId ([string]$keyInfo.accessKeyId) -SecretAccessKey ([string]$keyInfo.secretAccessKey)
        $deletePassed = $deleteStatus -in @('200', '204')
        Add-Check -Check "$bucketName probe cleanup" -Expected '200 or 204' `
            -Actual $deleteStatus -Passed $deletePassed
    }
    else {
        Add-Check -Check "$bucketName signed GET" -Expected '200' -Actual 'Skipped: PUT failed' -Passed $false
        Add-Check -Check "$bucketName probe cleanup" -Expected 'No object left' -Actual 'No object written' -Passed $true
    }
}

$anonymousObject = 'phase2-anonymous-' + [guid]::NewGuid().ToString('N')
$anonymousGet = Invoke-S3Request -Bucket 'product-media' -Object $anonymousObject -Method 'GET' -Anonymous $true
Add-Check -Check 'Anonymous read' -Expected '403' -Actual $anonymousGet -Passed ($anonymousGet -eq '403')

$anonymousPut = Invoke-S3Request -Bucket 'product-media' -Object $anonymousObject -Method 'PUT' -Anonymous $true
$anonymousPutDenied = $anonymousPut -eq '403'
Add-Check -Check 'Anonymous write' -Expected '403' -Actual $anonymousPut -Passed $anonymousPutDenied
if (-not $anonymousPutDenied -and $anonymousPut -eq '200') {
    $cleanupStatus = Invoke-S3Request -Bucket 'product-media' -Object $anonymousObject -Method 'DELETE' `
        -AccessKeyId ([string]$keyInfo.accessKeyId) -SecretAccessKey ([string]$keyInfo.secretAccessKey)
    Add-Check -Check 'Anonymous probe cleanup' -Expected '200 or 204' -Actual $cleanupStatus `
        -Passed ($cleanupStatus -in @('200', '204'))
}

$probeBucket = 'phase2-isolation-' + [guid]::NewGuid().ToString('N')
$probeObject = 'product-key-isolation-probe'
$probeCreated = $false
$probeObjectWritten = $false
$probeObjectDeleted = $false
try {
    Invoke-GarageCli -Arguments @('bucket', 'create', $probeBucket)
    $probeCreated = $true

    $probeStatus = Invoke-S3Request -Bucket $probeBucket -Object $probeObject -Method 'PUT' `
        -AccessKeyId ([string]$keyInfo.accessKeyId) -SecretAccessKey ([string]$keyInfo.secretAccessKey)
    $probeObjectWritten = $probeStatus -eq '200'
    Add-Check -Check 'Other bucket S3 access' -Expected '403' -Actual $probeStatus `
        -Passed ($probeStatus -eq '403')

    if ($probeObjectWritten) {
        $deleteStatus = Invoke-S3Request -Bucket $probeBucket -Object $probeObject -Method 'DELETE' `
            -AccessKeyId ([string]$keyInfo.accessKeyId) -SecretAccessKey ([string]$keyInfo.secretAccessKey)
        $probeObjectDeleted = $deleteStatus -in @('200', '204')
        Add-Check -Check 'Isolation probe object cleanup' -Expected '200 or 204' `
            -Actual $deleteStatus -Passed $probeObjectDeleted
    }
}
finally {
    if ($probeCreated -and (-not $probeObjectWritten -or $probeObjectDeleted)) {
        Invoke-GarageCli -Arguments @('bucket', 'delete', $probeBucket, '--yes')
        Add-Check -Check 'Isolation probe bucket cleanup' -Expected 'Deleted' `
            -Actual 'Deleted' -Passed $true
    }
    elseif ($probeCreated) {
        Add-Check -Check 'Isolation probe bucket cleanup' -Expected 'Deleted' `
            -Actual 'Not deleted: object cleanup failed; inspect before removal' -Passed $false
    }
}

Write-CheckResults
