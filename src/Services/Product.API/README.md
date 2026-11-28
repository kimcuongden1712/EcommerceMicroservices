# Product API

Catalog HTTP API (`/api/products`) backed by MySQL. The project uses EF Core with `MySql.EntityFrameworkCore` and applies database migrations synchronously before accepting HTTP traffic. A migration or development seed failure is logged and stops startup.

Health endpoints:

- `/health/live` checks that the API process is running; it does not depend on MySQL.
- `/health/ready` checks MySQL connectivity and reports whether the API can serve requests.
- `/hc` remains an aggregate health report for compatibility.

## Infrastructure and configuration

| Dependency | Compose address | Host development address | Setting |
| --- | --- | --- | --- |
| MySQL `ProductDb` | `productdb:3306` | `localhost:3307` | `DatabaseSettings:ConnectionString` |
| Identity API | `tedu.identity.api:80` | `localhost:6001` | `ApiConfiguration:IdentityServerBaseUrl`, `ApiConfiguration:IssuerUri` |
| Elasticsearch (optional logging) | `elasticsearch:9200` | `localhost:9200` | `ElasticConfiguration:Uri` |

Compose overrides the database connection string with the `tedu_product_db` container name. `JwtSettings` and `ApiConfiguration` also control authentication and Swagger OAuth settings. Compose starts the identity server as `identity.api`. The Compose HTTP port is **6002**; the local launch profile uses **5002**.

## Browser CORS

Product.API has an environment-specific browser CORS policy. Set `PRODUCT_API_CORS_ALLOWED_ORIGINS` to one or more exact comma-separated MFE origins when the browser calls Product.API from a different origin:

```powershell
$env:PRODUCT_API_CORS_ALLOWED_ORIGINS = '<exact MFE origin>'
dotnet run --project src/Services/Product.API/Product.API.csproj --launch-profile Product.API
```

The policy allows `GET`, `POST`, and `PUT`, request headers `Authorization`, `Content-Type`, `If-Match`, and `X-Correlation-ID`, and exposes `ETag`. It does not enable credentials. The default `ProductApiCors:AllowedOrigins` list is empty, so same-origin/BFF deployments need no browser CORS configuration. Origins must be `http` or `https` authorities without a path or wildcard; invalid values fail startup. Garage bucket CORS is configured separately by `src/Infrastructure/Garage/scripts/configure-local-cors.ps1` for direct uploads to `product-staging`.

## Create `appsettings.Development.json`

Create `src/Services/Product.API/appsettings.Development.json` for a local `dotnet run`:

```json
{
  "DatabaseSettings": {
    "DBProvider": "Mysql",
    "ConnectionString": "Server=localhost;Port=3307;Database=ProductDb;Uid=root;Pwd=Passw0rd",
    "DatabaseName": "ProductDb"
  },
  "JwtSettings": {
    "Key": "replace-with-your-development-signing-key"
  },
  "ApiConfiguration": {
    "ApiName": "tedu_microservices_api",
    "ApiVersion": "v1",
    "ApiBaseUrl": "http://localhost:5002",
    "IdentityServerBaseUrl": "http://localhost:6001",
    "IssuerUri": "http://localhost:6001",
    "ClientId": "tedu_microservices_swagger"
  }
}
```

Set `IssuerUri` to the issuer configured by your running identity server; it must also be reachable from the Product API process. Add `ElasticConfiguration` if you want Elasticsearch logging. The local settings file is ignored by Git.

Run from the repository root with `dotnet run --project src/Services/Product.API/Product.API.csproj --launch-profile Product.API`, after providing reachable MySQL and identity settings. Existing database migrations should be reviewed when changing the EF Core provider.

## Docker hub

- docker login: docker login
- Go to docker hub, create a new repository: tedu-microservice-product
- At root folder:
  - replace `kietphamdev` with your repo id
  - `docker build -t kietphamdev/tedu-microservice-product -f src/Services/Product.API/Dockerfile src/.`
  - `docker push kietphamdev/tedu-microservice-product`
