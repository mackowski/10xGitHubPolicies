param location string = resourceGroup().location
@description('Base name prefix, e.g. 10xghpol')
param baseName string
@secure()
param sqlAdminLogin string
@secure()
param sqlAdminPassword string
@description('Web App name, must be globally unique')
param webAppName string
@description('SKU for App Service Plan')
param appServiceSku string = 'B1'

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-${baseName}'
  location: location
  sku: {
    name: appServiceSku
    tier: 'Basic'
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
  name: 'sqldb-${baseName}'
  parent: sql
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    autoPauseDelay: 60
  }
}

resource fwAllowAzure 'Microsoft.Sql/servers/firewallRules@2022-05-01-preview' = {
  name: 'AllowAzureServices'
  parent: sql
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

@description('App settings')
param orgName string
@secure()
param githubClientId string
@secure()
param githubClientSecret string
@secure()
param githubAppId string
@secure()
param githubAppPrivateKey string
@secure()
param githubAppInstallationId string

resource webConfig 'Microsoft.Web/sites/config@2023-12-01' = {
  parent: web
  name: 'appsettings'
  properties: {
    ASPNETCORE_ENVIRONMENT: 'Production'
    TestMode__Enabled: 'false'
    GitHubApp__OrganizationName: orgName
    GitHubApp__AppId: githubAppId
    GitHubApp__PrivateKey: githubAppPrivateKey
    GitHubApp__InstallationId: githubAppInstallationId
    GitHub__ClientId: githubClientId
    GitHub__ClientSecret: githubClientSecret
    ConnectionStrings__DefaultConnection: 'Server=tcp:${sql.name}.database.windows.net,1433;Database=sqldb-${baseName};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;Authentication=Active Directory Managed Identity'
  }
}

output webAppUrl string = 'https://${web.name}.azurewebsites.net'