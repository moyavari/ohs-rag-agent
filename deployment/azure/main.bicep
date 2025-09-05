@description('Main Bicep template for OHS Copilot Azure deployment')

@minLength(3)
@maxLength(10)
param environmentName string = 'dev'

@minLength(3)
@maxLength(15)
param applicationName string = 'ohs-copilot'

param location string = resourceGroup().location

@description('Azure OpenAI region (may differ from main location)')
param openAiLocation string = 'eastus'

@description('Container image for the application')
param containerImage string = 'ghcr.io/your-org/ohs-copilot:latest'

@description('Administrator email for alerts and notifications')
param adminEmail string

var resourcePrefix = '${applicationName}-${environmentName}'
var tags = {
  Application: 'OHS Copilot'
  Environment: environmentName
  ManagedBy: 'Bicep'
}

// Key Vault for secrets management
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${resourcePrefix}-kv'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enabledForDeployment: false
    enabledForTemplateDeployment: true
    enabledForDiskEncryption: false
    softDeleteRetentionInDays: 7
    purgeProtectionEnabled: false
  }
}

// User-assigned managed identity for the application
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${resourcePrefix}-identity'
  location: location
  tags: tags
}

// Role assignment for Key Vault access
resource keyVaultAccessPolicy 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, managedIdentity.id, 'Key Vault Secrets User')
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
    principalId: managedIdentity.properties.principalId
    principalType: 'ServicePrincipal'
  }
}

// Azure OpenAI Cognitive Service
resource cognitiveService 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: '${resourcePrefix}-openai'
  location: openAiLocation
  tags: tags
  kind: 'OpenAI'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${resourcePrefix}-openai'
    publicNetworkAccess: 'Enabled'
    restrictOutboundNetworkAccess: false
  }
}

// Azure OpenAI GPT-4 deployment
resource gpt4Deployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: cognitiveService
  name: 'gpt-4'
  properties: {
    model: {
      format: 'OpenAI'
      name: 'gpt-4'
      version: '0125-Preview'
    }
    raiPolicyName: 'Microsoft.Default'
  }
  sku: {
    name: 'Standard'
    capacity: 10
  }
}

// Azure OpenAI Text Embedding deployment
resource embeddingDeployment 'Microsoft.CognitiveServices/accounts/deployments@2023-05-01' = {
  parent: cognitiveService
  name: 'text-embedding-ada-002'
  dependsOn: [gpt4Deployment]
  properties: {
    model: {
      format: 'OpenAI'
      name: 'text-embedding-ada-002'
      version: '2'
    }
    raiPolicyName: 'Microsoft.Default'
  }
  sku: {
    name: 'Standard'
    capacity: 10
  }
}

// Azure AI Content Safety
resource contentSafety 'Microsoft.CognitiveServices/accounts@2023-05-01' = {
  name: '${resourcePrefix}-contentsafety'
  location: location
  tags: tags
  kind: 'ContentSafety'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: '${resourcePrefix}-contentsafety'
    publicNetworkAccess: 'Enabled'
  }
}

// Cosmos DB account for vector store and memory
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2023-09-15' = {
  name: '${resourcePrefix}-cosmos'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    databaseAccountOfferType: 'Standard'
    enableFreeTier: environmentName == 'dev'
    capacityMode: 'Serverless'
    capabilities: [
      {
        name: 'EnableMongo'
      }
    ]
  }
}

// Cosmos DB database
resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2023-09-15' = {
  parent: cosmosAccount
  name: 'ohscopilot'
  properties: {
    resource: {
      id: 'ohscopilot'
    }
  }
}

// Cosmos DB containers
resource chunksContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-09-15' = {
  parent: cosmosDatabase
  name: 'chunks'
  properties: {
    resource: {
      id: 'chunks'
      partitionKey: {
        paths: ['/title']
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
  }
}

resource memoryContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2023-09-15' = {
  parent: cosmosDatabase
  name: 'memory'
  properties: {
    resource: {
      id: 'memory'
      partitionKey: {
        paths: ['/type']
        kind: 'Hash'
      }
    }
  }
}

// Log Analytics Workspace
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${resourcePrefix}-logs'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${resourcePrefix}-appinsights'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
  }
}

// Container Apps Environment
resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${resourcePrefix}-env'
  location: location
  tags: tags
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// Container App for OHS Copilot API
resource containerApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${resourcePrefix}-api'
  location: location
  tags: tags
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        allowInsecure: false
        traffic: [
          {
            weight: 100
            latestRevision: true
          }
        ]
      }
      secrets: [
        {
          name: 'aoai-endpoint'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/aoai-endpoint'
          identity: managedIdentity.id
        }
        {
          name: 'aoai-api-key'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/aoai-api-key'
          identity: managedIdentity.id
        }
        {
          name: 'cosmos-connection-string'
          keyVaultUrl: '${keyVault.properties.vaultUri}secrets/cosmos-connection-string'
          identity: managedIdentity.id
        }
      ]
    }
    template: {
      containers: [
        {
          image: containerImage
          name: 'ohs-copilot-api'
          resources: {
            cpu: json('1.0')
            memory: '2Gi'
          }
          env: [
            {
              name: 'VECTOR_STORE'
              value: 'cosmos'
            }
            {
              name: 'MEMORY_BACKEND'
              value: 'cosmos'
            }
            {
              name: 'AOAI_ENDPOINT'
              secretRef: 'aoai-endpoint'
            }
            {
              name: 'AOAI_API_KEY'
              secretRef: 'aoai-api-key'
            }
            {
              name: 'AOAI_CHAT_DEPLOYMENT'
              value: 'gpt-4'
            }
            {
              name: 'AOAI_EMB_DEPLOYMENT'
              value: 'text-embedding-ada-002'
            }
            {
              name: 'COSMOS_CONN_STR'
              secretRef: 'cosmos-connection-string'
            }
            {
              name: 'CONTENT_SAFETY_ENDPOINT'
              value: contentSafety.properties.endpoint
            }
            {
              name: 'CONTENT_SAFETY_KEY'
              value: contentSafety.listKeys().key1
            }
            {
              name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
              value: appInsights.properties.ConnectionString
            }
            {
              name: 'TELEMETRY_ENABLED'
              value: 'true'
            }
            {
              name: 'ASPNETCORE_ENVIRONMENT'
              value: 'Production'
            }
          ]
        }
      ]
      scale: {
        minReplicas: 1
        maxReplicas: 10
        rules: [
          {
            name: 'http-scaling'
            http: {
              metadata: {
                concurrentRequests: '100'
              }
            }
          }
        ]
      }
    }
  }
}

// Store secrets in Key Vault
resource aoaiEndpointSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'aoai-endpoint'
  properties: {
    value: cognitiveService.properties.endpoint
  }
}

resource aoaiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'aoai-api-key'
  properties: {
    value: cognitiveService.listKeys().key1
  }
}

resource cosmosConnectionSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'cosmos-connection-string'
  properties: {
    value: cosmosAccount.listConnectionStrings().connectionStrings[0].connectionString
  }
}

// Output important values
output apiUrl string = 'https://${containerApp.properties.configuration.ingress.fqdn}'
output keyVaultName string = keyVault.name
output openAiEndpoint string = cognitiveService.properties.endpoint
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
output appInsightsConnectionString string = appInsights.properties.ConnectionString
