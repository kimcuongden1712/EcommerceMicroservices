# Contracts

Shared domain and service interfaces used across APIs and infrastructure, including repository and unit-of-work contracts. The project also references EF Core types used by those interfaces and MongoDB driver types used by Mongo-backed implementations.

This is a class library with no process, port, or database connection of its own. Keep the EF Core reference compatible with each consuming API's provider when upgrading packages.
