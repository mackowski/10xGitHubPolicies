## 10x GitHub Policy Enforcer – Production Deployment (OIDC + GitHub Actions)

This guide describes one-time setup in Azure and GitHub, Infrastructure-as-Code (Bicep), and fully automated CI/CD using GitHub Actions with Azure OIDC (workload identity federation). It deploys the Blazor Server app to Azure App Service and uses Azure SQL for EF Core + Hangfire storage.

### Overview
- Hosting: Azure App Service (Linux, HTTPS only)
- Database: Azure SQL (serverless recommended)
- Auth: GitHub OAuth for app users; OIDC (workload identity federation) for GitHub Actions → Azure
- Jobs: Hangfire (dashboard at `/hangfire`)
- App settings: Production disables Test Mode (`TestMode__Enabled=false`)
- Secretless SQL (recommended): Use Azure AD Managed Identity (MSI) for the Web App to connect to Azure SQL without username/password

---

## 1) One-time setup (Azure + GitHub)

### 1.1 Decide names and region
- Resource group: `rg-10xghpolicies-prod`
- Region: `westeurope` 
- App Service Plan: `asp-10xghpolicies-prod`
- Web App: `wa-10xghpolicies-prod` 
- SQL Server: `sql-10xghpolicies-prod`
- SQL DB: `sqldb-10xghpolicies`
- GitHub repo: `OWNER/10xGitHubPolicies`

### 1.2 Create Azure AD App Registration + OIDC (workload identity federation)
Use Azure Cloud Shell or local `az` CLI (logged into the correct subscription):

```bash
# Inputs
SUBSCRIPTION_ID="5ad1ad66-5ad7-4ed5-aeb2-a46b1abfbffd"
TENANT_ID="8afe73f9-0d93-4821-a898-c5c2dc320953"
RESOURCE_GROUP="rg-10xghpolicies-prod"
AZURE_LOCATION="westeurope"
APP_DISPLAY_NAME="gha-10xghpolicies-prod"
REPO="mackowski/10xGitHubPolicies"  

# Resource group
az group create -n "$RESOURCE_GROUP" -l "$AZURE_LOCATION"

# App registration + service principal
APP_JSON=$(az ad app create --display-name "$APP_DISPLAY_NAME")
APP_ID=$(echo "$APP_JSON" | jq -r .appId)
SP_JSON=$(az ad sp create --id "$APP_ID")
SP_OBJECT_ID=$(echo "$SP_JSON" | jq -r .id)

# RBAC on the resource group (Contributor is sufficient for Web App/SQL steps used here)
az role assignment create \
  --assignee-object-id "$SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "Contributor" \
  --scope "/subscriptions/$SUBSCRIPTION_ID/resourceGroups/$RESOURCE_GROUP"

# Federated credential bound to the GitHub Actions environment: production
az ad app federated-credential create \
  --id "$APP_ID" \
  --parameters '{
    "name": "gh-env-production",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:'"$REPO"'":environment:production",
    "audiences": ["api://AzureADTokenExchange"]
  }'

# Output values for GitHub repository variables
echo "AZURE_CLIENT_ID=$APP_ID"
echo "AZURE_TENANT_ID=$TENANT_ID"
echo "AZURE_SUBSCRIPTION_ID=$SUBSCRIPTION_ID"
```

Why environment-bound trust? Azure only accepts OIDC tokens for jobs that target the `production` environment, letting you enforce GitHub Environment protections (approvals, wait timers) before any Azure access is granted.

### 1.3 Configure GitHub
- Create GitHub Environment `production` and add protection rules (required reviewers, wait timer, etc.).
- Repository Variables (Settings → Variables → Actions):
  - `AZURE_CLIENT_ID`
  - `AZURE_TENANT_ID`
  - `AZURE_SUBSCRIPTION_ID`
- Repository Secrets (Settings → Secrets → Actions):
  - `GITHUB_CLIENT_ID` (OAuth app for user login)
  - `GITHUB_CLIENT_SECRET`
  - `ORG_NAME` (value for `GitHubApp:OrganizationName`)

Note: When using Managed Identity for SQL (recommended), SQL admin username/password are not required for application runtime or CI/CD. They are only needed for one-time bootstrap if you have not yet configured SQL Azure AD admin.

### 1.4 Create GitHub OAuth App (for user login to the product)
- Homepage URL: `https://wa-10xghpolicies-prod.azurewebsites.net`
- Authorization callback URL: `https://wa-10xghpolicies-prod.azurewebsites.net/signin-github`
- Store Client ID/Secret in repo secrets as above.

---

## 2) Infrastructure as Code (Bicep)

Create `infra/main.bicep`:

```bicep
param location string = resourceGroup().location
@description('Base name prefix, e.g. 10xghpol')
param baseName string
@secure() param sqlAdminLogin string
@secure() param sqlAdminPassword string
@description('Web App name, must be globally unique')
param webAppName string
@description('SKU for App Service Plan')
param appServiceSku string = 'P1v3'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${baseName}'
  location: location
  sku: {
    name: appServiceSku
    tier: 'PremiumV3'
    size: appServiceSku
    capacity: 1
  }
  kind: 'app'
}

resource web 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppName
  location: location
  kind: 'app'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: plan.id
    httpsOnly: true
  }
}

resource sql 'Microsoft.Sql/servers@2022-05-01-preview' = {
  name: 'sql-${baseName}'
  location: location
  properties: {
    administratorLogin: sqlAdminLogin
    administratorLoginPassword: sqlAdminPassword
    publicNetworkAccess: 'Enabled'
  }
}

resource db 'Microsoft.Sql/servers/databases@2022-05-01-preview' = {
  name: '${sql.name}/sqldb-${baseName}'
  location: location
  sku: {
    name: 'GP_S_Gen5' // Serverless
    tier: 'GeneralPurpose'
  }
  properties: {
    autoPauseDelay: 60
    minCapacity: 0.5
  }
}

resource fwAllowAzure 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  name: '${sql.name}/AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('App settings')
param orgName string
@secure() param githubClientId string
@secure() param githubClientSecret string

resource webConfig 'Microsoft.Web/sites/config@2023-12-01' = {
  name: '${web.name}/appsettings'
  properties: {
    'ASPNETCORE_ENVIRONMENT': 'Production'
    'TestMode__Enabled': 'false'
    'GitHubApp__OrganizationName': orgName
    'GitHub__ClientId': githubClientId
    'GitHub__ClientSecret': githubClientSecret
    // Secretless MSI connection string (no username/password)
    'ConnectionStrings__DefaultConnection': 'Server=tcp:${sql.name}.database.windows.net,1433;Database=${db.name};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity'
  }
}

output webAppUrl string = 'https://${web.name}.azurewebsites.net'
```

---

## 3) GitHub Actions – OIDC-based workflows

### 3.1 Provisioning workflow (run once or on infra changes)
Create `.github/workflows/provision.yml`:

```yaml
name: provision-infra
on:
  workflow_dispatch:
    inputs:
      baseName:
        description: "Base name (e.g. 10xghpol)"
        required: true
      webAppName:
        description: "Web App name (globally unique)"
        required: true

permissions:
  id-token: write
  contents: read

env:
  RESOURCE_GROUP: rg-10xghpolicies-prod
  LOCATION: westeurope

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      - name: Create RG
        uses: azure/cli@v2
        with:
          inlineScript: |
            az group create -n ${{ env.RESOURCE_GROUP }} -l ${{ env.LOCATION }}

      - name: Deploy Bicep
        uses: azure/cli@v2
        with:
          inlineScript: |
            az deployment group create \
              -g ${{ env.RESOURCE_GROUP }} \
              -f infra/main.bicep \
              -p baseName='${{ github.event.inputs.baseName }}' \
                 webAppName='${{ github.event.inputs.webAppName }}' \
                 sqlAdminLogin='${{ secrets.SQL_ADMIN_LOGIN }}' \
                 sqlAdminPassword='${{ secrets.SQL_ADMIN_PASSWORD }}' \
                 orgName='${{ secrets.ORG_NAME }}' \
                 githubClientId='${{ secrets.GITHUB_CLIENT_ID }}' \
                 githubClientSecret='${{ secrets.GITHUB_CLIENT_SECRET }}'

      # Optional one-time bootstrap if not already configured:
      # Set Azure AD admin on SQL server and create MSI DB user matching Web App identity
      - name: Configure SQL Azure AD admin (one-time)
        if: ${{ always() }}
        uses: azure/cli@v2
        with:
          inlineScript: |
            # Enable AAD-only auth and set AAD admin (use your group/user)
            az sql server ad-only-auth enable -g ${{ env.RESOURCE_GROUP }} -s sql-10xghpolicies-prod
            az sql server ad-admin create \
              -g ${{ env.RESOURCE_GROUP }} \
              -s sql-10xghpolicies-prod \
              -u "AAD-SQL-Admins" \
              -i "<AAD_GROUP_OBJECT_ID>"

      - name: Create DB user for Web App Managed Identity
        uses: azure/cli@v2
        with:
          inlineScript: |
            WEBAPP_NAME='${{ github.event.inputs.webAppName }}'
            # Install sqlcmd
            sudo ACCEPT_EULA=Y apt-get update && sudo ACCEPT_EULA=Y apt-get install -y mssql-tools unixodbc-dev
            export PATH="$PATH:/opt/mssql-tools/bin"
            # Create contained user from EXTERNAL PROVIDER and grant role
            sqlcmd -S "tcp:sql-10xghpolicies-prod.database.windows.net,1433" -d "sqldb-10xghpolicies" -G -C -l 30 -Q "
              IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = '$(WEBAPP_NAME)')
              BEGIN
                CREATE USER [$(WEBAPP_NAME)] FROM EXTERNAL PROVIDER;
                ALTER ROLE db_owner ADD MEMBER [$(WEBAPP_NAME)];
              END
            "
```

### 3.2 CI/CD workflow (automatic on main, deploy gated by environment: production)
Create `.github/workflows/ci-cd.yml`:

```yaml
name: ci-cd
on:
  push:
    branches: [ "main" ]
  workflow_dispatch: {}

permissions:
  id-token: write
  contents: read

env:
  DOTNET_VERSION: '8.0.x'
  PROJECT_PATH: '10xGitHubPolicies.App/10xGitHubPolicies.App.csproj'
  WEBAPP_NAME: 'wa-10xghpolicies-prod'
  RESOURCE_GROUP: 'rg-10xghpolicies-prod'
  SQL_SERVER: 'sql-10xghpolicies-prod'
  SQL_DB: 'sqldb-10xghpolicies'

jobs:
  build-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}
      - run: dotnet restore ${{ env.PROJECT_PATH }}
      - run: dotnet build ${{ env.PROJECT_PATH }} -c Release --no-restore
      # - run: dotnet test --configuration Release --no-build
      - run: dotnet publish ${{ env.PROJECT_PATH }} -c Release -o publish
      - uses: actions/upload-artifact@v4
        with:
          name: drop
          path: publish

  migrate-and-deploy:
    environment: production
    needs: build-test
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
        with:
          name: drop
          path: drop

      - name: Azure login (OIDC)
        uses: azure/login@v2
        with:
          client-id: ${{ vars.AZURE_CLIENT_ID }}
          tenant-id: ${{ vars.AZURE_TENANT_ID }}
          subscription-id: ${{ vars.AZURE_SUBSCRIPTION_ID }}

      # Optional: Get runner public IP and open firewall temporarily unless using private endpoints
      - name: Get runner public IP
        id: ip
        uses: haythem/public-ip@v1.3

      - name: Add firewall rule for runner
        uses: azure/cli@v2
        with:
          inlineScript: |
            az sql server firewall-rule create \
              -g ${{ env.RESOURCE_GROUP }} \
              -s ${{ env.SQL_SERVER }} \
              -n "gh-runner-${{ github.run_id }}" \
              --start-ip-address ${{ steps.ip.outputs.ipv4 }} \
              --end-ip-address ${{ steps.ip.outputs.ipv4 }}

      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: ${{ env.DOTNET_VERSION }}

      # Recommended: run migrations using Managed Identity via a tiny console runner
      # This avoids handling AAD tokens in CLI and remains secretless
      - name: Run EF Core migrations via MSI
        env:
          ConnectionStrings__DefaultConnection: "Server=tcp:${{ env.SQL_SERVER }}.database.windows.net,1433;Database=${{ env.SQL_DB }};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity"
          ASPNETCORE_ENVIRONMENT: "Production"
        run: |
          dotnet run --project Tools/DbMigrator/DbMigrator.csproj --configuration Release

      - name: Remove firewall rule
        if: always()
        uses: azure/cli@v2
        with:
          inlineScript: |
            az sql server firewall-rule delete \
              -g ${{ env.RESOURCE_GROUP }} \
              -s ${{ env.SQL_SERVER }} \
              -n "gh-runner-${{ github.run_id }}" \
              --yes

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v3
        with:
          app-name: ${{ env.WEBAPP_NAME }}
          package: drop

      - name: Apply app settings (idempotent)
        uses: azure/cli@v2
        with:
          inlineScript: |
            az webapp config appsettings set \
              -g ${{ env.RESOURCE_GROUP }} \
              -n ${{ env.WEBAPP_NAME }} \
              --settings \
                ASPNETCORE_ENVIRONMENT=Production \
                TestMode__Enabled=false \
                GitHubApp__OrganizationName='${{ secrets.ORG_NAME }}' \
                GitHub__ClientId='${{ secrets.GITHUB_CLIENT_ID }}' \
                GitHub__ClientSecret='${{ secrets.GITHUB_CLIENT_SECRET }}' \
                ConnectionStrings__DefaultConnection='Server=tcp:${{ env.SQL_SERVER }}.database.windows.net,1433;Database=${{ env.SQL_DB }};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity'
```

---

## 4) Go-live steps
1. Commit `infra/main.bicep` and both workflow files; push to `main`.
2. Ensure GitHub repo Variables/Secrets and the `production` environment protections are configured.
3. Run the “provision-infra” workflow once to create Azure resources, configure app settings, set SQL Azure AD admin, and create the MSI DB user.
4. Create the GitHub OAuth App with the production callback URL and verify secrets.
5. Push to `main` (or dispatch) to trigger “ci-cd”. Approve the `production` environment when prompted.
6. Verify:
   - App: `https://wa-10xghpolicies-prod.azurewebsites.net`
   - OAuth login works; `TestMode` is OFF.
   - Hangfire Dashboard at `/hangfire` requires authenticated access.
   - Database schema exists and scheduled job is registered.
   - Application connects to SQL without username/password (MSI-based).

---

## 5) Security notes and next steps
- OIDC eliminates cloud secrets in GitHub; Azure trusts only tokens from jobs targeting the protected `production` environment.
- Scope RBAC to the resource group only (principle of least privilege).
- Managed Identity for SQL removes DB credentials entirely; ensure SQL firewall or private endpoints are configured appropriately for CI runners.
- Optionally add custom domain and certs via additional Bicep and DNS changes.

