# Infrastructure

Reusable service wiring and implementations for MassTransit/RabbitMQ, Hangfire storage, authentication helpers, HTTP retry policies, email services, and repositories. APIs reference this class library and choose which extensions to call from their own startup code.

`ConfigureMassTransitWithRabbitMq` reads `EventBusSettings:HostAddress` and `EventBusSettings:ServiceName`. The solution currently uses `MassTransit.RabbitMQ` 8.2.2. Hangfire wiring reads `HangFireSettings:Storage` and supports MongoDB, PostgreSQL, or SQL Server; the Compose Hangfire API selects MongoDB. This project has no independent host or port.

`MassTransit.RabbitMQ` should use `8.2.2` version for free community
