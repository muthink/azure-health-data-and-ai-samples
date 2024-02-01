param createWorkspace bool
param createFhirService bool
param workspaceName string
param fhirServiceName string
param tenantId string
param location string
param audience string = ''
param appTags object = {}

var loginURL = environment().authentication.loginEndpoint
var authority = '${loginURL}${tenantId}'
var resolvedAudience = length(audience) > 0 ? audience :  'https://${workspaceName}-${fhirServiceName}.fhir.azurehealthcareapis.com'

resource healthWorkspace 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' = if (createWorkspace) {
  name: workspaceName
  location: location
  tags: appTags
}

resource healthWorkspaceExisting 'Microsoft.HealthcareApis/workspaces@2021-06-01-preview' existing = if (!createWorkspace) {
  name: workspaceName
}
var newOrExistingWorkspaceName = createWorkspace ? healthWorkspace.name : healthWorkspaceExisting.name

resource fhir 'Microsoft.HealthcareApis/workspaces/fhirservices@2021-06-01-preview' = if (createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
  location: location
  kind: 'fhir-R4'

  identity: {
    type: 'SystemAssigned'
  }

  properties: {
    authenticationConfiguration: {
      authority: authority
      audience: resolvedAudience
      smartProxyEnabled: false
      smartIdentityProviders: [
        {
            authority: 'https://fhirb2ctenantdemo.b2clogin.com/fhirb2ctenantdemo.onmicrosoft.com/B2C_1_USER_FLOW_SIGN_IN_1/v2.0'
            applications: [
                {
                    clientId: 'a198006d-ced2-447d-a25c-b21f3162f201'
                    audience: 'a198006d-ced2-447d-a25c-b21f3162f201'
                    allowedDataActions: [
                        'Read'
                    ]
                }
            ]
        }
      ]
    }
  }

  tags: appTags
}

resource fhirExisting 'Microsoft.HealthcareApis/workspaces/fhirservices@2021-06-01-preview' existing = if (!createFhirService) {
  name: '${newOrExistingWorkspaceName}/${fhirServiceName}'
}

output fhirId string = createFhirService ? fhir.id : fhirExisting.id
#disable-next-line BCP053
output fhirIdentity string = createFhirService ? fhir.identity.principalId : fhirExisting.identity.principalId
