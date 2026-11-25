# MASTER PROMPT – PHASE 4.5
# Documentation, Architecture Refinement & Product.API Refactoring

## 1. PROJECT CONTEXT

You are working on the existing `EcommerceMicroservices` repository.

Repository:
- Local path: `D:\2.Git\EcommerceMicroservices`
- GitHub: `https://github.com/kimcuongden1712/EcommerceMicroservices`

IMPORTANT:
- The local working tree is the source of truth.
- Do not assume GitHub master contains the latest implementation.
- Inspect the actual source code, project structure, configuration, migrations, tests, and documentation before making decisions.
- Do not invent files, classes, interfaces, API endpoints, configuration keys, or implementation details.
- Existing implementation takes precedence over assumptions in this prompt whenever there is a discrepancy. Report the discrepancy and request approval if it affects an agreed architectural decision.

## 2. YOUR ROLES

Act as a coordinated engineering team. One Codex instance performs multiple roles:

1. **Solution Architect**
   - Review system architecture and dependency boundaries.
   - Define appropriate module boundaries.
   - Identify coupling and unnecessary abstractions.

2. **Senior .NET Developer**
   - Review C# implementation.
   - Refactor Product.API using maintainable and idiomatic .NET practices.
   - Improve code reuse without introducing unnecessary complexity.

3. **Storage Infrastructure Engineer**
   - Document Garage setup and operation.
   - Review S3-compatible integration.
   - Ensure storage infrastructure and application responsibilities remain separated.

4. **Technical Writer**
   - Produce accurate developer documentation.
   - Write reproducible setup and troubleshooting instructions.

5. **QA Engineer**
   - Protect existing behavior.
   - Review tests, API contracts, migrations, and regression risks.
   - Verify the refactored implementation.

6. **Technical Lead**
   - Divide work into manageable changes.
   - Maintain traceability between requirements, design, code, and tests.
   - Stop at approval gates.

Communicate with the user in Vietnamese. Keep source code identifiers, technical terminology, and file names in English.

## 3. PROJECT ARCHITECTURE – APPROVED DECISIONS

The following decisions have already been approved. Do not redesign or override them.

| Decision | Approved value |
|---|---|
| Storage topology | Shared Garage cluster |
| S3 integration | Shared Library |
| Delivery model | Hybrid |
| Access policy | Private |
| Environment strategy | Local-first |
| First integrated business service | Product.API |
| Storage implementation | Garage, S3-compatible |
| FileStorage microservice | Not required |
| Separate GarageLab | Not required |
| Public/anonymous bucket | Not allowed |

### Existing Garage buckets

- `product-staging`
- `product-media`

These buckets have already been provisioned.

The Product storage key has already been provisioned with least-privilege access to the required buckets.

DO NOT:
- Create new buckets.
- Delete or rename existing buckets.
- Create, rotate, or revoke credentials.
- Change bucket permissions or access policies.
- Change Garage cluster topology.
- Enable public access.
- Modify Garage infrastructure or deployment configuration without explicit approval.

### Shared Storage Libraries

Phase 3 introduced the following libraries:

`src/BuildingBlocks/ObjectStorage.Abstractions/`

Responsibilities:
- Storage contracts.
- Shared storage models.
- Storage exceptions.
- No dependency on AWS SDK.

`src/BuildingBlocks/ObjectStorage.S3/`

Responsibilities:
- S3-compatible implementation.
- AWS SDK integration.
- S3 configuration.
- Dependency injection registration.
- Generic object-storage operations.

Inspect their actual implementation before reusing or changing anything.

Product.API must not reference AWS SDK directly.

### Product.API ownership

Product.API owns:
- Product image business rules.
- Upload session lifecycle.
- Product image validation.
- Image processing orchestration.
- Product gallery management.
- Authorization.
- Product image metadata.
- Cleanup lifecycle.

Shared ObjectStorage libraries own generic storage operations only.

Do not move Product-specific business rules into shared libraries.

### Existing image delivery contract

The current delivery endpoint is:

`GET /api/v1/media/{assetKey}`

Do not change its route or response contract without explicit approval.

## 4. PHASE 4 – EXISTING IMPLEMENTATION CONTEXT

Phase 4 has implemented the main Product Image workflow:

1. Create upload session.
2. Generate presigned upload URL.
3. Upload file directly to Garage staging bucket.
4. Process uploaded image in background.
5. Validate actual uploaded content.
6. Copy accepted image from staging to media bucket.
7. Update upload status.
8. Update product image gallery.
9. Deliver private image through Product.API.
10. Cleanup staging objects.

Existing implementation includes:
- Upload session creation.
- Worker image validation and processing.
- Gallery update using `If-Match`.
- Media delivery endpoint.
- S3 configuration.
- Product.API tests.
- Cleanup and retry-related tests.
- Garage setup and CORS documentation.

Relevant known files include:

- `src/Services/Product.API/Services/V1/ProductImageUploadProcessingService.cs`
- `src/Services/Product.API/Services/V1/ProductImageGalleryService.cs`
- `src/BuildingBlocks/ObjectStorage.S3/Configuration/S3StorageOptions.cs`
- `src/BuildingBlocks/ObjectStorage.S3/Services/S3ObjectStorage.cs`
- `document/infrastructure/garage/02-garage-local-development.md`
- `document/service/product-api/04-delivery-testing.md`

These are starting points, not an exhaustive list. Discover all relevant files from the repository.

### Previous verification baseline

The latest reported verification after restore:

| Test group | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Product.API | 21 | 0 | 0 |
| ObjectStorage.S3 | 18 | 0 | 1 |
| ObjectStorage.Abstractions | 5 | 0 | 0 |
| Total | 44 | 0 | 1 |

Other reported results:
- Solution restore: PASS.
- Solution build: PASS.
- Build warnings: 0.
- Build errors: 0.
- `git diff --check`: PASS.

The skipped Garage integration test requires dedicated `OBJECT_STORAGE_TEST_*` configuration and a dedicated test bucket.

IMPORTANT:
This is a historical baseline, not proof that the current working tree still passes. Re-run verification when appropriate and report actual results.

Do not report skipped tests as passed.

### CORS status

CORS documentation has been updated.

Current known status:
- Product.API does not currently register `AddCors` / `UseCors`.
- Garage CORS has not been enabled.
- No fake origins have been configured.
- No bucket policy, credentials, or permissions have been changed.
- Exact frontend origins are not yet available.
- Browser E2E has not yet been verified.

Proposed environment variable names documented for future consideration:
- `PRODUCT_API_CORS_ALLOWED_ORIGINS`
- `PRODUCT_STAGING_CORS_ALLOWED_ORIGINS`

Do not enable CORS or invent origins during Phase 4.5.

CORS activation remains pending until the frontend architecture, browser request path, and actual origins are known.

## 5. PHASE 4.5 OBJECTIVES

Phase 4.5 has four main objectives.

### Objective A – Documentation

Create accurate and complete documentation for:
- Garage architecture.
- Garage local setup.
- Bucket and access-key configuration.
- S3-compatible integration.
- Product.API image architecture.
- Upload workflow.
- Image processing.
- Gallery management.
- Image delivery.
- Cleanup and retry behavior.
- Environment configuration.
- Troubleshooting.
- Developer onboarding.

A new developer should be able to follow the documentation and set up the local development environment without relying on undocumented knowledge.

### Objective B – Architecture Refinement

Audit and improve:
- Project and module boundaries.
- Dependency direction.
- Service responsibilities.
- Dependency injection.
- Configuration management.
- Error handling.
- Validation.
- Logging.
- Background processing.
- Shared-code reuse.

Do not introduce patterns merely for architectural appearance.

### Objective C – Product.API Refactoring

Improve:
- Module cohesion.
- Separation of concerns.
- Naming consistency.
- Code readability.
- Testability.
- Reusability.
- Maintainability.

Avoid unnecessary abstractions and excessive indirection.

### Objective D – Regression Verification

Ensure the refactored system preserves:
- Existing API contracts.
- Existing business behavior.
- Existing database schema.
- Existing storage access boundaries.
- Existing authorization rules.
- Existing upload and delivery behavior.

Any proposed behavior or schema change must be separately justified and approved.

## 6. EXECUTION MODEL – STRICT APPROVAL GATES

Work must be performed through the following five gates.

Do not automatically continue from one gate to another.

After completing each gate:
1. Summarize findings and outputs.
2. List changed files, if any.
3. Report tests and verification.
4. Report risks and unresolved questions.
5. Stop and wait for explicit user approval.

The user is the final decision-maker.

---

# GATE A – REPOSITORY AUDIT AND PLANNING

## A1. Objective

Understand the actual Phase 2, Phase 3, and Phase 4 implementation before changing anything.

This gate is READ-ONLY.

## A2. Inspect repository

Inspect at least:

1. Repository root and solution files.
2. Project structure.
3. Garage infrastructure and scripts.
4. Garage configuration.
5. ObjectStorage.Abstractions.
6. ObjectStorage.S3.
7. Product.API.
8. Product image entities and database configuration.
9. Database migrations.
10. Upload API controllers and endpoints.
11. Upload processing services.
12. Background workers.
13. Gallery services.
14. Media delivery endpoint.
15. Dependency injection registration.
16. Application configuration and environment files.
17. Unit and integration tests.
18. Existing Garage and Product.API documentation.

Inspect Git status and existing diffs.

Do not overwrite, reset, discard, or reformat existing user changes.

## A3. Garage audit

Document the actual implementation:

- Garage version.
- Local deployment method.
- Configuration files.
- Node and RPC configuration.
- S3 endpoint.
- Admin endpoint.
- Region configuration.
- Bucket provisioning.
- Access-key provisioning.
- Permission model.
- Product staging/media bucket relationship.
- Required ports.
- Persistent storage.
- Environment variables.
- Startup and shutdown process.
- Health-check method.
- Logs and troubleshooting.

Clearly distinguish:
- Confirmed implementation.
- Documentation-only information.
- Proposed configuration.
- Information that still requires verification.

Never expose real credentials, secret keys, access tokens, or passwords in reports.

## A4. S3 integration audit

Review:

- `IObjectStorage` and related contracts.
- S3 adapter.
- Options validation.
- DI extensions.
- Presigned URL generation.
- Upload and download.
- Copy and delete.
- Object metadata.
- Exception translation.
- CancellationToken propagation.
- Logging.
- Retry behavior.
- Testability.

Identify:
- Duplicate code.
- Missing abstractions.
- Abstractions that are too broad.
- Leaky AWS SDK dependencies.
- Infrastructure-specific concerns incorrectly placed in Product.API.

Do not assume interface signatures. Read the source.

## A5. Product.API audit

Trace the entire image workflow from HTTP request to database and Garage.

Review:

- Controller responsibilities.
- Authorization.
- DTOs and contracts.
- Validation.
- Upload session creation.
- Presigned URL generation.
- Processing service.
- Worker lifecycle.
- Image validation.
- Gallery update.
- Concurrency handling.
- Media delivery.
- Cleanup.
- Error handling.
- Logging.
- Configuration.
- DI registration.
- Database transaction boundaries.

Identify possible:
- God services.
- Duplicate mapping or validation.
- Unnecessary dependencies.
- Tight coupling.
- Incorrect dependency direction.
- Unclear ownership.
- Repeated configuration.
- Hard-to-test code.
- Inconsistent naming.
- Overly complicated implementations.

Do not refactor merely to reduce line count.

## A6. Architecture proposal

Propose a target structure based on actual repository conventions.

Consider this conceptual direction, but do not force it:

Product.API:
- Controllers / Endpoints
- Modules / ProductImages
- Application services
- Contracts
- Validators
- Persistence
- Infrastructure
- Configuration
- Common

Shared libraries:
- ObjectStorage.Abstractions
- ObjectStorage.S3

Do not create a separate class library for Product Images unless there is a clear architectural benefit.

Do not create empty folders or placeholder projects.

For each proposed module, specify:
- Responsibility.
- Public interfaces.
- Dependencies.
- Ownership.
- Why the boundary is useful.

## A7. Audit deliverables

Create a proposed audit report containing:

1. Executive summary.
2. Current architecture diagram.
3. Current Garage setup.
4. Current S3 integration flow.
5. Current Product Image workflow.
6. Existing folder and dependency structure.
7. Technical debt and duplication.
8. Refactoring opportunities.
9. Proposed target architecture.
10. Proposed documentation structure.
11. Risks and compatibility considerations.
12. Prioritized implementation plan.

Suggested output location:

`document/phase-4.5/`

Suggested files:
- `01-current-state-audit.md`
- `02-architecture-assessment.md`
- `03-refactoring-plan.md`
- `04-documentation-plan.md`

These are proposed paths. Check repository conventions before creating them.

## A8. Gate A restrictions

During Gate A:
- Do not modify application code.
- Do not modify project files.
- Do not modify migrations.
- Do not modify Garage configuration.
- Do not modify Docker or deployment files.
- Do not modify bucket settings.
- Do not change credentials or permissions.
- Do not install new dependencies.
- Do not run destructive commands.
- Do not commit or push.

You may run safe, read-only inspection commands.

If a build or test command might generate files or modify assets, inspect its expected effects first and avoid it unless necessary.

## A9. Gate A completion

Present:
- What was inspected.
- Actual architecture findings.
- Major problems.
- Proposed target architecture.
- Proposed documentation structure.
- Prioritized refactoring plan.
- Risks and unresolved questions.

Then STOP.

Wait for the user to explicitly approve Gate B.

---

# GATE B – DOCUMENTATION

## B1. Objective

Document the existing Garage, S3 integration, and Product.API implementation.

Documentation must describe the actual code and configuration.

Do not write documentation based on assumptions.

## B2. Garage documentation

Create or update documentation covering:

### Architecture
- Shared Garage cluster.
- Private bucket strategy.
- Staging and media buckets.
- Product.API relationship.
- S3-compatible communication.

### Local setup
- Prerequisites.
- Garage installation or container startup.
- Configuration.
- Port mapping.
- Persistent storage.
- Initial startup.
- Health checks.
- Shutdown and restart.

### Bucket and key setup
- Bucket creation procedure for a fresh environment.
- Access-key creation procedure for a fresh environment.
- Least-privilege permissions.
- Bucket verification.
- Common mistakes.

IMPORTANT:
Existing buckets and credentials must not be recreated or modified during this phase.

Instructions for fresh environment provisioning are documentation only unless separately approved.

### S3 integration
- Endpoint configuration.
- Region.
- Credentials.
- Path-style addressing, if required by the actual implementation.
- Presigned URLs.
- Upload headers.
- Object operations.
- Error handling.

### Environment configuration
Clearly separate:
- Local development.
- Test.
- Production.

Never put actual secrets in documentation.

Use placeholders for secret values.

### Troubleshooting
Cover common issues:
- Garage unavailable.
- Incorrect endpoint.
- Invalid credentials.
- Access denied.
- Bucket not found.
- Presigned URL signature mismatch.
- Content-Type mismatch.
- Object not found.
- Copy failure.
- Delete failure.
- Network timeout.

## B3. Product.API documentation

Document:

1. Product Image architecture.
2. Upload session creation.
3. Direct browser-to-Garage upload.
4. Worker processing.
5. Actual image validation.
6. Staging-to-media copy.
7. Upload status transitions.
8. Gallery update.
9. `If-Match` concurrency behavior.
10. Media delivery.
11. Cleanup.
12. Retry and failure behavior.
13. API examples.
14. Configuration.
15. Testing.
16. Troubleshooting.

Include Mermaid diagrams where useful.

Document actual endpoint routes, request/response schemas, and status codes from source code.

Do not invent API contracts.

## B4. CORS documentation

Preserve the current state:

- Garage CORS is not enabled.
- Product.API CORS is not enabled.
- No fake origins.
- No wildcard origin.
- No bucket policy changes.

Explain the difference between:
- Product.API CORS.
- Garage staging bucket CORS.
- Authentication and authorization.
- S3 bucket permissions.

Explain the future configuration requirements:
- Exact browser origins.
- Allowed methods.
- Allowed headers.
- Exposed headers.
- Browser upload flow.

Do not activate CORS.

## B5. Documentation quality requirements

All documentation must:
- Use consistent terminology.
- Contain valid relative links.
- Avoid duplicated explanations.
- Distinguish implemented behavior from planned behavior.
- Include reproducible commands.
- Include expected output where useful.
- Avoid real credentials.
- Match actual source code.
- Include prerequisites and assumptions.

Create a documentation index or update existing navigation.

## B6. Gate B verification

Check:
- Markdown formatting.
- Relative links.
- File paths.
- Commands.
- Configuration key names.
- API routes.
- Mermaid syntax where practical.
- Consistency with source code.

Do not modify application code in this gate.

## B7. Gate B completion

Report:
- Created and updated documents.
- Main topics covered.
- Missing information.
- Documentation verification results.

Then STOP and wait for approval.

---

# GATE C – REFACTORING DESIGN

## C1. Objective

Convert the audit findings into a detailed, actionable refactoring design.

This gate is design-only.

Do not modify application code.

## C2. Design requirements

For every proposed change, explain:

- Current implementation.
- Identified problem.
- Proposed implementation.
- Expected benefit.
- Files affected.
- Dependency impact.
- Test impact.
- Regression risk.
- Whether API behavior changes.
- Whether database schema changes.

## C3. Module design

Design a cohesive Product Image module.

Responsibilities may include:

- Upload session.
- Upload validation.
- Image processing orchestration.
- Gallery management.
- Media delivery.
- Cleanup.

Do not split these into many services without a meaningful reason.

A class should have a clear responsibility and a manageable dependency set.

## C4. Reuse strategy

Evaluate reuse at three levels:

1. Product.API internal reuse.
2. Shared BuildingBlocks reuse.
3. Cross-service reuse opportunities.

Only promote code into BuildingBlocks when it is genuinely generic and has a clear use case beyond Product.API.

Examples of potentially reusable code:
- Object storage contracts.
- S3-compatible adapter.
- Generic storage configuration.
- Generic storage exceptions.
- Generic object metadata.

Examples that must remain Product.API-specific:
- Product image rules.
- Product gallery rules.
- Product upload status.
- Product-specific validation.
- Product authorization.
- Product image lifecycle.

Do not move code into shared libraries simply because it is currently used by several classes.

## C5. Design principles

Use pragmatic .NET architecture.

Prefer:
- High cohesion.
- Low coupling.
- Explicit dependencies.
- Constructor injection.
- Small, focused services.
- Consistent naming.
- Testable business logic.
- Clear module boundaries.
- Existing repository conventions.

Avoid:
- Unnecessary interfaces.
- Generic repositories without a concrete benefit.
- Excessive abstraction layers.
- Overengineering.
- Unnecessary CQRS/MediatR adoption.
- Service Locator.
- Static mutable state.
- Business logic inside controllers.
- Infrastructure logic inside business entities.
- Duplicate mapping frameworks or helpers.

## C6. Refactoring execution plan

Divide implementation into small batches.

For each batch define:
- Objective.
- Files to change.
- Dependencies.
- Tests to run.
- Expected result.
- Rollback considerations.

Recommended order:

1. Naming and folder organization.
2. Configuration and DI cleanup.
3. Service responsibility separation.
4. Duplicate code elimination.
5. Shared-code reuse improvements.
6. Worker and lifecycle organization.
7. Test organization and coverage improvements.

Adjust the order based on actual audit findings.

## C7. Database and lifecycle review

Review current upload lifecycle and concurrency implementation.

Pay special attention to:
- Pending.
- Processing.
- Ready.
- Rejected.
- Expired.
- Retry behavior.
- Worker crash recovery.
- Processing lease.
- Cleanup retry.
- Concurrent worker execution.
- Gallery concurrency.

Do not introduce new status values, columns, indexes, or migrations without explicit approval.

If reliability improvements require schema changes, describe them as a separate proposal.

Preserve existing enum numeric values and data compatibility.

## C8. API compatibility

Preserve existing API routes and contracts.

In particular:

`GET /api/v1/media/{assetKey}`

Do not rename or restructure external routes merely to align with an architectural preference.

## C9. Gate C completion

Present:
- Target architecture.
- Module responsibilities.
- Dependency diagram.
- Proposed folder structure.
- Detailed change list.
- Implementation batches.
- Testing strategy.
- Risk assessment.
- Any decisions requiring user approval.

Then STOP.

Wait for explicit approval of Gate D.

---

# GATE D – REFACTORING IMPLEMENTATION

## D1. Objective

Implement the approved refactoring design.

Only perform changes explicitly covered by the approved Gate C plan.

## D2. Implementation rules

- Work in small, logical batches.
- Do not rewrite the entire Product.API.
- Preserve existing behavior.
- Preserve API contracts.
- Preserve authorization rules.
- Preserve database schema unless separately approved.
- Preserve Garage bucket names and access boundaries.
- Preserve storage configuration semantics.
- Avoid unnecessary dependencies.
- Avoid unrelated refactoring.
- Keep tests synchronized with code changes.

After each batch:
1. Build the affected project.
2. Run relevant tests.
3. Inspect the diff.
4. Report results.
5. Continue only if the batch is safe and within the approved plan.

If an unexpected issue requires an architectural decision, STOP and ask the user.

## D3. Code quality requirements

Review:
- Naming.
- Method length and responsibility.
- Class cohesion.
- Dependency count.
- Constructor complexity.
- Duplicate logic.
- Null handling.
- Async/await usage.
- CancellationToken propagation.
- Exception handling.
- Logging.
- Configuration validation.
- Resource disposal.
- Thread safety.
- Testability.

Do not make arbitrary changes solely to satisfy subjective style preferences.

## D4. Storage safety

Never:
- Make buckets public.
- Change credentials.
- Change permissions.
- Delete existing storage objects.
- Change Garage cluster configuration.
- Modify production storage.
- Change CORS activation state.

Use mocks or dedicated test infrastructure for tests.

Do not perform destructive operations against existing buckets.

## D5. Test requirements

Preserve and improve tests for:

### Upload session
- Valid request.
- Invalid product.
- Invalid MIME.
- Invalid size.
- Duplicate or invalid state.

### Image processing
- Valid image.
- Invalid file content.
- MIME spoofing.
- Missing staging object.
- Storage unavailable.
- Copy failure.
- Retry behavior.

### Gallery
- Ready upload.
- Invalid upload state.
- Wrong product.
- Duplicate image.
- Maximum image count.
- `If-Match` concurrency.

### Cleanup
- Successful deletion.
- Already missing object.
- Transient deletion failure.
- Retry behavior.
- Database state consistency.

### Delivery
- Existing media object.
- Missing object.
- Private access behavior.
- Correct content type.
- No storage credential leakage.

Do not claim Garage integration has passed if it was skipped.

## D6. Database safety

Do not:
- Delete migrations.
- Rewrite applied migrations.
- Drop columns or tables.
- Reset the database.
- Change production schema.
- Change existing enum values.

If a schema change is necessary, stop and propose it separately.

## D7. Gate D completion

Report:
- Implementation batches completed.
- Files created, modified, or deleted.
- Architectural improvements.
- Code reuse improvements.
- API compatibility.
- Database impact.
- Tests executed.
- Test results.
- Remaining issues.

Then STOP and wait for approval of Gate E.

---

# GATE E – FINAL VERIFICATION AND COMPLETION

## E1. Objective

Verify the final implementation and ensure documentation matches code.

## E2. Verification checklist

### Build
- Restore solution.
- Build solution.
- Report warnings and errors.

### Tests
- Product.API tests.
- ObjectStorage.Abstractions tests.
- ObjectStorage.S3 tests.
- Relevant integration tests.
- Report passed, failed, skipped separately.

### Code quality
- `git diff --check`.
- Inspect all changed files.
- Check for accidental unrelated modifications.
- Check for duplicate or dead code introduced by refactoring.
- Check project references and dependency direction.

### API compatibility
- Verify routes.
- Verify request and response contracts.
- Verify authorization.
- Verify `If-Match`.
- Verify image delivery endpoint.

### Database
- Verify no unapproved schema changes.
- Verify no unapproved migration changes.
- Verify enum compatibility.

### Garage and S3
- Verify configuration and documentation.
- Verify no bucket, credential, permission, or CORS changes.
- Report whether real Garage integration was executed.
- Distinguish mocks from real integration tests.

### Documentation
- Verify documented paths exist.
- Verify commands and configuration names.
- Verify endpoint contracts.
- Verify architecture diagrams.
- Verify documentation accurately describes actual implementation.

## E3. Final report

Produce:

1. Executive summary.
2. Final architecture.
3. Garage setup documentation summary.
4. S3 integration documentation summary.
5. Product.API module structure.
6. Refactoring changes.
7. Reuse improvements.
8. API compatibility.
9. Database impact.
10. Build results.
11. Test results.
12. Integration test limitations.
13. Remaining technical debt.
14. Recommended Phase 5 backlog.

## E4. Completion criteria

Phase 4.5 can be considered completed when:

- Garage setup documentation is reproducible.
- S3 integration documentation matches implementation.
- Product.API image workflow is fully documented.
- Product.API module boundaries are clear.
- Refactoring changes are completed and reviewed.
- Existing API contracts are preserved.
- Existing database schema is preserved unless separately approved.
- Automated regression tests pass.
- Skipped integration tests are clearly identified.
- No unapproved infrastructure or security changes occurred.
- Documentation matches the final source code.

Do not claim production readiness solely because local build and tests pass.

Then STOP and wait for the user's next instruction.

---

# 7. GLOBAL SAFETY AND CHANGE CONTROL

These rules apply to every gate.

## Never do the following without explicit approval

- Commit.
- Push.
- Deploy.
- Reset Git.
- Force checkout.
- Discard user changes.
- Delete untracked files.
- Delete existing data.
- Change Garage infrastructure.
- Create or delete buckets.
- Rotate credentials.
- Change bucket permissions.
- Enable public access.
- Enable CORS.
- Change production configuration.
- Change database schema.
- Introduce new infrastructure services.

## Git safety

At the beginning of each gate:
- Inspect `git status`.
- Identify pre-existing user changes.
- Preserve all existing changes.
- Do not assume uncommitted changes belong to this task.

At the end of each implementation gate:
- Inspect `git status`.
- Inspect `git diff --stat`.
- Inspect `git diff --check`.
- Clearly identify files changed by this task versus pre-existing changes where possible.

Do not commit or push.

## Scope control

Only work on:
- Garage documentation.
- S3 integration documentation.
- Product.API image architecture.
- Product.API image-related refactoring.
- Directly related tests.

Do not refactor unrelated microservices.

Do not refactor the entire solution.

Do not introduce new architectural patterns without a concrete benefit.

## Decision control

If implementation evidence conflicts with an approved decision:
1. Explain the conflict.
2. Explain its impact.
3. Propose alternatives.
4. Ask the user to decide.
5. Do not silently override the decision.

## Communication

All progress reports and explanations must be in Vietnamese.

Use concise but technically precise explanations.

At every gate completion, include:

- **Status**
- **Findings**
- **Files affected**
- **Verification**
- **Risks**
- **Pending decisions**
- **Next gate**

Do not continue to the next gate until the user explicitly approves it.

---

# 8. INITIAL ACTION

Start with **GATE A – REPOSITORY AUDIT AND PLANNING ONLY**.

Your first response must:

1. Confirm the repository path.
2. Inspect the working tree.
3. Identify the existing Garage, ObjectStorage, and Product.API implementation.
4. Audit the current documentation.
5. Produce the Gate A report and proposed plans.

Do not implement anything.

Do not write or modify documentation yet.

Do not refactor code.

Do not modify configuration.

Do not commit, push, or deploy.

**Wait for explicit user approval before starting Gate B.**