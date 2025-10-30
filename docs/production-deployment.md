# Production Deployment (Azure OIDC + Managed Identity)

This guide outlines production deployment using GitHub Actions with Azure OpenID Connect (OIDC) and secretless SQL access via Azure Managed Identity (MSI).

## What you get
- Azure App Service (Linux, HTTPS only)
- Azure SQL (Serverless)
- GitHub OAuth for users; OIDC for CI → Azure
- Hangfire dashboard at `/hangfire`
- Test Mode disabled in production
- Secretless SQL via MSI

## End-to-end guide
For a complete step-by-step, see the detailed runbook: `/.ai/production-deployment.md`.

Highlights:
- Create an Azure AD App Registration and configure a federated credential bound to the GitHub `production` environment.
- Store `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID` as GitHub Repository Variables; store OAuth secrets and org name as Repository Secrets.
- Provision infra with Bicep (`infra/main.bicep`) via a `provision-infra` workflow.
- CI/CD uses OIDC for Azure login; deployments are gated by the `production` environment.

## Database migrations via Managed Identity
Use the lightweight console at `Tools/DbMigrator` to run EF Core migrations in CI without SQL credentials.

Environment variables expected:

```bash
ConnectionStrings__DefaultConnection="Server=tcp:<sql-server>.database.windows.net,1433;Database=<db>;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity"
ASPNETCORE_ENVIRONMENT=Production
```

Run in CI:

```bash
dotnet run --project Tools/DbMigrator/DbMigrator.csproj --configuration Release
```

This connects with the runner's Azure identity (established by `azure/login`) and applies `db.Database.MigrateAsync()`.

## CI/CD reference
- Keep deployments gated by the protected `production` environment to enforce approvals before Azure access.
- Optionally open a temporary SQL firewall rule for the runner IP or use private endpoints.
- After deploy, set app settings idempotently (environment, TestMode, GitHub OAuth, MSI connection string).

## Security
- No cloud secrets in CI: OIDC provides short-lived tokens, MSI handles SQL auth.
- Scope RBAC to the resource group.
- Disable Test Mode in production.

