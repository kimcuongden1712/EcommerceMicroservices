# Garage access control

## English

## Architecture

Garage is shared object-storage infrastructure. Each business service receives its own S3 access key and only the bucket permissions required by that service. Applications use the S3 API at `http://garage:3900` on the Compose network; the Garage Admin API and RPC port are not published to the host.

The local Garage deployment is a single-node development setup. It is not highly available and does not replace backups.

## Bucket inventory

| Service | Bucket | Purpose | Public |
| --- | --- | --- | --- |
| Product.API | `product-media` | Product images accepted for official use | No |
| Product.API | `product-staging` | Temporary uploads and validation | No |

Buckets are private. Garage website access is disabled. No anonymous S3 access is configured.

## Access-key inventory

| Key | Service | Buckets | Permissions |
| --- | --- | --- | --- |
| `product-api-local` | Product.API (local development) | `product-media`, `product-staging` | Read and Write; no Owner; no global CreateBucket |

No access key ID or secret is recorded in this document. The Product key is not shared with Garage administration or any other service.

### Permission matrix

| Principal | `product-media` | `product-staging` | Other buckets | Create bucket | Bucket administration |
| --- | --- | --- | --- | --- | --- |
| `product-api-local` | Read + Write | Read + Write | Denied | Denied | Denied |
| Garage administrator | Administrative control | Administrative control | Administrative control | Allowed | Allowed |
| Anonymous | Denied | Denied | Denied | Denied | Denied |

## Proposed future bucket names

These are naming proposals only. They are not provisioned and have no access keys:

- `customer-files`
- `order-documents`
- `payment-evidence`
- `inventory-media`

Create buckets only after the corresponding service source and storage use case are confirmed.

## Provision and verify local access

From the repository's `src` directory, start Garage and run:

```powershell
docker compose -f docker-compose.yml -f docker-compose.override.yml up -d garage
powershell -NoProfile -ExecutionPolicy Bypass -File .\Infrastructure\Garage\scripts\provision-local.ps1
powershell -NoProfile -ExecutionPolicy Bypass -File .\Infrastructure\Garage\scripts\verify-local.ps1
```

The provisioning script is safe to rerun: it creates only missing Product buckets and the named key, adds only missing Read/Write permissions on the two Product buckets, and never deletes or rotates resources. It stops for broader existing key permissions rather than silently reducing them.

The verification script performs signed S3 PUT/GET/DELETE checks in both Product buckets, anonymous read/write checks, and a cross-bucket denial check. For the latter, it creates a uniquely named temporary bucket, tests that Product access is denied, and removes the bucket when it is safe to do so. Review the script output and ensure the temporary bucket cleanup check passes.

## Secret handling

The local Product credentials are stored in the ignored file `src/Infrastructure/Garage/.env.product-api-local`, separate from `src/Infrastructure/Garage/.env.garage` (which holds Garage infrastructure secrets). On Windows both files have ACLs restricted to the current user. The Product provisioning script verifies its credentials file ACL and fails rather than reporting success if it cannot enforce it.

The secret is captured without printing it to the terminal. The file is excluded from Docker build contexts. It is not yet injected into the Product.API container; the storage integration phase must wire the needed environment variables into the service. Never copy Garage RPC or Admin credentials into a business service.

Garage v2.5.0 can return a stored S3 secret through an explicitly privileged `GetKeyInfo` request (`showSecretKey=true`); the CLI equivalent is `garage key info --show-secret`. The secret is omitted by default. Provisioning uses the explicit API option only to persist the local key safely and never writes it to tracked files or documentation.

## Rotation and revocation

Rotation is a controlled manual operation, not part of local provisioning:

1. Create a replacement Product key and securely capture its credentials without writing them to shell history, terminal logs, or tracked files.
2. Grant it Read/Write on only `product-media` and `product-staging`; verify permissions and signed S3 operations.
3. Update the local secret file and any consuming service configuration, then verify the service with the replacement key.
4. Revoke the old key only after the replacement is confirmed in use.

Revocation immediately removes that key's access to all buckets. Garage CLI requires explicit confirmation for deletion (`garage key delete <key-id> --yes`). Do not delete the only working key before the replacement is validated. Keep the Garage Admin/RPC credentials separate from all S3 keys.

## Security boundaries

- Use one key per service; never share unrestricted S3 credentials.
- Keep buckets private and do not enable website/public access for product media.
- Grant only bucket-level Read/Write permissions required by the service; do not grant Owner or global bucket creation.
- Store local S3 secrets in the ignored, ACL-restricted local file. Do not commit credentials or paste them into logs, markdown, or shell history.
- Garage Admin tokens and the RPC secret are infrastructure credentials. Do not expose them to Product.API.
- The Compose setup publishes only loopback S3 port 3900. It does not publish Admin API or RPC ports.

## Version-specific references

- [Garage CLI reference](https://garagehq.deuxfleurs.fr/documentation/reference-manual/cli/)
- [Garage Administration API](https://garagehq.deuxfleurs.fr/documentation/reference-manual/admin-api/)
- [Garage v2.5.0 Admin API specification](https://garagehq.deuxfleurs.fr/api/garage-admin-v2.json)

## Tiếng Việt

Garage là object storage dùng chung. Mỗi business service có S3 key riêng, chỉ được cấp quyền trên bucket cần thiết. Ứng dụng gọi S3 API tại `http://garage:3900` trong Compose; Admin API và RPC không publish ra host. Garage local chạy một node, không HA và không thay thế backup.

Product.API dùng hai bucket private đã provision: `product-media` lưu ảnh được chấp nhận; `product-staging` lưu upload tạm và xác minh. Không bật website/anonymous access. Key `product-api-local` có Read/Write trên đúng hai bucket, không Owner, không quyền CreateBucket toàn cục. Không lưu access key ID hay secret trong tài liệu.

Provision local từ thư mục `src` bằng `provision-local.ps1`, sau đó chạy `verify-local.ps1` theo [hướng dẫn local](./02-garage-local-development.md). Script provision chỉ tạo tài nguyên còn thiếu và không xóa/rotate; nếu quyền key hiện hữu rộng hơn dự kiến thì dừng để tránh tự ý hạ quyền. Verification tạo probe tạm và bucket cô lập để kiểm tra biên quyền, rồi dọn chúng khi an toàn. Không chạy trên môi trường production.

Quy tắc: mỗi service dùng key riêng; chỉ cấp Read/Write ở bucket cần dùng; không dùng Garage Admin/RPC token trong ứng dụng; không chia sẻ Product key; không dùng bucket Product cho integration test xóa dữ liệu; không sửa bucket, credential hoặc policy nếu chưa được duyệt. CORS hiện chưa bật và không phải cơ chế phân quyền.
