# Shared .NET object storage

## English

## Architecture

`ObjectStorage.Abstractions` defines a provider-neutral API and models. `ObjectStorage.S3` implements that API using the AWS SDK for .NET and is configured for Garage through its S3-compatible endpoint. Business services depend on the abstraction, not on AWS SDK types. The library does not create buckets, apply ACLs, or contain service-specific object naming rules.

Garage remains private by default. Access is determined by the Garage key's bucket permissions; the adapter does not send public ACLs.

## Projects and dependencies

- `src/BuildingBlocks/ObjectStorage.Abstractions`: interfaces, models, and `ObjectStorageException`; no AWS, Garage, database, or Product.API dependency.
- `src/BuildingBlocks/ObjectStorage.S3`: S3 options, adapter, and DI registration.
- `src/BuildingBlocks/ObjectStorage.S3.Tests`: unit tests and opt-in Garage integration test.

The S3 project uses `AWSSDK.S3` 4.1.2 and Microsoft.Extensions options/DI packages 11.0.0. The AWS S3 client is registered as a singleton and reused for the application lifetime.

## Configuration

Configure the `ObjectStorage:S3` section from an environment-specific secret source:

| Key | Description |
| --- | --- |
| `ServiceUrl` | S3 API base URL |
| `Region` | Signature region; the local Garage value is `garage` |
| `AccessKey` | Service-specific Garage S3 access key |
| `SecretKey` | Secret for that S3 access key |
| `ForcePathStyle` | Use path-style requests; `true` for the local Garage setup |

Environment variable names use the double-underscore convention, for example `ObjectStorage__S3__ServiceUrl`. Do not put credentials in tracked `appsettings.json`, solution items, or documentation. Never use Garage RPC/Admin credentials as S3 credentials.

Local endpoints:

- Application in Docker Compose: `http://garage:3900`
- Application running on the Windows host: `http://localhost:3900`

The library does not hard-code either endpoint. Supply the correct endpoint for the application network.

## Dependency injection

```csharp
using ObjectStorage.S3.Extensions;

builder.Services.AddS3ObjectStorage(builder.Configuration);
```

Options are validated at startup. Required values are an absolute HTTP(S) `ServiceUrl` without user-info/query/fragment, a non-empty region, and non-empty access/secret keys. Validation errors do not include configured values.

## Usage

Inject `IObjectStorage` from `ObjectStorage.Abstractions.Interfaces`:

```csharp
using ObjectStorage.Abstractions.Interfaces;
using ObjectStorage.Abstractions.Models;

public sealed class ImageService(IObjectStorage storage)
{
    public async Task<ObjectDescriptor> UploadAsync(
        Stream content,
        long contentLength,
        CancellationToken cancellationToken)
    {
        return await storage.UploadAsync(
            new ObjectUploadRequest
            {
                BucketName = "service-owned-bucket",
                ObjectKey = "images/example.png",
                ContentType = "image/png",
                Metadata = new Dictionary<string, string> { ["source"] = "catalog" }
            },
            content,
            contentLength,
            cancellationToken);
    }
}
```

The caller owns upload and download streams. Upload requires the content length so the adapter can set the HTTP `Content-Length` without buffering the object; for seekable streams, the length must match the remaining bytes. Download writes to the supplied destination stream. Metadata returns `null` only for an absent object; `ExistsAsync` returns `false` only for a confirmed object NotFound. Other service, authorization, timeout, and network failures are surfaced as `ObjectStorageException` with the original exception as `InnerException`. Caller cancellation is propagated. `ObjectDescriptor.LastModified` is `null` after upload because the S3 PutObject response does not return that value.

`CopyAsync` accepts source and destination bucket/key explicitly. The AWS SDK constructs the copy-source header from the source bucket/key; callers should pass the original unescaped key.

## Presigned URLs

`CreatePresignedUploadUrlAsync` signs a PUT request. If `ContentType` is supplied, the caller must send the returned `Content-Type` header with exactly that value. GET URLs require no additional headers by default. Do not edit the signed URL or its query string. URLs should be shared only with the intended recipient and treated as temporary bearer credentials. Expiration must be greater than zero and at most seven days.

## Credentials and local Garage

Use the per-service S3 key and least-privilege bucket permissions created during Garage provisioning. Store the key outside tracked configuration, using the existing local ignored secret mechanism or the deployment platform's secret store. Do not copy secret values into this guide or command history. Integration tests require a separate test key and bucket; Product's `product-media` and `product-staging` buckets are prohibited for destructive tests.

## Tests

Run unit tests:

```powershell
dotnet test --project .\src\BuildingBlocks\ObjectStorage.S3.Tests\ObjectStorage.S3.Tests.csproj
```

The repository's `global.json` selects Microsoft.Testing.Platform for .NET 10, which is the xUnit v3 test runner used by this project.

The Garage integration test is skipped unless all of these process environment variables are set:

- `OBJECT_STORAGE_TEST_SERVICE_URL`
- `OBJECT_STORAGE_TEST_REGION`
- `OBJECT_STORAGE_TEST_ACCESS_KEY`
- `OBJECT_STORAGE_TEST_SECRET_KEY`
- `OBJECT_STORAGE_TEST_BUCKET`

Configure an isolated, pre-provisioned bucket and a dedicated key with object read, write, and delete permissions limited to that bucket. These cover GET/HEAD, copy source/destination, PUT, and cleanup. The test creates unique object keys and removes only objects it created; it never creates or deletes a bucket. It checks upload, HEAD/metadata, download, copy with special characters in the key, delete, presigned GET, and presigned PUT.

## Known limitations

- Garage compatibility documentation lists PutObject, GetObject, HeadObject, CopyObject, DeleteObject, and presigned URLs as implemented. The compatibility page is not tied to the exact deployed Garage release, so the optional integration test is the runtime check for Garage v2.5.0.
- The adapter uses single-request `PutObjectAsync`; it does not provide multipart upload. AWS S3 PutObject is limited to 5 GB per object.
- Garage does not implement S3 bucket versioning. The API does not expose version-management operations.
- ETags are passed through as provider values and are not promised to be MD5 hashes.
- Presigned URLs are bearer credentials until expiry; use short expirations and do not log them.

## Tiếng Việt

`ObjectStorage.Abstractions` định nghĩa API và model trung lập với nhà cung cấp. `ObjectStorage.S3` hiện thực API bằng AWS SDK for .NET và kết nối Garage qua S3 endpoint. Business service phụ thuộc abstraction, không phụ thuộc kiểu AWS SDK. Thư viện không tạo bucket, áp ACL hay chứa quy tắc đặt tên object riêng theo service. Garage private; quyền truy cập do quyền bucket của Garage key quyết định.

### Project và cấu hình

- `src/BuildingBlocks/ObjectStorage.Abstractions`: interface, model và `ObjectStorageException`, không phụ thuộc AWS/Garage/database/Product.API.
- `src/BuildingBlocks/ObjectStorage.S3`: options, adapter và DI registration.
- `test/BuildingBlocks/ObjectStorage.S3.Tests`: unit test và integration test Garage opt-in.

Cấu hình `ObjectStorage:S3`: `ServiceUrl`, `PresignedServiceUrl` tùy chọn, `Region`, `AccessKey`, `SecretKey`, `ForcePathStyle`. Local region là `garage`, path-style bật. Dùng biến môi trường dạng `ObjectStorage__S3__ServiceUrl`; không commit secret và không dùng Garage RPC/Admin token làm S3 credential. Từ Compose dùng `http://garage:3900`, từ Windows host dùng `http://localhost:3900`. `PresignedServiceUrl` cho phép dùng hostname mà caller của URL có thể truy cập.

Đăng ký bằng `builder.Services.AddS3ObjectStorage(builder.Configuration)`. Options được validate lúc khởi động; URL phải là HTTP(S) tuyệt đối, không có user-info/query/fragment; region/key/secret bắt buộc. AWS S3 client đăng ký singleton.

### Hành vi và xử lý lỗi

Inject `IObjectStorage`. Caller sở hữu stream. Upload cần content length; với stream seekable, length phải bằng số byte còn lại. Download ghi vào stream đích. `GetMetadataAsync` trả null và `ExistsAsync` trả false chỉ khi xác định object NotFound. Lỗi quyền, service, timeout và network được bọc trong `ObjectStorageException` với lỗi gốc ở `InnerException`; cancellation được truyền tiếp. `LastModified` của descriptor sau upload là null vì PutObject không trả giá trị này.

`CopyAsync` nhận bucket/key nguồn và đích; truyền object key gốc chưa URL-escape. Presigned PUT yêu cầu gửi đúng `Content-Type` đã ký. Không sửa URL/query; URL là bearer credential tạm thời, không ghi log và hạn tối đa 7 ngày. Adapter dùng `PutObjectAsync` đơn, không hỗ trợ multipart; ETag không được cam kết là MD5; Garage không hỗ trợ bucket versioning.

### Kiểm thử

Unit test: `dotnet test .\test\BuildingBlocks\ObjectStorage.S3.Tests\ObjectStorage.S3.Tests.csproj`. Garage integration test chỉ chạy khi có đủ `OBJECT_STORAGE_TEST_SERVICE_URL`, `OBJECT_STORAGE_TEST_REGION`, `OBJECT_STORAGE_TEST_ACCESS_KEY`, `OBJECT_STORAGE_TEST_SECRET_KEY`, `OBJECT_STORAGE_TEST_BUCKET`. Chỉ dùng bucket/key riêng biệt đã provision, có quyền object read/write/delete trên bucket đó. Không dùng bucket `product-media` hoặc `product-staging` cho test destructive. Test tạo key ngẫu nhiên và xóa riêng object của nó, không tạo/xóa bucket.
