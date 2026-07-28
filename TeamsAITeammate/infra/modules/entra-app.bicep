// Note: Entra ID app registrations cannot be fully created via Bicep.
// This module documents the required configuration.
// Use the setup script (scripts/setup-entra-app.sh) or Azure Portal to create the app.

/*
Required Entra ID App Registration:
  Display Name: AI Teammate Bot
  Sign-in audience: AzureADMyOrg (Single-tenant)
  Redirect URI: https://token.botframework.com/.auth/web/redirect

Required API Permissions (Application):
  - Microsoft Graph:
    - OnlineMeetings.ReadWrite.All
    - OnlineMeetingTranscript.Read.All
    - Chat.ReadWrite.All
    - User.Read.All
    - CallRecords.Read.All

Required API Permissions (Delegated):
  - Microsoft Graph:
    - OnlineMeetings.ReadWrite
    - Chat.ReadWrite

Bot Channel Registration:
  - Messaging endpoint: https://<container-app-fqdn>/api/messages
  - Supported channels: Microsoft Teams
*/

// Placeholder output — the actual app ID comes from Entra ID registration
output note string = 'Entra ID app registration must be created manually or via CLI script. See scripts/setup-entra-app.sh'
