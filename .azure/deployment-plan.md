# Azure Deployment Plan

> **Status:** Deployed - Ready for Teams Live Test

Generated: 2026-07-28

## 1. Project Overview

**Goal:** Extend the existing development deployment so the Teams AI Teammate can be tested end to end in a real Teams meeting using browser-side microphone capture, Azure AI Speech transcription, persisted artifacts, AI analysis, and the SidePanel experience.

**Path:** Modify the existing AZD/Bicep deployment without replacing or deleting existing resources.

**Controlling hypothesis:** The application implementation is locally viable; the remaining end-to-end blockers are the missing Blob Storage and Speech resources/configuration plus missing Blob, Azure OpenAI, and AI Search data-plane permissions for the Container App managed identity.

**Discriminating checks:** Bicep/AZD preview must show only the planned additive/configuration changes, and authenticated post-deployment smoke tests must reach Speech, Blob Storage, Azure OpenAI, and AI Search without authorization failures.

## 2. Requirements

| Attribute | Value |
| --- | --- |
| Classification | Development / proof of concept |
| Scale | One Container App deployment; small-group Teams meeting test |
| Budget | Reuse existing services; add one Standard_LRS GPv2 Storage Account and one Speech S0 resource |
| Subscription | E7-Dev (`fbdb4c71-a589-42e4-9ede-4e56d2591523`), confirmed by user |
| Tenant | `889d19c6-b17d-4350-a3f0-95df5c3fffd5` |
| Resource group | `rg-aiteammate-dev` |
| Location | `japaneast`, confirmed by user |
| Audio constraint | No VM or VM-based audio processing; Azure AI Speech receives the local user's microphone audio from a Teams dialog |
| Privacy boundary | Do not claim or implement capture of every participant's Teams meeting audio |
| Secret handling | Never print or persist the Bot client secret in source, logs, command output, or this plan |

## 3. Components Detected

| Component | Technology | Deployment |
| --- | --- | --- |
| Agent API | .NET 10, ASP.NET Core, Microsoft 365 Agents SDK | Azure Container Apps |
| SidePanel and capture dialog | React 19, Vite, Teams JS, Speech SDK, SignalR | Built into Agent `wwwroot` and served by the same Container App |
| Meeting and analysis data | Cosmos DB | Existing account |
| Transcript/artifact files | Azure Blob Storage SDK | New Storage Account and containers |
| Speech recognition | Azure AI Speech SDK | New Speech S0 resource |
| Analysis | Azure OpenAI | Existing account and deployments |
| Retrieval | Azure AI Search | Existing service |
| Secrets | Azure Key Vault | Existing vault; update secret versions only |
| Identity | User-assigned managed identity | Existing identity; add scoped data-plane roles |

## 4. Recipe Selection

**Selected:** Existing AZD workflow with Bicep infrastructure and Container App deployment.

**Rationale:** `TeamsAITeammate/azure.yaml`, the Docker build, and modular Bicep deployment already own the development environment. The change should extend those artifacts rather than introduce a parallel deployment path.

## 5. Planned Architecture Changes

| Scope | Planned Change | Security / Operational Notes |
| --- | --- | --- |
| Storage | Add one StorageV2 `Standard_LRS` account and required private Blob containers | Disable public blob access; use managed identity, not account keys |
| Container App | Add `BlobStorage__Endpoint` and retain existing Speech endpoint/region/key settings | Endpoint is non-secret; Speech key remains a Key Vault reference |
| Speech | Provision the existing Bicep-defined Speech S0 account in Japan East | Browser dialog supplies only the local microphone stream |
| Blob RBAC | Assign Storage Blob Data Contributor to the Container App identity at the new account scope | Allows application read/write/delete of its blobs without account keys |
| OpenAI RBAC | Assign Cognitive Services OpenAI User to the Container App identity at the existing OpenAI account scope | Data-plane inference only |
| Search RBAC | Assign Search Index Data Contributor at the existing Search service scope | The application performs document operations; its `SearchIndexClient` is currently unused, so Search Service Contributor is not required |
| Search index | Validate `knowledge-index` and create it only when absent through an explicit bootstrap script | Indexes are AI Search data-plane objects, not ARM/Bicep resources; the script never overwrites an existing index and does not grant the app index-management rights |
| Key Vault | Store Speech key as a new secret version and rotate the Bot client secret without exposing it | Existing managed identity secret access remains in place |
| Entra / Teams SSO | Preserve single-tenant app ID and verify Application ID URI, delegated scope, redirect URLs, and Teams manifest values | No new tenant or app registration |
| Teams package | Regenerate package with the deployed HTTPS hostname and existing app IDs | Upload only after deployment verification |

No VM, AKS, audio relay server, or separate frontend hosting resource will be introduced.

## 6. Provisioning Limit Evidence

The direct quota command did not return within 120 seconds and was terminated. The fallback uses Azure Resource Graph inventory plus published Microsoft limits.

| Resource Type | Current in Japan East | Add | Total After | Published Limit / Default | Assessment |
| --- | ---: | ---: | ---: | --- | --- |
| Standard-endpoint Storage Accounts | 0 | 1 | 1 | 250 per subscription per region by default | Sufficient |
| Azure AI Speech resources | 0 | 1 | 1 | 100 resources of one AI Services type per region | Sufficient |
| All Cognitive Services accounts | 1 OpenAI | 1 Speech | 2 | 200 mixed AI Services resources per region | Sufficient |
| Speech S0 real-time STT concurrency | N/A before creation | 1 resource | 100 default concurrent base-model requests | 100 per Speech S0 resource, adjustable | Sufficient for a small meeting test |
| Azure RBAC assignments | Existing count not returned | Up to 4 | Existing + up to 4 | 4,000 per subscription | Low-risk; validate during preview |

Evidence sources:

- Azure Resource Graph query across the approved subscription and `japaneast` returned one `Microsoft.CognitiveServices/accounts` resource of kind `OpenAI` and no Storage or Speech resources.
- Microsoft Learn: Azure subscription and service limits, Azure Storage limits, Foundry Tools limits, and Azure RBAC limits.
- Microsoft Learn: Azure Speech quotas and limits; Speech S0 defaults to 100 concurrent real-time base-model requests.

The actual create operations and role assignments remain gated by `azd provision --preview` and Azure validation. No quota increase is planned.

## 7. Validation Proof

Validated on 2026-07-28 against subscription `fbdb4c71-a589-42e4-9ede-4e56d2591523`, resource group `rg-aiteammate-dev`, and `japaneast`.

| Check | Result |
| --- | --- |
| `azure.yaml` stable-schema validation | Passed |
| `main.bicep` compilation | Passed with zero diagnostics |
| Dev, staging, and prod `.bicepparam` compilation | Passed with zero diagnostics |
| Dev deployment snapshot | Passed with zero diagnostics; predicted resource names match the existing Dev environment |
| Azure Policy | One inherited West Europe restriction; no conflict with Japan East |
| `dotnet build TeamsAITeammate.slnx --no-restore` | Passed, 0 warnings and 0 errors |
| Unit tests | Passed, 410 passed, 0 failed, 0 skipped |
| SidePanel `npm run build` | Passed |
| Search bootstrap syntax/schema checks | Passed; direct execution returns the expected usage guard when arguments are absent |
| Docker build context | Passed; SidePanel `package-lock.json` is present for `npm ci` |
| `azd package --no-prompt` | Passed |
| `azd provision --preview --no-prompt` | Passed after preserving existing Cosmos DB automatic failover; 0 deletes and 0 replacements |

Preview summary:

- Creates the Speech S0 account and secure Storage Account.
- Modifies the Container App runtime configuration to add Speech/Blob settings and the planned identity-backed access.
- Preserves existing Cosmos DB automatic failover.
- Does not delete or replace Cosmos DB, Key Vault, OpenAI, AI Search, Container Apps Environment, managed identity, Container App, or another stateful resource.
- Remaining omitted-property entries correspond to current service defaults or the Key Vault child access-policy resource that re-applies the existing managed identity `get/list` policy.

### Role Assignment Verification

- Identity checked: existing user-assigned Container App identity.
- Cosmos DB Built-in Data Contributor is scoped to the Cosmos DB account.
- AcrPull is scoped to the Container Registry.
- Storage Blob Data Contributor is scoped to the new Storage Account.
- Cognitive Services OpenAI User is scoped to the OpenAI account.
- Search Index Data Contributor is scoped to the AI Search service.
- Key Vault access remains a single access policy granting only `get/list` secrets to the application identity.
- No subscription-wide or resource-group-wide application role was added.

### Deployment Evidence

Deployed and verified on 2026-07-29.

| Check | Result |
| --- | --- |
| `azd provision --no-prompt` | Passed; approved infrastructure changes applied |
| `azd deploy --no-prompt` | Passed; Agent and SidePanel image built remotely and deployed |
| Active revision | `aiteammate-dev-mfxwfpuo7stza-app--azd-1785332293`; Healthy, Running, one replica, 100% traffic |
| Public endpoint | `GET /` returned 200 |
| Health endpoint | `GET /healthz` returned 200 and `Healthy` |
| Bot endpoint boundary | Empty unauthenticated `POST /api/messages` returned 400 from activity validation rather than the SPA fallback |
| Blob Storage | `knowledge` and `transcripts` exist with no public access; shared-key access disabled, OAuth default enabled, TLS 1.2 minimum |
| Key Vault references | Bot credential and Speech key are identity-backed Container App secret references; no secret values were read |
| Azure AI Search | Existing `knowledge-index` passed required field, 3072-dimension vector profile, and semantic configuration validation |
| Speech | SpeechServices S0 exists in Japan East; no VM-based audio resource is present |
| Cosmos DB | Automatic failover remains enabled |
| Entra SSO | `api://e8ce26fe-665b-4e51-bb7b-8483d7bb0f08` and enabled `access_as_user` scope configured; current Microsoft Teams desktop/mobile and web clients pre-authorized |
| Azure Bot | Teams channel enabled and endpoint matches the deployed `/api/messages` URL |
| Teams package | `ai-teammate-app.zip` regenerated; packaged manifest checksum matches the source and ZIP integrity passed |
| Unit tests after health correction | 411 passed, 0 failed, 0 skipped |

The setup script's obsolete Teams Web client ID was replaced with the current Microsoft Teams Web Client application ID observed in the live Teams authorization flow. Authenticated downstream operations remain part of the meeting E2E test because they require a real Teams user/session context.

### Execution Checklist

### Phase 1: Planning

- [x] Analyze workspace and existing Azure architecture
- [x] Confirm subscription, tenant, resource group, and location
- [x] Inventory relevant resources without reading secrets
- [x] Document quota API timeout and fallback evidence
- [x] Select existing AZD/Bicep recipe
- [x] Finalize this deployment plan
- [x] Receive explicit user approval

### Phase 2: Preparation

- [x] Add Storage Account and Blob containers to Bicep
- [x] Pass the Blob service endpoint to the Container App
- [x] Add scoped Blob, OpenAI, and Search data-plane role assignments
- [x] Add and validate the non-destructive `knowledge-index` bootstrap
- [x] Verify stable resource names and no replacement of existing stateful resources
- [x] Build Bicep and both application surfaces
- [x] Run focused and full automated tests
- [x] Set plan status to `Ready for Validation`

### Phase 3: Validation

- [x] Invoke azure-validate
- [x] Compile all Bicep and parameter files
- [x] Run `azd provision --preview --no-prompt`
- [x] Confirm preview contains no deletion or replacement of existing stateful resources
- [x] Verify role scopes and least privilege statically
- [x] Record validation proof and set status to `Validated`

### Phase 4: Deployment

- [x] Invoke azure-deploy
- [x] Rotate the Bot client secret through a non-echoing workflow and update Key Vault/AZD environment securely
- [x] Apply approved infrastructure changes
- [x] Build and deploy the Agent/SidePanel container
- [x] Verify healthy Container App revision and startup logs
- [ ] Run authenticated service smoke tests
- [x] Update Entra SSO settings only where deployed URLs require it
- [x] Regenerate the Teams app package
- [ ] Upload the Teams app package

### Phase 5: Teams Live Test

- [ ] Open the SidePanel in a Teams meeting
- [ ] Start the capture dialog and grant microphone permission
- [ ] Confirm finalized local speech appears as transcript data
- [ ] Confirm persistence, AI analysis, and SignalR insight delivery
- [ ] Confirm stop/leave lifecycle and error handling
- [ ] Record observed limitations and test evidence without storing sensitive meeting content in the plan

## 8. Validation and Safety Gates

Deployment must stop if any of the following occurs:

- Preview proposes deletion or replacement of Cosmos DB, Key Vault, OpenAI, AI Search, Container Apps Environment, or the existing managed identity.
- Resource names resolve differently from the current Dev environment.
- A role assignment is broader than the owning resource scope without a documented need.
- A secret appears in stdout, generated files, Git diff, or command history.
- Build, tests, Bicep compilation, or AZD preview fails.
- Speech or Teams SSO configuration would imply unsupported all-participant audio capture.

Rollback strategy:

1. Route Container App traffic back to the previous healthy revision if application deployment fails.
2. Restore the prior active Bot secret version if credential rotation causes authentication failure.
3. Remove only the newly added role assignments or app settings if they cause authorization/configuration regressions.
4. Do not delete newly created Storage or Speech resources automatically; preserve them for diagnosis and require explicit approval for deletion.

## 9. Expected Files

| File | Planned Change |
| --- | --- |
| `.azure/deployment-plan.md` | Current source of truth for prepare, validate, and deploy |
| `TeamsAITeammate/infra/main.bicep` | Wire Storage, endpoints, and service-scoped RBAC |
| `TeamsAITeammate/infra/modules/storage.bicep` | Define secure StorageV2 account and Blob containers |
| `TeamsAITeammate/infra/modules/container-app.bicep` | Add Blob endpoint runtime configuration |
| `TeamsAITeammate/infra/search/knowledge-index.json` | Define the application-compatible Search index schema |
| `TeamsAITeammate/scripts/ensure-search-index.sh` | Validate or create the Search index without persisting its admin key |
| `TeamsAITeammate/appPackage/manifest.json` | Update only if deployed hostname or SSO URL values differ |

Exact file changes may be narrowed after approval, but the Azure resource and permission scope may not be expanded without updating this plan and obtaining approval again.

## 10. Prior Deployment History

The earlier `/api/messages` code-only deployment completed successfully on 2026-07-28. It registered the Bot endpoint, deployed a healthy Container App revision, and added the Cosmos DB Built-in Data Contributor assignment after a live 403/5301 diagnosis. That completed change is retained in Git history; it is not the approval basis for this infrastructure update.

## 11. Approval

> Approved by the user with “承認して続行”. Validation, infrastructure provisioning, application deployment, and desktop Teams SSO preparation are complete. Teams custom app upload and the meeting E2E runbook remain.
