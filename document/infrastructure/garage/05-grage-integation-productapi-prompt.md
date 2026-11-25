# PHASE 4 — PRODUCT.API INTEGRATION WITH GARAGE OBJECT STORAGE

## 1. ROLE

You are a Senior .NET Solution Architect, Backend Developer, and QA Engineer.

You are working on the existing repository:

`kimcuongden1712/EcommerceMicroservices`

Your task is to implement Phase 4: integrate the existing Product.API with the shared Garage S3-compatible Object Storage infrastructure created in Phase 1, Phase 2, and Phase 3.

You must work with the actual repository source code. Do not assume that previous architectural examples are identical to the current implementation.

Use the existing codebase, existing API contracts, database entities, repositories, authorization policies, middleware, and shared storage libraries.

Explain technical decisions and implementation reports in Vietnamese. Keep source code, identifiers, comments, and XML documentation in English, consistent with repository conventions.

---

## 2. CONFIRMED ARCHITECTURE DECISIONS

These decisions are fixed. Do not change them.

- Storage topology: `shared`.
- One shared Garage cluster for all microservices.
- S3 integration: `shared-lib`.
- Delivery model: `hybrid`.
- Access policy: `private`.
- Environment strategy: `local-first`.
- Product.API is the first real business-service integration.
- No separate FileStorage microservice.
- No separate GarageLab environment.
- Garage buckets are private by default.
- Product API owns product-image business rules, metadata, authorization, lifecycle, and cleanup.
- Shared storage libraries own only generic S3-compatible object-storage operations.
- Product.API must not directly depend on AWS SDK types.
- Product.API must use the Phase 3 `ObjectStorage.Abstractions` interface and DI registration.
- No anonymous bucket access.
- No credentials or secrets in tracked configuration.
- Do not create buckets automatically from Product.API.
- Do not integrate other business services in this phase.

### Existing bucket/key design

Use the existing provisioned buckets:

- `product-staging`
- `product-media`

Use the existing Product API service key and credentials provisioned in Phase 2.

Do not create, rotate, overwrite, or delete Garage credentials or buckets in this phase.

If the current environment does not contain the expected buckets or credentials, report the problem and stop the affected integration tests. Do not silently create infrastructure.

---

## 3. MANDATORY DISCOVERY — GATE A

Before editing source code, inspect the working tree.

The local working tree may contain Phase 1–3 changes that are not present on GitHub master. The local working tree is authoritative.

### 3.1. Inspect repository state

Inspect:

- `git status`
- current branch
- recent commits
- uncommitted changes
- solution/project structure
- target framework
- package management conventions
- existing test frameworks
- current Docker Compose configuration
- current Phase 1/2 Garage configuration and scripts
- Phase 3 storage libraries and tests

Do not reset, stash, discard, or overwrite existing user changes.

### 3.2. Inspect Phase 3 implementation

Find and read the actual implementations of:

- `ObjectStorage.Abstractions`
- `ObjectStorage.S3`
- `IObjectStorage`
- upload/download/metadata/exists/delete/copy methods
- presigned PUT and GET methods
- `S3StorageOptions`
- `S3ObjectStorage`
- DI extension, such as `AddS3ObjectStorage`
- exception types
- test projects
- configuration examples
- Phase 3 documentation

Record the exact namespaces, method signatures, parameter types, return types, DI method name, and package versions.

Do not create duplicate interfaces or duplicate storage clients.

Do not change Phase 3 public interfaces merely to make Product.API integration easier. If a genuine missing capability is found, report it and propose the smallest backward-compatible change.

### 3.3. Inspect Product.API

Read the actual source code for:

- `src/Services/Product.API/Product.API.csproj`
- `src/Services/Product.API/Program.cs`
- `src/Services/Product.API/Persistence/ProductContext.cs`
- `src/Services/Product.API/Entities/ProductImages.cs`
- `src/Services/Product.API/Entities/ProductImageUploads.cs`
- `src/Services/Product.API/Repositories/ProductImageRepository.cs`
- `src/Services/Product.API/Repositories/Interfaces/`
- `src/Services/Product.API/Controllers/V1/`
- `src/Services/Product.API/Services/V1/`
- `src/Services/Product.API/Contracts/V1/`
- `src/Services/Product.API/Commons/Enums/`
- `src/Services/Product.API/Authorization/`
- existing middleware and exception handling
- existing product repositories and services
- existing product version/ETag implementation
- existing migration conventions
- existing tests

Also inspect:

- `document/service/product-api/02-api-contract.md`
- `document/service/product-api/03-processing-integrations.md`
- `document/service/product-api/04-delivery-testing.md`
- the Product API domain/data document
- release/acceptance requirements relevant to product images

### 3.4. Known existing domain model

The previously inspected repository snapshot contains:

`ProductImages`:
- ProductId
- AssetKey
- AltText
- Position

`ProductImageUploads`:
- ProductId
- StagingKey
- AssetKey
- ExpectedContentType
- ExpectedSize
- Status
- ExpiresAt
- CreatedAt

Existing upload status values:

- Pending = 0
- Ready = 1
- Rejected = 2
- Expired = 3

`ProductContext` already contains DbSets for ProductImages and ProductImageUploads.

`ProductImageRepository` already has image-query logic and primary-image readiness logic.

These are known from an earlier repository snapshot. Verify the current local source before relying on them.

### 3.5. Gate A output

Do not modify source code in Gate A.

Produce a discovery report containing:

1. Current repository state.
2. Exact Phase 3 library implementation and namespaces.
3. Exact storage interface method signatures.
4. Exact Product.API image entities and enum.
5. Existing Product.API endpoint and service conventions.
6. Existing authorization and ETag mechanisms.
7. Existing DB schema/migrations.
8. Existing test projects and frameworks.
9. Current Garage endpoint/bucket configuration.
10. Gaps between current source and the target Phase 4 behavior.
11. Exact file-by-file implementation plan.
12. Potential breaking changes and risks.
13. Commands that will be used to verify the implementation.

Separate confirmed facts from assumptions.

Stop after Gate A and wait for explicit approval.

---

## 4. PHASE 4 OBJECTIVE

Implement a complete Product.API product-image lifecycle using Garage through the Phase 3 shared object-storage libraries.

The expected lifecycle is:

1. Admin creates an upload session.
2. Product.API validates the request.
3. Product.API creates a pending upload record.
4. Product.API generates a presigned upload URL for the private staging bucket.
5. Client uploads directly to Garage.
6. Product.API background processing verifies the uploaded object.
7. Valid objects are copied to the private media bucket.
8. Upload session becomes Ready.
9. Admin updates the product gallery.
10. Public catalog reads images through controlled delivery.
11. Expired, rejected, orphaned, and unreferenced objects are cleaned safely.

Implement only the scope described below.

---

## 5. API CONTRACT — PRESERVE EXISTING CONTRACT

Use the existing API contract as the source of truth.

Expected endpoints:

### 5.1. Create upload session

`POST /api/v1/admin/products/{id}/image-uploads`

Expected request:

```json
{
  "contentType": "image/webp",
  "contentLength": 245760
}
```

Expected response concept:

```json
{
  "uploadId": "123",
  "uploadUrl": "https://...",
  "headers": {
    "Content-Type": "image/webp"
  },
  "expiresAt": "2026-11-24T12:00:00Z"
}
```

The exact DTO shape must follow the existing API contract and naming conventions.

Behavior:

- Require the existing `CatalogAdmin` policy.
- Verify the product exists.
- Validate content type.
- Validate content length.
- Enforce the per-product image limit.
- Generate a unique upload ID.
- Generate a server-controlled staging object key.
- Create a Pending upload record.
- Generate a presigned PUT URL for `product-staging`.
- Return the exact headers required by the signature.
- Return the URL expiration time.
- Never expose Garage credentials.

The client must not choose:

- Bucket name.
- Staging key.
- Asset key.
- Arbitrary storage endpoint.

The server must use configured bucket names.

### 5.2. Read upload session

`GET /api/v1/admin/products/{id}/image-uploads/{uploadId}`

Behavior:

- Require `CatalogAdmin`.
- Verify the upload belongs to the requested product.
- Return Pending, Ready, Rejected, or Expired.
- Do not expose secret keys or internal Garage endpoint details.
- Return 404 for a session that does not belong to the product or does not exist, according to existing API conventions.

### 5.3. Replace product gallery

`PUT /api/v1/admin/products/{id}/images`

Expected request concept:

```json
{
  "images": [
    {
      "uploadId": "123",
      "altText": "Product front view"
    },
    {
      "uploadId": "124",
      "altText": "Product side view"
    }
  ]
}
```

Behavior:

- Require `CatalogAdmin`.
- Require `If-Match`.
- Reuse the existing product ETag/version mechanism.
- Verify the product exists.
- Verify every upload session exists.
- Verify every session is Ready.
- Verify every session belongs to this product.
- Reject duplicate upload IDs.
- Enforce the maximum gallery size.
- Set Position sequentially starting at zero.
- The first image is the primary image.
- Persist gallery changes transactionally.
- Increment/update product version using the existing mechanism.
- Return the response shape defined in the current API contract.

Do not accept an arbitrary AssetKey from the client.

Do not allow one product to attach another product's upload session.

### 5.4. Public image delivery

Inspect the current public catalog contract and existing DTO projections.

The public catalog response already expects image URLs rather than exposing internal storage credentials.

Implement controlled image delivery while keeping Garage buckets private.

Preferred local-first implementation:

`GET /api/v1/media/{assetKey}`

The endpoint must:

- Be anonymous only if the image is confirmed to belong to a currently public/published product.
- Verify the asset is referenced by a public product gallery.
- Reject staging keys and arbitrary object keys.
- Read from the private `product-media` bucket through `IObjectStorage`.
- Stream the object to the HTTP response.
- Set the correct Content-Type.
- Set safe cache headers.
- Avoid buffering large files entirely in memory.
- Return 404 for unpublished, unreferenced, missing, or unauthorized assets.
- Never return credentials or internal Garage hostnames.
- Never permit access to `product-staging`.

If the repository already contains an approved CDN/reverse-proxy-compatible delivery layer, reuse it instead of creating a second delivery mechanism.

Do not persist expiring presigned URLs in the database.

Do not expose internal object keys in public DTOs unless the existing API contract explicitly requires them.

---

## 6. PRODUCT MEDIA CONFIGURATION

Add a dedicated options class in Product.API, following repository conventions.

Suggested location:

`src/Services/Product.API/Options/ProductMediaOptions.cs`

Suggested configuration:

```json
{
  "ProductMedia": {
    "StagingBucket": "product-staging",
    "MediaBucket": "product-media",
    "UploadUrlExpirationMinutes": 10,
    "DownloadUrlExpirationMinutes": 5,
    "MaxFileSizeBytes": 5242880,
    "MaxImagesPerProduct": 10,
    "PendingUploadExpirationMinutes": 30,
    "AllowedContentTypes": [
      "image/jpeg",
      "image/png",
      "image/webp"
    ]
  }
}
```

These are Product.API business configuration values.

Garage endpoint, region, access key, and secret key must continue to come from the Phase 3 `ObjectStorage:S3` configuration.

Requirements:

- Validate options on startup.
- Reject invalid bucket names and non-positive limits.
- Do not put credentials in this options class.
- Do not put secrets in tracked appsettings files.
- Preserve environment-specific configuration.
- Do not create buckets from application startup.

If existing configuration already provides these settings, reuse it instead of introducing duplicate options.

---

## 7. IMPLEMENT UPLOAD SESSION SERVICE

Create or extend the existing service layer.

Suggested location:

`src/Services/Product.API/Services/V1/ProductImageUploadService.cs`

Use existing repository and service abstractions.

Responsibilities:

1. Validate product existence.
2. Validate image content type.
3. Validate declared content length.
4. Enforce image-count constraints.
5. Generate a unique upload ID.
6. Generate a server-controlled staging key.
7. Persist Pending upload metadata.
8. Request a presigned upload URL from `IObjectStorage`.
9. Return upload URL, required headers, and expiration.

Validation:

- Only JPEG, PNG, and WebP are allowed in the initial implementation.
- Maximum declared size: 5 MiB.
- Content length must be greater than zero.
- Maximum gallery size: 10 images per product.
- Do not trust the client filename.
- Do not use the client filename as the object key.
- Do not permit client-supplied bucket names or object keys.

Use a cryptographically secure unique identifier or the repository's established ID generation mechanism.

Use cancellation tokens throughout.

If presigned URL generation fails, do not leave an unusable Pending record indefinitely. Handle the failure consistently with the existing transaction/repository approach.

Consider concurrent upload-session creation so two requests cannot bypass the per-product image limit.

---

## 8. IMPLEMENT BACKGROUND IMAGE VERIFICATION

Create a hosted background worker following existing repository conventions.

Suggested location:

`src/Services/Product.API/BackgroundServices/ProductImageUploadProcessor.cs`

Do not create a new worker framework if an existing background processing mechanism already exists.

### 8.1. Pending upload processing

The worker must:

1. Fetch a bounded batch of Pending upload sessions.
2. Ignore sessions that are already being processed by another worker instance.
3. Detect expired sessions.
4. Verify the staging object exists.
5. Read actual object metadata.
6. Compare actual size with ExpectedSize.
7. Verify actual content type.
8. Read the actual file bytes and validate the real image format.
9. Reject malformed or unsupported image data.
10. Generate a new immutable AssetKey.
11. Copy the staging object to `product-media`.
12. Verify the destination object exists.
13. Update the upload session to Ready and persist AssetKey.
14. Delete the staging object only after successful destination verification.

### 8.2. Image validation

Never trust:

- File extension.
- Client-provided MIME type.
- Client-provided content length.
- Custom metadata supplied by the client.

Validate the actual uploaded bytes.

Use an image decoding library only after checking:

- Existing package conventions.
- Compatibility with the repository target framework.
- License suitability.
- Whether an image decoder is already present.

If no suitable image decoder exists, propose the smallest appropriate dependency and explain the license/compatibility before adding it.

Do not implement image processing or thumbnail generation unless required by the existing PRD.

Protect against:

- Malformed image files.
- Truncated files.
- Extremely large decoded dimensions.
- Decompression bombs.
- Invalid content types.
- Empty files.

Avoid loading arbitrarily large files into memory.

### 8.3. Idempotency and concurrency

The worker must be safe to retry.

Handle these failure points:

- Before metadata verification.
- After metadata verification.
- After image validation.
- After destination copy.
- Before DB status update.
- After DB status update but before staging deletion.

Do not create multiple final objects for the same upload session on retry.

Use a deterministic or persisted AssetKey for the upload session once processing begins.

Use database concurrency protection or an atomic claim mechanism to prevent multiple workers from processing the same session concurrently.

If the application may run multiple instances, do not rely only on an in-memory lock.

### 8.4. Failure handling

For invalid image content:

- Mark Rejected.
- Store a safe internal rejection reason if the current schema supports it.
- Do not return storage exception details to clients.
- Clean staging data according to the safe cleanup policy.

For expired uploads:

- Mark Expired.
- Delete staging data only after confirming expiration and ownership of the staging key.

For transient Garage/network errors:

- Do not mark the image Rejected merely because storage is temporarily unavailable.
- Keep the session retryable.
- Use bounded retries/backoff.
- Log operation, upload ID, and correlation ID.
- Never log credentials or presigned URL query strings.

If the current database schema lacks a retry count or processing timestamp and these are necessary for reliable processing, report the proposed schema change in Gate A. Do not silently expand the schema.

---

## 9. GALLERY MANAGEMENT

Implement gallery replacement using the existing Product API domain rules.

Suggested service:

`src/Services/Product.API/Services/V1/ProductImageGalleryService.cs`

Reuse existing ProductImageRepository where possible.

Rules:

- Maximum 10 images per product.
- Position starts at zero.
- Position zero is the primary image.
- AltText is required or optional according to the current contract; preserve that exact rule.
- Every upload must be Ready.
- Every upload must belong to the target product.
- No duplicate upload IDs.
- No duplicate AssetKeys.
- A gallery update must respect product version concurrency.
- A failed DB transaction must not cause object deletion.
- An old asset must not be deleted while another gallery still references it.

Use the existing UnitOfWork/transaction implementation.

Do not bypass repository/service conventions by writing all business logic directly in the controller.

### Asset cleanup

After a successful gallery update:

- Identify assets removed from the gallery.
- Check whether each asset is referenced by any other gallery.
- Delete only assets with no remaining references.
- If deletion fails, log it and allow safe retry.
- Do not fail an already committed gallery update solely because an orphan cleanup operation failed.

If a durable cleanup queue/outbox already exists, reuse it where appropriate.

If no reliable cleanup mechanism exists, implement the smallest safe retryable approach and document its limitation.

Never delete objects based only on a client request.

---

## 10. PUBLIC IMAGE DELIVERY

Garage remains private.

Use the shared storage abstraction.

Do not use AWS SDK directly in Product.API.

Do not return an internal endpoint such as `http://garage:3900` to a browser.

Do not expose staging objects.

For the proxy delivery approach:

1. Resolve AssetKey against a published product gallery.
2. Return 404 if it is not publicly referenced.
3. Download/stream from the private media bucket.
4. Set Content-Type from verified metadata.
5. Set Content-Length when available.
6. Set safe cache headers.
7. Support cancellation when the client disconnects.
8. Avoid buffering entire objects in memory.
9. Do not dispose caller-owned streams incorrectly.
10. Do not expose internal storage errors.

The endpoint must not become an arbitrary object-storage proxy.

If public DTOs currently construct image URLs in repository projections, update the projection or mapping layer to use the controlled delivery route.

Preserve the existing public response contract.

---

## 11. DATABASE AND MIGRATIONS

Inspect current entity definitions and migrations first.

Do not create duplicate entities or tables.

Do not change existing column names or status numeric values without a proven requirement.

If a schema change is necessary:

- Explain why.
- Add a migration using the repository's existing EF Core migration process.
- Preserve existing data.
- Avoid destructive migration operations.
- Ensure the migration can be applied to a fresh local database.
- Ensure it can be applied safely to a database already created by previous migrations.

Do not execute a destructive migration against an existing user database without explicit approval.

---

## 12. DEPENDENCY INJECTION

In `Program.cs`, register:

- Phase 3 shared storage through its existing DI extension.
- ProductMediaOptions.
- Product image upload service.
- Product image gallery service.
- Any repository additions.
- Background worker.
- Delivery service/controller dependencies.

Use exact method names and namespaces from Phase 3.

Do not instantiate S3 clients manually in Product.API.

Do not register duplicate singleton S3 clients.

Do not register the same worker more than once.

Ensure the worker obtains scoped services using a proper scope per processing batch or item.

---

## 13. TESTING REQUIREMENTS

Follow the repository's existing test framework and conventions.

Do not introduce a second test framework unnecessarily.

### 13.1. Unit tests

Test:

- Product existence validation.
- Content type validation.
- File size validation.
- Image-count limit.
- Upload session creation.
- Staging key generation.
- Presigned URL generation.
- Required headers.
- Upload session ownership.
- Upload status transitions.
- Gallery validation.
- Duplicate upload ID rejection.
- Product version mismatch.
- Asset reference checking.
- Cleanup retry behavior.

### 13.2. Worker tests

Test:

- Missing staging object.
- Wrong object size.
- Wrong content type.
- Invalid image bytes.
- Valid image.
- Successful copy.
- Destination verification.
- Staging deletion.
- Retry after copy but before DB update.
- Retry after DB update but before staging deletion.
- Concurrent worker execution.
- Cancellation.
- Transient Garage errors.

### 13.3. API integration tests

Test:

- Admin authorization.
- Anonymous access denial for Admin endpoints.
- Upload session creation.
- Upload session status.
- Gallery replacement.
- Missing If-Match.
- Stale If-Match.
- Upload belonging to another product.
- Pending/Rejected/Expired upload.
- Public delivery of a published image.
- 404 for unpublished or unreferenced image.
- 404 for staging key.
- ProblemDetails response format.

### 13.4. Garage integration tests

Use a dedicated test bucket or explicitly approved test objects.

Do not use production-like Product buckets for destructive test cases.

Test:

1. Generate presigned PUT URL.
2. Upload a real image to staging.
3. Verify object metadata.
4. Process the upload.
5. Verify final object in media.
6. Verify staging cleanup.
7. Attach image to gallery.
8. Retrieve image through Product.API delivery.
9. Remove gallery reference.
10. Verify safe cleanup.

If Garage is unavailable or test credentials are absent, mark these tests SKIPPED or NOT RUN.

Never report an unexecuted integration test as PASS.

---

## 14. ERROR HANDLING AND SECURITY

Use the existing exception middleware and ProblemDetails conventions.

Expected behavior:

- Invalid request: 400.
- Unauthorized: 401.
- Forbidden: 403.
- Product/session/asset not found: 404.
- Business conflict: 409.
- Stale If-Match: 412.
- Missing required If-Match: 428.
- Temporary storage dependency failure: use the existing service-unavailable mapping, normally 503.
- Unexpected exception: 500 with generic detail and trace ID.

Never return:

- AWS access key.
- AWS secret key.
- Garage admin token.
- Internal Garage endpoint.
- Raw storage exception details.
- Presigned URL query strings in logs.
- Internal staging keys in public responses.

Validate all object keys on the server.

Use least privilege.

Do not add anonymous access to Garage.

Do not add CORS permissions to Garage unless the current direct-upload browser flow explicitly requires them and the change is approved. If browser direct upload requires CORS, report the exact allowed origin, methods, and headers before modifying Garage configuration.

---

## 15. FILE-BY-FILE IMPLEMENTATION PLAN

The following are expected target locations. Reuse existing files where appropriate and do not create duplicates.

### Existing files to inspect and likely modify

1. `src/Services/Product.API/Product.API.csproj`
   - Add project references to Phase 3 abstractions and S3 implementation if missing.
   - Do not add AWSSDK.S3 directly.

2. `src/Services/Product.API/Program.cs`
   - Register storage.
   - Register ProductMediaOptions.
   - Register upload/gallery services.
   - Register background worker.

3. `src/Services/Product.API/Persistence/ProductContext.cs`
   - Reuse existing DbSets and mappings.
   - Add only mappings required by a justified schema change.

4. `src/Services/Product.API/Repositories/ProductImageRepository.cs`
   - Add gallery replacement/query logic where appropriate.
   - Add public asset lookup.
   - Add reference-count/existence checks.

5. Existing product repository/service
   - Reuse product existence and version/ETag mechanisms.

6. Existing Admin Product controller
   - Extend only if this is the established controller for these routes.
   - Otherwise create a dedicated controller following existing V1 conventions.

7. Existing public catalog projection/mapping
   - Return controlled delivery URLs without exposing internal storage details.

### New files, if not already present

1. `src/Services/Product.API/Options/ProductMediaOptions.cs`

2. `src/Services/Product.API/Contracts/V1/CreateProductImageUploadRequest.cs`

3. `src/Services/Product.API/Contracts/V1/ProductImageUploadResponse.cs`

4. `src/Services/Product.API/Contracts/V1/ProductImageUploadStatusResponse.cs`

5. `src/Services/Product.API/Contracts/V1/ReplaceProductImagesRequest.cs`

6. `src/Services/Product.API/Contracts/V1/ProductImageItemRequest.cs`

7. `src/Services/Product.API/Services/V1/ProductImageUploadService.cs`

8. `src/Services/Product.API/Services/V1/ProductImageGalleryService.cs`

9. `src/Services/Product.API/BackgroundServices/ProductImageUploadProcessor.cs`

10. `src/Services/Product.API/Controllers/V1/ProductImageDeliveryController.cs`

11. Additional repository interface/implementation files only if required by existing architecture.

12. Validation classes following the existing FluentValidation convention.

13. Unit and integration tests following the existing test project layout.

14. Update:
   - `document/service/product-api/02-api-contract.md`
   - `document/service/product-api/03-processing-integrations.md`
   - `document/service/product-api/04-delivery-testing.md`

Do not create files that duplicate existing equivalents.

---

## 16. IMPLEMENTATION RULES — GATE B

Only begin implementation after explicit user approval of Gate A.

During implementation:

1. Re-read the current working tree before editing.
2. Preserve all unrelated user changes.
3. Implement in small logical steps.
4. Reuse existing abstractions and patterns.
5. Build after each major integration step.
6. Add tests before declaring completion.
7. Inspect the final diff.
8. Report all changed and created files.
9. Report any deviations from the approved plan.
10. Do not commit, push, or deploy.

Do not modify:

- Garage Docker Compose infrastructure.
- Garage bucket provisioning scripts.
- Garage credentials.
- Phase 3 shared library public contracts unless a backward-compatible change is explicitly approved.
- Other business services.
- Ocelot routes unless required and explicitly approved.
- Production deployment configuration.

Do not silently add unrelated features such as:
- Image resizing.
- Thumbnail generation.
- Image CDN provisioning.
- Video upload.
- General-purpose file management.
- New FileStorage microservice.
- Public Garage buckets.

---

## 17. ACCEPTANCE CRITERIA

Phase 4 is complete only when all applicable criteria pass.

### Architecture

- Product.API uses Phase 3 shared abstractions.
- Product.API does not depend directly on AWS SDK.
- No duplicate S3 client or storage abstraction exists.
- Product-specific logic remains inside Product.API.

### Upload

- Admin can create a valid upload session.
- Invalid content type and size are rejected.
- Upload URL points to the staging bucket.
- Client uploads directly to Garage.
- Credentials are never exposed.

### Verification

- Worker verifies actual object metadata and image bytes.
- Invalid uploads are rejected.
- Valid uploads are copied to media.
- Staging object is deleted only after successful final-object verification.
- Retry is idempotent.

### Gallery

- Only Ready uploads belonging to the same product can be attached.
- Gallery supports ordering.
- First image is primary.
- If-Match is enforced.
- Gallery update is transactional.
- Old assets are not deleted while referenced.

### Delivery

- Garage buckets remain private.
- Public catalog uses controlled delivery.
- Staging objects cannot be accessed publicly.
- Unpublished/unreferenced assets return 404.
- Public DTO does not expose storage credentials or internal Garage endpoints.

### Quality

- Solution builds successfully.
- Relevant unit tests pass.
- API integration tests pass.
- Garage integration tests pass or are explicitly marked NOT RUN/SKIPPED.
- Documentation reflects the actual implementation.
- No unrelated source files are modified.

---

## 18. FINAL REPORT

At the end, provide a Vietnamese implementation report containing:

1. Phase 4 completion status.
2. Architecture implemented.
3. Actual upload lifecycle.
4. API endpoints implemented.
5. Files created.
6. Files modified.
7. Database migrations created, if any.
8. Configuration required to run locally.
9. Garage bucket/key assumptions.
10. Unit test results.
11. API integration test results.
12. Garage integration test results.
13. Build commands and results.
14. Known limitations.
15. Deviations from the approved plan.
16. Remaining risks.
17. Manual steps needed to verify the complete flow.

Clearly distinguish PASS, FAIL, SKIPPED, and NOT RUN.

Do not claim completion if any mandatory acceptance criterion is unverified.

Stop after Phase 4. Do not start Phase 5 without explicit approval.