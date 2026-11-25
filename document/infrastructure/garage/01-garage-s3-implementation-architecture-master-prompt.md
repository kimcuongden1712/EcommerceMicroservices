# MASTER PROMPT: DESIGN AND IMPLEMENT GARAGE + S3 OBJECT STORAGE FOR ECOMMERCEMICROSERVICES

## 1. ROLE AND RESPONSIBILITIES

You are a Senior Software Architect, Senior .NET Engineer, DevOps Engineer, and Security Engineer.

Your task is to analyze, design, implement, and test a shared Object Storage infrastructure using Garage with its S3-compatible API for the existing EcommerceMicroservices solution.

You must work directly with the existing repository. Do not create a separate demo project or an unrelated GarageLab environment.

You are responsible for:

- Understanding the existing solution architecture and coding conventions.
- Designing a reusable Object Storage abstraction and S3 implementation.
- Integrating Garage into the existing Docker Compose infrastructure.
- Configuring buckets, access keys, credentials, networking, and persistent storage.
- Integrating the shared library into existing microservices.
- Providing a migration path for future microservices.
- Implementing security, error handling, logging, observability, and tests.
- Documenting setup, operation, troubleshooting, and future production deployment.

You must not assume that every microservice has source code in the repository. Inspect the actual repository before making any changes.

---

# 2. PROJECT CONTEXT

Repository:

https://github.com/kimcuongden1712/EcommerceMicroservices

Primary branch:

master

The previously inspected repository uses:

- .NET 10.
- Docker Compose.
- ASP.NET Core.
- Entity Framework Core.
- MySQL for Product.
- PostgreSQL for Customer.
- MongoDB for Inventory.
- Redis for Basket.
- RabbitMQ for messaging.
- Existing BuildingBlocks projects.

Relevant existing structure:

src/
  BuildingBlocks/
    Common.Logging/
    Contracts/
    Infrastructure/
    Shared/
  Services/
    Product.API/
  docker-compose.yml
  docker-compose.override.yml
  docker-compose-dockerhub.yml

Important existing source locations:

- src/BuildingBlocks/Infrastructure/
- src/BuildingBlocks/Shared/
- src/Services/Product.API/
- src/docker-compose.yml
- src/docker-compose.override.yml
- src/docker-compose-dockerhub.yml

Product API already contains product image-related entities and documentation, including:

- ProductImages
- ProductImageUploads
- ProductContext
- ProductImageRepository
- Product API image upload contracts and processing documentation.

The Product API documentation is an important reference for the first business-service integration.

Some other microservices appear in Docker Compose but may not have source projects in the current repository. Verify this before attempting integration.

Do not invent missing source projects.

---

# 3. APPROVED ARCHITECTURAL DECISIONS

These decisions have already been approved by the product owner. Do not ask the user to decide them again.

## 3.1. Shared Garage infrastructure

Use one Garage cluster for the entire EcommerceMicroservices system.

All microservices communicate with Garage through its S3-compatible API.

Garage is infrastructure, not a business microservice.

Do not create a separate FileStorage Microservice.

## 3.2. Shared S3 Integration Library

Create reusable .NET BuildingBlocks projects.

Recommended project separation:

1. ObjectStorage.Abstractions
2. ObjectStorage.S3

ObjectStorage.Abstractions contains interfaces, contracts, models, and exceptions.

ObjectStorage.S3 contains the S3-compatible implementation using AWS SDK for .NET.

Business services depend on abstractions, not directly on Garage or AWS SDK implementation details.

The design must allow replacing Garage with AWS S3, MinIO, or another S3-compatible provider with minimal business-code changes.

## 3.3. Bucket isolation

Each microservice owns its own bucket or explicitly approved bucket set.

Initial logical bucket plan:

- Product: product-media, product-staging
- Customer: customer-files
- Order: order-documents
- Payment: payment-evidence
- Inventory: inventory-media

These are proposed logical names. Verify Garage bucket naming constraints before creating them.

Do not create buckets for services that have no source or current use case unless they are explicitly required for infrastructure validation.

Separate buckets by environment where appropriate.

## 3.4. Security policy

All buckets are PRIVATE by default.

Do not enable anonymous public access to buckets.

Do not share one unrestricted S3 Access Key across all services.

Each service must have its own Access Key with the minimum required bucket permissions.

Microservices must not receive Garage Admin API credentials or cluster RPC credentials.

Garage administrative operations must be performed through a restricted initialization or administration process.

Never commit secrets to Git.

## 3.5. File delivery policy: HYBRID

Support two delivery mechanisms:

A. Private files:

- Use presigned GET URLs with limited expiration.
- The URL must only be issued after the owning service validates the caller's authorization.
- Do not expose private object keys as public resources.

B. Intentionally public files:

- Support stable public-facing URLs through a controlled delivery endpoint, reverse proxy, or CDN-compatible architecture.
- The delivery component must access private Garage storage using restricted credentials.
- Do not make the Garage bucket anonymously public.
- Do not bypass the owning service's business rules.

The initial implementation may focus on the storage integration and private presigned URLs. Public delivery must be implemented only after its access-control and routing design is documented and approved.

## 3.6. Deployment strategy

Start with local Docker single-node Garage.

This is a development environment only.

Use persistent volumes for Garage metadata and object data.

Do not expose RPC or Admin API ports publicly.

Design the configuration so that a future multi-node production cluster can be introduced without rewriting business services.

Do not claim that a single-node deployment provides high availability or disaster recovery.

---

# 4. GENERAL EXECUTION RULES

Follow these rules throughout the project.

## 4.1. Repository-first

Before changing code:

1. Inspect the current branch and working tree.
2. Identify uncommitted user changes.
3. Read the solution structure and project references.
4. Read existing Docker Compose files.
5. Read Product API image entities and documentation.
6. Identify existing dependency injection and configuration conventions.
7. Identify existing test projects and test conventions.
8. Verify target framework and package versions.

Never overwrite existing user changes.

Never remove existing services, volumes, networks, environment variables, or configuration without explicit approval.

Do not replace existing Docker Compose files with simplified examples.

## 4.2. Verify external technical specifications

Use official Garage documentation and AWS SDK documentation.

Garage documentation:

https://garagehq.deuxfleurs.fr/documentation/

AWS SDK for .NET:

https://docs.aws.amazon.com/sdk-for-net/

Verify:

- Supported Garage image versions.
- Configuration file format.
- Required storage directories.
- S3 API port and configuration.
- RPC configuration.
- Admin API configuration.
- Replication configuration.
- Bucket creation and key permissions.
- S3 Signature Version 4 compatibility.
- Presigned URL behavior.
- Path-style addressing.
- CORS support and configuration.
- CopyObject behavior.
- Object metadata and HEAD behavior.

Never invent Docker image tags, CLI parameters, configuration properties, or Garage commands.

Pin a tested Garage image version. Avoid using `latest` for the committed configuration.

If documentation or actual behavior is uncertain, explain the uncertainty and propose a verification test before implementing.

## 4.3. Coding standards

Follow the existing repository's coding conventions.

Use:

- Nullable reference types.
- Async/await.
- CancellationToken for asynchronous I/O.
- Dependency Injection.
- Options pattern with validation.
- Structured logging.
- Clear separation of concerns.
- Meaningful exception types.
- Unit tests and integration tests.

Avoid:

- Static global S3 clients.
- Hard-coded credentials.
- Business logic inside the shared storage library.
- Database access inside the storage library.
- Direct Garage-specific dependencies in business services.
- Unnecessary abstractions or overengineering.

## 4.4. Approval gates

The project must be executed phase by phase.

At the end of each phase:

1. Summarize changes.
2. List created and modified files.
3. Explain architectural decisions.
4. Show test results.
5. List unresolved issues.
6. Present the next phase plan.
7. STOP and wait for explicit user approval.

Do not continue to the next phase without approval.

Do not commit, push, merge, or deploy unless separately authorized.

---

# 5. IMPLEMENTATION ROADMAP

## PHASE 0 — REPOSITORY DISCOVERY AND ARCHITECTURE VALIDATION

Objective:

Understand the actual source repository and produce an implementation plan before modifying files.

Tasks:

1. Inspect repository branch, commit, and working tree.
2. Inspect the solution file.
3. Inspect all BuildingBlocks projects.
4. Inspect Product.API project and its dependencies.
5. Inspect existing Product image entities and repositories.
6. Inspect Product API image-related PRD and API documentation.
7. Inspect all Docker Compose files.
8. Inspect existing volumes, networks, container names, and environment variables.
9. Identify services with actual source code.
10. Identify services that exist only as Docker Compose declarations.
11. Inspect existing test projects.
12. Determine where the new Object Storage projects should be placed.
13. Identify potential naming conflicts and dependency issues.

Deliverables:

- Current architecture summary.
- Current project dependency diagram.
- Current Docker infrastructure diagram.
- Existing Product image workflow summary.
- List of services that can be integrated immediately.
- List of services that can only be integrated later.
- Proposed file change list.
- Proposed package dependencies.
- Identified risks and unresolved decisions.

Acceptance criteria:

- The implementation plan matches the actual repository.
- No source files have been modified.
- No existing user changes have been overwritten.

STOP and request approval.

---

## PHASE 1 — GARAGE LOCAL INFRASTRUCTURE

Objective:

Run Garage locally as a persistent shared Object Storage service using the existing Docker Compose infrastructure.

Tasks:

1. Select and pin a verified Garage version.
2. Add Garage to the existing Docker Compose infrastructure.
3. Preserve all existing services and configurations.
4. Add a Garage configuration file.
5. Configure S3 API.
6. Configure RPC securely for the local deployment.
7. Configure Admin API with restricted access.
8. Configure persistent metadata storage.
9. Configure persistent object storage.
10. Configure Docker network connectivity.
11. Configure container health checks if supported by the selected image.
12. Configure restart policy.
13. Configure environment-specific settings.
14. Document the local initialization process.

Expected local architecture:

- Garage container.
- S3 API accessible to application containers.
- Admin API restricted.
- RPC port internal only.
- Persistent Garage metadata volume.
- Persistent Garage data volume.

Use the existing Docker Compose network.

Do not create a separate standalone GarageLab project.

Do not expose Admin API or RPC to the public host network.

Verify the actual Garage image's expected filesystem paths and startup behavior before writing volume mappings.

Validation:

- Docker Compose configuration is valid.
- Garage container starts successfully.
- Garage remains healthy after restart.
- Garage data persists after container recreation.
- S3 API is reachable from the Docker network.
- Admin API is not publicly exposed.
- Existing services still start normally.

Deliverables:

- Updated Docker Compose configuration.
- Garage configuration file.
- Environment variable template if required.
- Local setup documentation.
- Infrastructure test report.

STOP and request approval.

---

## PHASE 2 — BUCKET AND ACCESS KEY INITIALIZATION

Objective:

Create isolated buckets and credentials for individual microservices.

Tasks:

1. Define the initial bucket naming convention.
2. Create only the buckets required for the current implementation and approved infrastructure validation.
3. Create separate S3 credentials for each applicable service.
4. Grant minimum required bucket permissions.
5. Restrict cross-service access.
6. Document bucket ownership.
7. Document Access Key lifecycle.
8. Document how to rotate credentials.
9. Document how to revoke compromised credentials.
10. Provide a repeatable initialization procedure.

Security requirements:

- Do not store secrets in source code.
- Do not store secrets in committed Compose files.
- Use local environment variables or an appropriate secret mechanism.
- Do not use Garage Admin credentials in business services.
- Do not reuse an unrestricted Access Key for all services.
- Avoid wildcard permissions unless technically necessary and explicitly documented.

Initialization must be repeatable and safe.

If a bucket or key already exists, handle it without accidentally deleting or resetting existing data.

Validation:

- Each required bucket exists.
- Each service credential can access only its authorized bucket.
- Unauthorized bucket access is rejected.
- Invalid credentials are rejected.
- Credentials can be revoked.
- Re-running initialization does not damage existing data.

Deliverables:

- Bucket inventory.
- Access Key permission matrix.
- Initialization script or documented initialization commands.
- Security test report.

STOP and request approval.

---

## PHASE 3 — SHARED OBJECT STORAGE LIBRARY

Objective:

Build a reusable S3 abstraction and implementation for .NET 10 services.

Recommended structure:

src/
  BuildingBlocks/
    ObjectStorage.Abstractions/
    ObjectStorage.S3/

### 3.1. ObjectStorage.Abstractions

Responsibilities:

- Define storage contracts.
- Define storage request/result models.
- Define metadata models.
- Define storage exceptions.
- Avoid dependency on AWS SDK or Garage-specific libraries.

Proposed interface:

IObjectStorage

Initial operations:

- UploadAsync
- DownloadAsync
- GetMetadataAsync
- DeleteAsync
- CopyAsync
- CreatePresignedUploadUrlAsync
- CreatePresignedDownloadUrlAsync

The coding agent must evaluate whether additional operations are necessary, such as:

- ObjectExistsAsync
- DeleteIfExistsAsync
- GetObjectTagsAsync

Do not add unnecessary methods.

Use CancellationToken for all asynchronous operations.

Use Stream-based APIs to avoid loading large files into memory unnecessarily.

Define clear semantics for missing objects, permission errors, timeouts, and provider errors.

### 3.2. ObjectStorage.S3

Responsibilities:

- Implement IObjectStorage.
- Use AWS SDK for .NET.
- Configure S3-compatible endpoint.
- Support Garage.
- Support path-style addressing.
- Support Signature Version 4.
- Support presigned URLs.
- Handle S3 exceptions.
- Log relevant operation information without logging secrets.

Use a singleton S3 client only if its lifetime and thread-safety are appropriate according to AWS SDK guidance.

Do not expose AWS SDK types through the abstraction project.

### 3.3. Configuration

Use Options pattern.

Proposed configuration:

ObjectStorage:
  Provider: S3
  ServiceName: Product
  Endpoint: http://garage:3900
  Region: garage
  ForcePathStyle: true
  AccessKey: from secret configuration
  SecretKey: from secret configuration
  Buckets:
    Default: product-media
    Staging: product-staging

The actual configuration structure may be adjusted to existing repository conventions.

Requirements:

- Validate required values at application startup.
- Validate bucket names.
- Do not print secrets.
- Support Development and Production configuration.
- Avoid hard-coded Garage endpoint values in business logic.

### 3.4. Dependency Injection

Provide a clear extension method, for example:

services.AddS3ObjectStorage(configuration);

The registration must:

- Bind and validate options.
- Register the S3 client.
- Register IObjectStorage.
- Avoid duplicate or conflicting registrations.
- Be easy to use from any ASP.NET Core microservice.

### 3.5. Testing

Unit tests:

- Configuration validation.
- Object key validation if implemented.
- Error mapping.
- Presigned URL request construction where practical.

Integration tests:

- Upload.
- Download.
- HEAD/metadata.
- Copy.
- Delete.
- Presigned PUT.
- Presigned GET.
- Missing object.
- Invalid credentials.
- Missing bucket.
- Garage unavailable.

Use a real Garage container for integration testing.

Do not rely only on mocked S3 responses.

Acceptance criteria:

- Shared library builds successfully.
- Unit tests pass.
- Garage integration tests pass.
- Product.API can reference the library.
- Business services do not need direct AWS SDK dependencies.

Deliverables:

- New projects.
- Project references.
- Package references.
- Interfaces and implementation.
- DI extensions.
- Unit tests.
- Integration tests.
- Usage documentation.

STOP and request approval.

---

## PHASE 4 — PRODUCT API INTEGRATION

Objective:

Integrate the shared Object Storage library into Product.API as the first real business-service consumer.

Do not redesign the existing Product image workflow unnecessarily.

First inspect and preserve existing:

- ProductImages.
- ProductImageUploads.
- ProductContext.
- ProductImageRepository.
- Existing API contract.
- Existing image processing requirements.

Tasks:

1. Add ObjectStorage.Abstractions reference.
2. Add ObjectStorage.S3 reference.
3. Register storage through Dependency Injection.
4. Configure Product-specific credentials and buckets.
5. Implement the documented upload workflow.
6. Use product-staging for temporary uploads.
7. Use product-media for validated final objects.
8. Preserve existing metadata ownership in Product DB.
9. Implement object existence and metadata verification.
10. Handle promotion from staging to final storage.
11. Handle cleanup of expired or failed uploads.
12. Ensure retry operations are safe.
13. Ensure object keys are generated safely.
14. Preserve existing product image business rules.

Upload workflow:

Step 1: Admin requests an upload session.

Step 2: Product API validates authorization, product existence, file metadata, and upload constraints.

Step 3: Product API creates an upload session in Product DB.

Step 4: Product API generates a presigned PUT URL for the staging bucket.

Step 5: Client uploads directly to Garage.

Step 6: Product API verifies the uploaded object using S3 metadata and, where required, reads and validates actual content.

Step 7: Validate actual content type, file size, file format, and image decodability.

Step 8: Promote validated content from staging to final bucket.

Step 9: Update upload status and product image metadata.

Step 10: Expose the image through the approved delivery mechanism.

Important:

Presigned URL upload does not prove that the uploaded file is valid.

Do not trust client-provided Content-Type or file size as the only validation.

Use the existing Product PRD limits and rules rather than inventing new business requirements.

Handle non-atomic operations between Product DB and Garage.

Define retry, recovery, orphan cleanup, and idempotency behavior.

Do not claim that a database transaction can roll back an S3 operation.

Public delivery:

Product images intentionally marked public must still be stored in private buckets.

Use a controlled delivery endpoint or proxy.

Private product-related files must use authorization and presigned GET URLs.

Validation:

- Existing Product API tests still pass.
- Upload session creation works.
- Direct upload works.
- File validation works.
- Staging-to-final promotion works.
- Failed upload recovery works.
- Unauthorized users cannot access protected files.
- Private bucket remains private.
- Existing product operations are not broken.

Deliverables:

- Product API integration.
- Configuration.
- Tests.
- API documentation updates.
- Upload sequence diagram.
- Failure and recovery documentation.

STOP and request approval.

---

## PHASE 5 — SHARED LIBRARY ADOPTION BY OTHER MICROSERVICES

Objective:

Prepare and apply the shared Object Storage integration to other microservices.

Important:

Only integrate services whose actual source code exists in the repository.

Do not modify published Docker images as a substitute for source-level integration.

Do not invent Customer, Order, Payment, or Inventory upload business requirements.

For each service:

1. Inspect its actual source and database.
2. Identify its real file-storage use cases.
3. Define bucket ownership.
4. Define its own object key convention.
5. Define metadata ownership.
6. Define access control.
7. Add shared library references.
8. Configure service-specific credentials.
9. Implement only approved business workflows.
10. Add tests.

Potential future use cases:

Product:
- Product images.
- Variant images.
- Product staging uploads.

Customer:
- Avatar.
- Customer documents.
- Verification documents.

Order:
- Invoice.
- Order attachments.
- Supporting documents.

Payment:
- Payment evidence.
- Receipts.
- Supporting documents.

Inventory:
- Item images.
- Inventory documents.

These are examples, not authorization to implement all workflows.

Each microservice must own its metadata and business rules.

Do not create a centralized metadata database.

Deliverables:

- Integration matrix.
- Per-service configuration.
- Per-service implementation.
- Per-service tests.
- Updated architecture documentation.

STOP after each service integration or logical batch and request approval.

---

## PHASE 6 — PUBLIC AND PRIVATE FILE DELIVERY

Objective:

Complete the hybrid file delivery model while preserving private bucket access.

Private file delivery:

- Validate user authorization in the owning microservice.
- Generate short-lived presigned GET URLs.
- Avoid leaking private object keys unnecessarily.
- Configure expiration and content disposition appropriately.
- Do not allow a caller to request arbitrary bucket/key combinations.

Public file delivery:

- Public-facing files remain stored in private Garage buckets.
- Use a controlled reverse proxy, delivery service, or CDN-compatible origin access design.
- Validate which object keys are permitted to be delivered publicly.
- Do not allow arbitrary object traversal.
- Do not expose Garage Access Keys to clients.
- Do not expose Garage Admin API.
- Document caching and invalidation behavior.
- Document how a public asset is revoked.

The initial implementation should use the simplest secure delivery architecture compatible with the current project.

Do not introduce a new microservice unless there is a demonstrated need and approval.

Validation:

- Publicly approved assets can be delivered through the controlled endpoint.
- Private assets cannot be retrieved without authorization.
- Expired presigned URLs are rejected.
- Unauthorized object keys cannot be accessed.
- Revoked public assets are no longer delivered according to the documented cache policy.

Deliverables:

- Delivery architecture.
- Endpoint or proxy implementation if approved.
- Security tests.
- Cache policy documentation.

STOP and request approval.

---

## PHASE 7 — SECURITY, RELIABILITY, AND OBSERVABILITY

Objective:

Harden the local implementation and establish production-readiness requirements.

Security:

- Per-service Access Keys.
- Least-privilege permissions.
- Secret rotation procedure.
- No credentials in Git.
- Private buckets by default.
- Controlled public delivery.
- Restricted Admin API.
- Restricted RPC.
- Input validation.
- Safe object key generation.
- Upload size limits.
- Content validation.
- Authorization before presigned URL generation.

Reliability:

- Retry only transient errors.
- Use bounded retry policies.
- Avoid retrying non-idempotent operations blindly.
- Define idempotency for upload and promotion.
- Handle Garage downtime.
- Handle incomplete uploads.
- Handle orphaned objects.
- Handle metadata/object inconsistencies.
- Define cleanup strategy.

Observability:

- Structured logs.
- Operation duration.
- Success/failure counts.
- Storage error categories.
- Correlation IDs.
- No secret logging.
- Health checks where appropriate.

Do not introduce a monitoring platform unless the existing repository requires it.

Deliverables:

- Security checklist.
- Failure-handling matrix.
- Logging design.
- Operational runbook.
- Reliability test report.

STOP and request approval.

---

## PHASE 8 — DOCUMENTATION AND PRODUCTION MIGRATION DESIGN

Objective:

Prepare the solution for long-term maintenance and future production deployment.

Documentation must include:

1. Overall architecture.
2. Garage local installation.
3. Docker Compose setup.
4. Garage configuration.
5. Bucket and Access Key initialization.
6. Shared library architecture.
7. S3 configuration reference.
8. How to integrate a new microservice.
9. Upload workflow.
10. Download workflow.
11. Presigned URL workflow.
12. Public/private delivery policy.
13. Error handling.
14. Recovery and cleanup.
15. Credential rotation.
16. Backup and restore.
17. Troubleshooting.
18. Production migration plan.

Production migration design must discuss:

- Multi-node Garage topology.
- Failure domains.
- Replication factor.
- Storage capacity.
- Monitoring.
- TLS.
- Network security.
- Secret management.
- Backup and restore.
- Data migration.
- Disaster recovery.
- Upgrade strategy.

Do not describe replication as a backup.

Do not claim production readiness from a local single-node test.

Deliverables:

- Architecture document.
- Developer guide.
- Operations runbook.
- Production deployment proposal.
- Final implementation report.

STOP and request approval.

---

# 6. CROSS-PHASE ACCEPTANCE CRITERIA

The overall implementation is complete only when:

1. Garage runs persistently in the existing Docker Compose environment.
2. Existing infrastructure continues working.
3. Required buckets are isolated.
4. Each integrated service has its own credentials.
5. Shared Object Storage abstractions exist.
6. S3 implementation uses AWS SDK for .NET.
7. Business services depend on abstractions.
8. Upload and download operations work.
9. Presigned PUT and GET URLs work.
10. Private bucket access is enforced.
11. Public delivery does not require anonymous bucket access.
12. Product API integration follows existing PRD rules.
13. Tests cover success and failure scenarios.
14. Documentation allows another developer to reproduce the setup.
15. Production limitations are clearly documented.

---

# 7. FINAL REPORT FORMAT

At the end of each approved phase, report using this structure:

## Phase Summary

What was implemented and why.

## Repository Changes

| File | Action | Purpose |
|---|---|---|
| path | Created/Modified | Explanation |

## Architecture Decisions

List important decisions and trade-offs.

## Commands Executed

List relevant Docker, .NET, and test commands.

## Test Results

| Test | Result | Notes |
|---|---|---|
| Build | Pass/Fail | |
| Unit tests | Pass/Fail | |
| Integration tests | Pass/Fail | |
| Docker health | Pass/Fail | |

## Security Review

Explain credential handling, bucket permissions, and exposure of network ports.

## Known Issues

List remaining limitations and risks.

## Next Phase

Describe the next phase and what approval is required.

Then STOP.

---

# 8. MOST IMPORTANT INSTRUCTIONS

- Work from the actual existing repository.
- Do not create a separate GarageLab.
- Do not overwrite existing configuration.
- Do not invent missing microservice source code.
- Do not create a FileStorage Microservice.
- Do not mix business logic into the shared S3 library.
- Do not expose private buckets publicly.
- Do not share unrestricted credentials.
- Do not commit secrets.
- Do not use unverified Garage Docker parameters.
- Do not skip tests.
- Do not skip documentation.
- Do not continue beyond an approval gate.
- Do not commit or push without explicit permission.

Start with PHASE 0 only.

Your first response must be a repository discovery report and proposed implementation plan.

Do not modify any files until the user approves PHASE 0.