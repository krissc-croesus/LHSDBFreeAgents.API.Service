# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Overview

ASP.NET Core Web API for the LHSDB fantasy hockey league, managing hockey players and contract offers. Data lives in Amazon DynamoDB; authentication is AWS Cognito JWTs. Single project solution (`LHSDBFreeAgentsAPI`).

Targets **.NET 10** (`net10.0`). Uses AWS SDK v4 (`AWSSDK.*` 4.x) and ASP.NET Core 10 packages. Still uses the classic `Startup.cs` + `Program.CreateHostBuilder` hosting model (not minimal hosting).

## Commands

Run all from the repo root (`dotnet` auto-discovers the `.sln`).

```bash
dotnet restore
dotnet build
dotnet run --project LHSDBFreeAgentsAPI    # serves https://localhost:5001 and http://localhost:5000
```

- Default launch profile sets `ASPNETCORE_ENVIRONMENT=Development` and opens `/players`.
- There is **no test project** in this repo — do not assume a `dotnet test` target exists.
- Deploy is to AWS Elastic Beanstalk via the AWS Toolkit / `dotnet eb deploy-environment` using `LHSDBFreeAgentsAPI/aws-beanstalk-tools-defaults.json` (app `LHSDBFreeAgents2020`, env `Lhsdbfreeagents2020-prod`, region `us-east-2`, nginx proxy). The `framework` key there must match the csproj `TargetFramework` (`net10.0`), and the target Beanstalk platform must offer a .NET 10 runtime (or deploy `--self-contained`).

## Architecture

Layered flow, one path per resource: **Controller → Service → Repository → DynamoDB**, with a **Mapper** translating between DB models and API models.

- `Controllers/` — HTTP endpoints. Return `IActionResult`, catch exceptions and translate to `BadRequest`/`NotFound`/`Unauthorized`. `[Authorize]` at the class level, so all endpoints require a valid JWT.
- `Services/` — orchestration only; delegate to repositories and call the mapper.
- `Repositories/DynamoDBImpl/` — all DynamoDB access via `DynamoDBContext`. Each repo `new`s its own `DynamoDBContext` from the injected `IAmazonDynamoDB`.
- `Mappers/Mapper.cs` — converts `PlayerDb`↔`PlayerResponse` and `OfferDb`↔`OfferModel`. When you add a field to a DB or API model, update the corresponding mapper method or it will silently drop the field.
- `Models/` — `*Db` classes are DynamoDB entities (annotated with `[DynamoDBTable]`, `[DynamoDBHashKey]`, etc.); `*Model`/`*Response` are the API DTOs.

Everything (services, repositories, mapper) is registered as a **singleton** in `Startup.ConfigureServices`. Keep these stateless — do not add per-request mutable state.

### DynamoDB tables & indexes (data model lives in the queries, not migrations)

Table/index names are string literals in the repositories and must match the live DynamoDB tables exactly:

- `Players` table (`PlayerDb`): hash key `UniqueID`. GSI `Team-OVK-index` (players by team). Free-agent lookups use a `Scan` filtering `IsFA == true` (and `Team` when scoped).
- `Offers` table (`OfferDb`): hash key `PlayerID`, GSI hash key `TeamID`. Indexes `TeamID-index` (offers by team) and `PlayerID-OfferedBy-index` (used by delete to find the caller's offer).

## Auth conventions

- JWT validation is configured manually in `Startup` against Cognito's JWKS endpoint. Issuer is derived from `AWSCognito:Region` + `AWSCognito:PoolId` in `appsettings.json`. Audience validation is **off**.
- Controllers read the caller's identity from the custom `"username"` claim (`User.FindFirst("username").Value`), not the standard name claim. Offer create/delete enforce that the offer's `OfferedBy` matches this claim.
- An `"Admin"` authorization policy exists requiring claim `custom:isAdmin == "1"` (defined but not yet applied to endpoints).
- CORS allows a fixed origin list (localhost:4200 + the piriwin.com hosts) in `Startup`. Add new front-end origins there.

## Gotchas

- `OfferService.CreateNewOffer` and `DeleteOffer` are synchronous (`void`) but call `async` repository methods **without awaiting** them (fire-and-forget). Exceptions from the DynamoDB write/delete will not surface to the controller's try/catch. Be aware of this if you touch offer write paths — awaiting properly is likely a latent bug fix, but changing it alters the method signatures up the chain.
- `AWSCognito` values (real pool/client IDs) and AWS region `us-east-2` are hardcoded in `appsettings.json` and `Startup`.
