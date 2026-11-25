$ErrorActionPreference = 'Stop'

$garageContainer = 'ecommerce_garage'
$expectedImage = 'dxflrs/garage:v2.5.0'
$productBuckets = @('product-media', 'product-staging')
$garageDirectory = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$secretFile = Join-Path $garageDirectory '.env.product-api-local'
$secretDirectory = Split-Path -Parent $secretFile
$keyName = 'product-api-local'

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

function Save-LocalEnvironmentFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$AccessKeyId,
        [Parameter(Mandatory = $true)][string]$SecretAccessKey
    )

    foreach ($value in @($AccessKeyId, $SecretAccessKey)) {
        if ($value.Contains("`r") -or $value.Contains("`n")) {
            throw 'Garage returned a credential that cannot be stored safely in the local environment file.'
        }
    }

    $entries = @(
        'GARAGE_S3_ENDPOINT=http://garage:3900'
        'GARAGE_S3_PUBLIC_ENDPOINT=http://localhost:3900'
        'GARAGE_S3_REGION=garage'
        'GARAGE_PRODUCT_MEDIA_BUCKET=product-media'
        'GARAGE_PRODUCT_STAGING_BUCKET=product-staging'
        "GARAGE_S3_ACCESS_KEY_ID=$AccessKeyId"
        "GARAGE_S3_SECRET_ACCESS_KEY=$SecretAccessKey"
    )

    $temporaryFile = Join-Path $secretDirectory ('.env.product-api-local-' + [guid]::NewGuid().ToString('N') + '.tmp')
    $null = New-Item -ItemType File -Path $temporaryFile
    try {
        Set-PrivateFileAcl -Path $temporaryFile
        [System.IO.File]::WriteAllLines($temporaryFile, $entries, [System.Text.Encoding]::ASCII)
        [System.IO.File]::Move($temporaryFile, $Path)
        Set-PrivateFileAcl -Path $Path
    }
    finally {
        if (Test-Path -LiteralPath $temporaryFile) {
            Remove-Item -LiteralPath $temporaryFile -Force
        }
    }
}

if ($env:OS -ne 'Windows_NT') {
    throw 'This local provisioning script is designed for Windows and Windows file ACLs.'
}

$null = & docker info --format '{{.ServerVersion}}' 2>$null
if ($LASTEXITCODE -ne 0) {
    throw 'Docker is unavailable. Start Docker Desktop and retry.'
}

$containerImage = & docker inspect --format '{{.Config.Image}}' $garageContainer 2>$null
if ($LASTEXITCODE -ne 0) {
    throw "Garage container '$garageContainer' does not exist."
}
if (($containerImage | Select-Object -First 1) -ne $expectedImage) {
    throw "Expected Garage image '$expectedImage'; found '$($containerImage | Select-Object -First 1)'. Re-verify CLI behavior before proceeding."
}

$containerState = & docker inspect --format '{{.State.Status}}' $garageContainer 2>$null
if ($LASTEXITCODE -ne 0 -or ($containerState | Select-Object -First 1) -ne 'running') {
    throw "Garage container '$garageContainer' is not running."
}

$health = Invoke-GarageApi -Operation 'GetClusterHealth'
if ($health.status -ne 'healthy') {
    throw "Garage is not healthy (status: $($health.status))."
}

$buckets = @(Invoke-GarageApi -Operation 'ListBuckets')
$keys = @(Invoke-GarageApi -Operation 'ListKeys')

foreach ($bucketName in $productBuckets) {
    $bucketExists = @($buckets | Where-Object {
        @($_.globalAliases) -contains $bucketName
    }).Count -gt 0

    if (-not $bucketExists) {
        Invoke-GarageCli -Arguments @('bucket', 'create', $bucketName)
    }

    $bucketInfo = Invoke-GarageApi -Operation 'GetBucketInfo' -Body @{ globalAlias = $bucketName }
    if ($bucketInfo.websiteAccess) {
        throw "Bucket '$bucketName' has website access enabled. Review it manually; provisioning will not change existing access settings."
    }
}

$matchingKeys = @($keys | Where-Object { $_.name -ceq $keyName })
if ($matchingKeys.Count -gt 1) {
    throw "Multiple Garage keys are named '$keyName'; resolve the duplicate names manually."
}

if ($matchingKeys.Count -eq 0) {
    Invoke-GarageCli -Arguments @('key', 'create', $keyName)
    $keys = @(Invoke-GarageApi -Operation 'ListKeys')
    $matchingKeys = @($keys | Where-Object { $_.name -ceq $keyName })
}

if ($matchingKeys.Count -ne 1) {
    throw "Could not find exactly one Garage key named '$keyName' after provisioning."
}
if ($matchingKeys[0].expired) {
    throw "Garage key '$keyName' is expired; it will not be rotated automatically."
}

$keyId = [string]$matchingKeys[0].id
$keyInfo = Invoke-GarageApi -Operation 'GetKeyInfo' -Body @{
    id = $keyId
    showSecretKey = $true
}

if ([string]::IsNullOrWhiteSpace([string]$keyInfo.secretAccessKey)) {
    throw "Garage did not return the existing secret for '$keyName'. No key was rotated; resolve secret storage manually."
}
if ($keyInfo.permissions.createBucket) {
    throw "Garage key '$keyName' can create buckets globally. Provisioning will not alter this broader permission."
}

foreach ($bucketPermission in @($keyInfo.buckets)) {
    $aliases = @($bucketPermission.globalAliases) + @($bucketPermission.localAliases)
    $hasTargetAlias = @($aliases | Where-Object { $productBuckets -contains $_ }).Count -gt 0
    $hasAnyPermission = $bucketPermission.permissions.read -or
        $bucketPermission.permissions.write -or
        $bucketPermission.permissions.owner

    if ($hasAnyPermission -and -not $hasTargetAlias) {
        throw "Garage key '$keyName' has permissions on a non-Product bucket. Review the broader access manually."
    }
    if ($hasTargetAlias -and $bucketPermission.permissions.owner) {
        throw "Garage key '$keyName' has bucket-owner permission. Provisioning will not remove it automatically."
    }
}

foreach ($bucketName in $productBuckets) {
    $permission = @($keyInfo.buckets | Where-Object {
        @($_.globalAliases) -contains $bucketName
    }) | Select-Object -First 1

    if ($null -eq $permission -or
        -not $permission.permissions.read -or
        -not $permission.permissions.write) {
        Invoke-GarageCli -Arguments @('bucket', 'allow', $bucketName, '--key', $keyName, '--read', '--write')
    }
}

$keyInfo = Invoke-GarageApi -Operation 'GetKeyInfo' -Body @{
    id = $keyId
    showSecretKey = $true
}

foreach ($bucketName in $productBuckets) {
    $permission = @($keyInfo.buckets | Where-Object {
        @($_.globalAliases) -contains $bucketName
    }) | Select-Object -First 1

    if ($null -eq $permission -or
        -not $permission.permissions.read -or
        -not $permission.permissions.write -or
        $permission.permissions.owner) {
        throw "Garage permissions for '$keyName' on '$bucketName' do not match Read/Write without Owner."
    }
}

foreach ($bucketPermission in @($keyInfo.buckets)) {
    $aliases = @($bucketPermission.globalAliases) + @($bucketPermission.localAliases)
    $isTarget = @($aliases | Where-Object { $productBuckets -contains $_ }).Count -gt 0
    $hasAnyPermission = $bucketPermission.permissions.read -or
        $bucketPermission.permissions.write -or
        $bucketPermission.permissions.owner
    if ($hasAnyPermission -and -not $isTarget) {
        throw "Garage key '$keyName' has permissions outside the two Product buckets."
    }
}
if ($keyInfo.permissions.createBucket) {
    throw "Garage key '$keyName' unexpectedly has global bucket-creation permission."
}

if (Test-Path -LiteralPath $secretFile) {
    Set-PrivateFileAcl -Path $secretFile
    $storedValues = Read-LocalEnvironmentFile -Path $secretFile
    if ($storedValues['GARAGE_S3_ACCESS_KEY_ID'] -cne [string]$keyInfo.accessKeyId -or
        $storedValues['GARAGE_S3_SECRET_ACCESS_KEY'] -cne [string]$keyInfo.secretAccessKey) {
        throw "The local credentials file does not match Garage key '$keyName'. It was not overwritten or rotated."
    }
}
else {
    Save-LocalEnvironmentFile -Path $secretFile `
        -AccessKeyId ([string]$keyInfo.accessKeyId) `
        -SecretAccessKey ([string]$keyInfo.secretAccessKey)
}

Write-Output 'Garage Product provisioning completed.'
Write-Output 'Buckets: product-media, product-staging (existing buckets/data preserved).'
Write-Output 'Key: product-api-local (Read/Write on the two Product buckets; no Owner or global CreateBucket).'
Write-Output 'Credentials saved to the ignored local file with current-user-only Windows ACL; no secret was displayed.'
