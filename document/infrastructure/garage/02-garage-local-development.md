# Garage local development

## English

This setup runs Garage as an independent Docker Compose stack, separate from the application stack. It is for local development only: replication factor 1 provides no redundancy or disaster recovery.

## Prerequisites

- Docker Desktop using Linux containers.
- Docker Compose v2.
- Windows PowerShell and `curl.exe` with AWS SigV4 support for `verify-local.ps1`.

## First run

From the repository root, copy this single command into **CMD or PowerShell**:

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\src\Infrastructure\Garage\scripts\initialize-local.ps1"
```

The [initialization script](../../../src/Infrastructure/Garage/scripts/initialize-local.ps1) resolves paths relative to its own location. It creates `.env.garage` once, restricts its ACL to the current Windows user before writing credentials, and generates independent RPC, Admin API and metrics tokens without displaying them. Running it again keeps the existing file unchanged. Expected output is `Garage local secrets initialized.` or `Garage secrets already exist; keeping the existing file unchanged.`

`-ExecutionPolicy Bypass` applies only to this PowerShell process. The file remains Git-ignored and excluded from Docker build contexts. Do not use `.env.garage.example` placeholders as credentials or pass Garage RPC/Admin tokens to applications. Initialization does not start Docker or create buckets/keys.

Start the standalone Garage stack from `src`; this step requires only Garage's own local secrets:

```text
docker compose -f docker-compose.garage.yml up -d
```

This Compose project creates the `ecommerce_garage` network and Garage data volumes without loading Product.API credentials. No placeholder or fake S3 credential is needed to start Garage.

## Network and storage

| Purpose | Address / location |
| --- | --- |
| S3 API from Windows host | `http://localhost:3900` |
| S3 API from application Compose services | `http://garage:3900` on external network `ecommerce_garage` |
| Garage configuration | `Infrastructure\Garage\garage.toml` mounted read-only at `/etc/garage.toml` |
| Metadata | Named volume `ecommerce_garage_meta` |
| Object data | Named volume `ecommerce_garage_data` |

Only the S3 API is published, bound to the local machine. RPC port 3901 and Admin API port 3903 are not published to the host. Garage uses dedicated network `ecommerce_garage`; Product.API joins it as an external dependency, while the application Compose project does not start Garage.

The Garage image is pinned to `dxflrs/garage:v2.5.0`, the image tag used by the official Garage Docker quick start. Compose overrides the image command with the documented `server --single-node` mode. The Garage CLI status command is used as the container health check.

Buckets and service credentials are provisioned separately from Garage startup. Product.API uses private `product-media` and `product-staging` buckets with its own least-privilege S3 key. See [Garage access control](./03-garage-access-control.md) for the permission matrix and credential handling.

## Product bucket provisioning

From the `src` directory, start Garage and run the local provisioning and verification scripts:

```powershell
docker compose -f docker-compose.garage.yml up -d
powershell -NoProfile -ExecutionPolicy Bypass -File .\Infrastructure\Garage\scripts\provision-local.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Infrastructure\Garage\scripts\verify-local.ps1
```

`-ExecutionPolicy Bypass` applies to that PowerShell process only; it does not change the machine or user execution policy. Provisioning is idempotent and does not delete buckets, objects, or keys, or rotate credentials. Verification performs short-lived signed S3 probes and creates a temporary cross-bucket isolation bucket; it removes its probes and temporary bucket when cleanup is safe.

The Product S3 credentials are stored in the Git-ignored `Infrastructure\Garage\.env.product-api-local`, separate from the Garage RPC/Admin secrets in `Infrastructure\Garage\.env.garage`. The script limits the credentials file to the current Windows user. Load this file as Compose interpolation input to pass only the Product S3 endpoint, region, access key, and secret into Product.API. Garage RPC/Admin secrets remain available only to the Garage container.

From the `src` directory, start Product.API and its dependencies with:

```powershell
docker compose --env-file .\Infrastructure\Garage\.env.product-api-local -f docker-compose.yml -f docker-compose.override.yml up -d product.api
```

S3 endpoints:

- From Windows: `http://localhost:3900`
- From a Compose service: `http://garage:3900`

The S3 endpoint used to generate presigned URLs must be reachable by the caller. When Product.API runs in Compose and the caller is the Windows host, keep `GARAGE_S3_ENDPOINT=http://garage:3900` for Product.API storage calls and use `GARAGE_S3_PUBLIC_ENDPOINT=http://localhost:3900` for presigned URLs. The public endpoint is an address placeholder for the local S3 API, not a storefront origin.

### Direct browser upload CORS (deferred until MFE origins are approved)

The browser uploads the file directly to the private `product-staging` bucket. CORS is therefore a bucket-level setting in Garage, separate from the Product API URL and separate from Garage credentials or bucket permissions. The current provisioning script deliberately does not create, replace, or enable a CORS rule. With no approved storefront origin, CORS remains unset.

Configure an allowlist independently for each environment only after the MFE deployment gives the exact browser origins:

| Environment | Allowed origin | Allowed method | Allowed request header | Exposed response headers |
| --- | --- | --- | --- | --- |
| Local | `<exact local MFE origin>` | `PUT` | `Content-Type` | none required |
| Production | `<exact production MFE origin>` | `PUT` | `Content-Type` | none required |

Replace the placeholders with concrete origins including scheme and, when applicable, port (for example, `https://host.example` or `http://host.example:port`). Do not use `*`, a path, a trailing URL path, or a guessed localhost port. Keep the local and production rules in separate deployment configuration. Do not apply either rule to Garage until the corresponding origin is approved.

The allowlist can be represented by an environment-specific deployment value such as `PRODUCT_STAGING_CORS_ALLOWED_ORIGINS`, with an empty/unset value meaning “do not call Garage PutBucketCors.” The variable is intentionally not added to the current secret or provisioning output: no origin is known yet, and the existing script must preserve the current Garage configuration. When an origin is approved, the deployment owner should apply a narrow `product-staging` CORS document through the environment’s controlled Garage/S3 configuration process and record the applied origin, method, and header. This does not change bucket permissions or private access.

#### Signed request contract

`Product.API` signs a `PUT` request. The presign request sets `Content-Type` to the upload session's validated MIME type, and the API response's `headers` map contains exactly `Content-Type` with that value. The browser request must be:

```http
PUT <uploadUrl>
Content-Type: <same value as response.headers.Content-Type>

<raw file bytes>
```

The browser supplies `Origin` and may send an `OPTIONS` preflight before `PUT`; those are CORS protocol headers and are not signed by Product.API. `Content-Length` is managed by the browser and is not a required signed header. Do not add an `Authorization` header, rewrite the presigned query string, send JSON or multipart data, or change the MIME type. A CORS rule must allow the exact MFE origin, `PUT`, and `Content-Type`; no response header needs to be exposed for the current client contract.

#### Browser calls to Product.API

Garage CORS only covers the direct S3 upload. If the MFE calls Product.API or its gateway from a different origin, that API boundary also needs its own environment-specific allowlist. Product.API currently has no `AddCors`/`UseCors` policy. Keep this unset until the MFE/API routing topology and origins are confirmed; do not assume Garage CORS makes API calls work. For the current browser contract, evaluate `GET`, `POST`, and `PUT`, request headers `Authorization`, `Content-Type`, and `If-Match`, and expose `ETag` if the browser must read the API's version response header. Configure this at the actual browser-facing API or gateway boundary, with exact origins and no wildcard. If the MFE and API are same-origin through a backend-for-frontend, browser CORS may not be needed for that boundary.

For deployment configuration, keep the two boundaries independently keyed per environment, for example `PRODUCT_API_CORS_ALLOWED_ORIGINS` and `PRODUCT_STAGING_CORS_ALLOWED_ORIGINS`; empty/unset values leave the associated policy disabled. These are names for future deployment configuration only, not active values or provisioning-script inputs.

## Persistence and shutdown

Stopping or recreating the Garage container preserves data in the named volumes. Do not use `docker compose down -v` unless intentionally deleting all Garage metadata and objects.

```powershell
docker compose -f docker-compose.garage.yml stop garage
docker compose -f docker-compose.garage.yml start garage
```

The local single-node setup is not a backup. Back up or export any data that must be retained.

### Destructive reset

Do not reset Garage as part of normal setup. The following removes all Garage metadata, buckets, access keys, and stored objects. Confirm that all Garage data can be discarded and that no backup is needed before running it. It deliberately targets only the two Garage volumes and does not use `docker compose down -v`, which could remove unrelated service data.

```powershell
docker compose -f docker-compose.garage.yml stop garage
docker compose -f docker-compose.garage.yml rm -f garage
docker volume rm ecommerce_garage_meta ecommerce_garage_data
docker compose -f docker-compose.garage.yml up -d
```

## Troubleshooting

- If Compose reports that `Infrastructure\Garage\.env.garage` is missing, run the initialization script above from the repository root.
- Inspect startup failures with `docker compose -f docker-compose.yml -f docker-compose.override.yml logs garage`.
- Confirm health with `docker compose -f docker-compose.yml -f docker-compose.override.yml ps garage` and `exec garage /garage status`.
- Confirm that Docker Desktop is running Linux containers and that host port 3900 is available.

## References

- [Garage Quick Start](https://garagehq.deuxfleurs.fr/documentation/quick-start/)
- [Garage configuration reference](https://garagehq.deuxfleurs.fr/documentation/reference-manual/configuration/)
- [Garage v2.5.0 S3 compatibility reference](https://github.com/deuxfleurs-org/garage/blob/v2.5.0/doc/book/reference-manual/s3-compatibility.md) (lists `PutBucketCors`, `GetBucketCors`, and `DeleteBucketCors` as implemented; run the optional runtime test against the deployed version before enabling it)
- [Garage deployment guide](https://garagehq.deuxfleurs.fr/documentation/cookbook/real-world/)

## Tiếng Việt

### Điều kiện và khởi động lần đầu

Cần Docker Desktop dùng Linux containers, Docker Compose v2, Windows PowerShell và `curl.exe` hỗ trợ AWS SigV4 cho bước verification. Từ thư mục gốc repository, copy **một lệnh này vào CMD hoặc PowerShell**:

```text
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\src\Infrastructure\Garage\scripts\initialize-local.ps1"
```

Script tự xác định đường dẫn theo vị trí của nó, tạo `.env.garage`, giới hạn ACL cho user hiện tại rồi sinh token ngẫu nhiên không in giá trị. Chạy lại sẽ giữ nguyên file đã có. Kết quả: `Garage local secrets initialized.` hoặc `Garage secrets already exist; keeping the existing file unchanged.` ExecutionPolicy chỉ áp dụng cho tiến trình này. Script chỉ chuẩn bị secret, không khởi động Docker hay tạo bucket/key.

Với môi trường đã provision, từ `src` chạy:

```text
docker compose -f docker-compose.garage.yml up -d
```

Sau khi tạo secret, từ `src` khởi động Garage bằng Compose độc lập:

```text
docker compose -f docker-compose.garage.yml up -d
```

Stack chỉ đọc `.env.garage`, tạo network và volume riêng; không cần `.env.product-api-local` hay credential S3 của Product.API. Không điền credential giả.

### Mạng và dữ liệu

Image trong Compose được pin `dxflrs/garage:v2.5.0`, chạy chế độ single-node. Host chỉ publish S3 port 3900 trên localhost; RPC 3901 và Admin 3903 không publish. Metadata và object lưu trong named volume `ecommerce_garage_meta` và `ecommerce_garage_data`. Cấu hình `garage.toml` mount read-only. Single-node local không có dự phòng/DR.

### Provision bucket và chạy Product.API

Từ `src`, chạy Garage bằng `docker-compose.garage.yml`, sau đó chạy `provision-local.ps1` và `verify-local.ps1` như lệnh English phía trên. Provisioning tạo `.env.product-api-local` riêng; chỉ sau bước này mới chạy Product.API bằng Compose ứng dụng. Hai stack giao tiếp qua network `ecommerce_garage`. Không chuyển Garage Admin/RPC secret sang Product.API. Endpoint Compose là `http://garage:3900`, endpoint host là `http://localhost:3900`.

### CORS, dừng và xử lý lỗi

Garage bucket CORS và Product.API CORS hiện chưa bật. Không điền origin giả hoặc `*`; chỉ cấu hình sau khi có origin MFE chính xác và phê duyệt riêng. Garage CORS chỉ áp dụng browser upload trực tiếp vào staging; API CORS áp dụng browser gọi API. Dừng/khởi động lại bằng lệnh Compose ở phần English; named volume giữ dữ liệu. Tránh `docker compose down -v`. Khối “Destructive reset” trong phần English sẽ xóa toàn bộ metadata/object Garage, không dùng trong setup thường ngày.

Nếu thiếu `.env.garage`, chạy quy trình first-run; xem log bằng `docker compose -f docker-compose.garage.yml logs garage`; xác minh bằng `docker compose -f docker-compose.garage.yml ps garage` và `docker compose -f docker-compose.garage.yml exec garage /garage status`. Nếu Product.API báo thiếu network `ecommerce_garage`, khởi động Garage stack trước. Lỗi kết nối còn có thể do Docker/Linux containers hoặc host port 3900 đang bận.
