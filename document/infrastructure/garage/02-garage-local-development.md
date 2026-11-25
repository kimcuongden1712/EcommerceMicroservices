# Garage local development

This setup runs a single Garage node through the existing Docker Compose infrastructure. It is for local development only: replication factor 1 provides no redundancy or disaster recovery.

## Prerequisites

- Docker Desktop using Linux containers.
- Docker Compose v2.
- Windows PowerShell and `curl.exe` with AWS SigV4 support for `verify-local.ps1`.

## First run

Run these commands from `src`:

```powershell
$environmentFile = Join-Path $PWD "Infrastructure\Garage\.env.garage"
if (Test-Path -LiteralPath $environmentFile) {
    throw "Garage secrets already exist. Keep the existing file or remove it explicitly to rotate them."
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
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d garage
docker compose -f docker-compose.yml -f docker-compose.override.yml ps garage
docker compose -f docker-compose.yml -f docker-compose.override.yml logs garage
docker compose -f docker-compose.yml -f docker-compose.override.yml exec garage /garage status
```

The PowerShell commands create `Infrastructure\Garage\.env.garage` once and refuse to overwrite an existing file. Before writing any credentials, they remove inherited ACLs and verify that only the current Windows user has access. They generate independent RPC, Admin API, and metrics tokens without printing their values. The file is Git-ignored and excluded from Docker build contexts. `.env.garage.example` contains placeholders only; do not use those placeholders as credentials. Do not pass Garage Admin or RPC credentials to application services.

## Network and storage

| Purpose | Address / location |
| --- | --- |
| S3 API from Windows host | `http://localhost:3900` |
| S3 API from Compose services | `http://garage:3900` |
| Garage configuration | `Infrastructure\Garage\garage.toml` mounted read-only at `/etc/garage.toml` |
| Metadata | Named volume `ecommerce_garage_meta` |
| Object data | Named volume `ecommerce_garage_data` |

Only the S3 API is published, bound to the local machine. RPC port 3901 and Admin API port 3903 are not published to the host. The Compose network is the existing `ecommerce_microservices` network.

The Garage image is pinned to `dxflrs/garage:v2.5.0`, the image tag used by the official Garage Docker quick start. Compose overrides the image command with the documented `server --single-node` mode. The Garage CLI status command is used as the container health check.

Buckets and service credentials are provisioned separately from Garage startup. Product.API uses private `product-media` and `product-staging` buckets with its own least-privilege S3 key. See [Garage access control](./garage-access-control.md) for the permission matrix and credential handling.

## Product bucket provisioning

From the `src` directory, start Garage and run the local provisioning and verification scripts:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d garage
powershell -NoProfile -ExecutionPolicy Bypass -File .\Infrastructure\Garage\scripts\provision-local.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Infrastructure\Garage\scripts\verify-local.ps1
```

`-ExecutionPolicy Bypass` applies to that PowerShell process only; it does not change the machine or user execution policy. Provisioning is idempotent and does not delete buckets, objects, or keys, or rotate credentials. Verification performs short-lived signed S3 probes and creates a temporary cross-bucket isolation bucket; it removes its probes and temporary bucket when cleanup is safe.

The Product S3 credentials are stored in the Git-ignored `Infrastructure\Garage\.env.product-api-local`, separate from the Garage RPC/Admin secrets in `Infrastructure\Garage\.env.garage`. The script limits the credentials file to the current Windows user. It does not pass Garage Admin/RPC credentials to Product.API, and it does not yet inject the Product credentials into the service container; that wiring belongs to the Product storage integration phase.

S3 endpoints:

- From Windows: `http://localhost:3900`
- From a Compose service: `http://garage:3900`

## Persistence and shutdown

Stopping or recreating the Garage container preserves data in the named volumes. Do not use `docker compose down -v` unless intentionally deleting all Garage metadata and objects.

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml stop garage
docker compose -f docker-compose.yml -f docker-compose.override.yml start garage
```

The local single-node setup is not a backup. Back up or export any data that must be retained.

### Destructive reset

Do not reset Garage as part of normal setup. The following removes all Garage metadata, buckets, access keys, and stored objects. Confirm that all Garage data can be discarded and that no backup is needed before running it. It deliberately targets only the two Garage volumes and does not use `docker compose down -v`, which could remove unrelated service data.

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml stop garage
docker compose -f docker-compose.yml -f docker-compose.override.yml rm -f garage
docker volume rm ecommerce_garage_meta ecommerce_garage_data
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d garage
```

## Troubleshooting

- If Compose reports that `Infrastructure\Garage\.env.garage` is missing, run the PowerShell secret-generation commands above from `src`.
- Inspect startup failures with `docker compose -f docker-compose.yml -f docker-compose.override.yml logs garage`.
- Confirm health with `docker compose -f docker-compose.yml -f docker-compose.override.yml ps garage` and `exec garage /garage status`.
- Confirm that Docker Desktop is running Linux containers and that host port 3900 is available.

## References

- [Garage Quick Start](https://garagehq.deuxfleurs.fr/documentation/quick-start/)
- [Garage configuration reference](https://garagehq.deuxfleurs.fr/documentation/reference-manual/configuration/)
- [Garage deployment guide](https://garagehq.deuxfleurs.fr/documentation/cookbook/real-world/)
