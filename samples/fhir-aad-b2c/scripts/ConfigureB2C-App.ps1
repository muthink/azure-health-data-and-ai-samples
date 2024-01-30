
param (
    [Parameter(Mandatory=$true)]
    [string]$APPLICATION_NAME
)

# Define variables
$SCRIPT_PATH = Split-Path -parent $MyInvocation.MyCommand.Definition
$CallBackURL = "https://oauth.pstmn.io/v1/callback"
$OAuth2Permissions = "$SCRIPT_PATH/manifest-json-contents/oauth2-permissions.json"
$API_Permissions = "$SCRIPT_PATH/manifest-json-contents/Required-access.json"

# Get domain name
$DOMAIN_INFO=$(az rest --method get --url 'https://graph.microsoft.com/v1.0/domains?$select=id')
$DOMAIN_JSON = $DOMAIN_INFO | ConvertFrom-Json
$PRIMARY_DOMAIN = $DOMAIN_JSON.value[0].id

# Create app registration
$APP_ID = $(az ad app create --display-name $APPLICATION_NAME  --identifier-uris "https://$PRIMARY_DOMAIN/fhir" --public-client-redirect-uris $CallBackURL --query 'appId' --output tsv)

az ad sp create --id $APP_ID

$jsonContent = Get-Content -Path $API_Permissions | ConvertFrom-Json

$jsonContent[0].resourceAppId = $APP_ID

$jsonContent | ConvertTo-Json -Depth 10 | Set-Content -Path $API_Permissions -Force

# Update scopes

az ad app update --id $APP_ID --set api=@$OAuth2Permissions 

az ad app update --id $APP_ID --required-resource-accesses @$API_Permissions 

Start-Sleep -Seconds 5

az ad app permission admin-consent --id $APP_ID

Write-Host "Script Executed"