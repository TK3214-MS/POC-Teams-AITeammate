#!/usr/bin/env bash
set -euo pipefail

# Entra ID App Registration Setup for AI Teammate
# Requires: Azure CLI (az) with logged-in session

echo "========================================="
echo " AI Teammate - Entra ID App Registration"
echo "========================================="

APP_NAME="AI Teammate Bot"
SIGN_IN_AUDIENCE="AzureADMultipleOrgs"

echo ""
echo "Creating Entra ID App Registration..."

# Create the app registration
APP_ID=$(az ad app create \
    --display-name "$APP_NAME" \
    --sign-in-audience "$SIGN_IN_AUDIENCE" \
    --web-redirect-uris "https://token.botframework.com/.auth/web/redirect" \
    --required-resource-accesses '[
        {
            "resourceAppId": "00000003-0000-0000-c000-000000000000",
            "resourceAccess": [
                { "id": "c9a497fc-a3d0-4e60-b1ae-0b4d44248b0d", "type": "Role" },
                { "id": "a]"]a2286e-7a14-4365-80e4-a5daa8e5e4d5", "type": "Role" },
                { "id": "294ce7c9-31ba-490a-ad7d-97a7d075e4ed", "type": "Role" },
                { "id": "df021288-bdef-4463-88db-98f22de89214", "type": "Role" },
                { "id": "45bbb07e-7321-4fd7-a8f6-3ff27e6a81c8", "type": "Role" },
                { "id": "a65f2972-a4f8-4f5e-afd7-69ccb046d5dc", "type": "Scope" },
                { "id": "9ff7295e-131b-4d94-90e1-69fde507ac11", "type": "Scope" }
            ]
        }
    ]' \
    --query appId -o tsv)

echo "  App ID: $APP_ID"

# Create a client secret
SECRET=$(az ad app credential reset \
    --id "$APP_ID" \
    --display-name "AI Teammate Secret" \
    --query password -o tsv)

echo "  Secret created (save this value securely)"

# Create service principal
az ad sp create --id "$APP_ID" > /dev/null 2>&1

echo ""
echo "========================================="
echo " Registration complete!"
echo ""
echo " Bot App ID:       $APP_ID"
echo " Bot App Password: $SECRET"
echo ""
echo " Add these to your appsettings.Development.json:"
echo "   Agents__MicrosoftAppId = $APP_ID"
echo "   Agents__MicrosoftAppPassword = $SECRET"
echo ""
echo " Next: Grant admin consent in Azure Portal:"
echo "   https://portal.azure.com/#view/Microsoft_AAD_RegisteredApps/ApplicationMenuBlade/~/CallAnAPI/appId/$APP_ID"
echo "========================================="
