# Shared

Configuration models, DTOs, and reusable types referenced by the APIs and other class libraries. Configuration models include `DatabaseSettings`, `MongoDbSettings`, `CacheSettings`, `EventBusSettings`, `HangFireSettings`, `ApiConfiguration`, `GrpcSettings`, and `ServiceUrls`.

This project does not connect to infrastructure or run a server. Each executable project binds the relevant configuration section from JSON and environment variables; Compose supplies container-specific overrides.
