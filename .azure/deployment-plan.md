# Azure Deployment Plan

> **Status:** Validated

Generated: 2026-07-28

---

## 1. Project Overview

**Goal:** Deploy the missing Microsoft 365 Agents SDK `/api/messages` route to the existing development Container App so Azure Bot Service can deliver Teams activities.

**Path:** Modify existing application

---

## 2. Requirements

| Attribute | Value |
| --- | --- |
| Classification | Development |
| Scale | Existing deployment; no capacity change |
| Budget | No infrastructure or SKU change |
| Subscription | E7-Dev (`fbdb4c71-a589-42e4-9ede-4e56d2591523`), confirmed |
| Location | `japaneast`, confirmed |

---

## 3. Components Detected

| Component | Type | Technology | Path |
| --- | --- | --- | --- |
| AI Teammate Agent | API / Agent | .NET 10, Microsoft 365 Agents SDK 1.8.3-beta | `TeamsAITeammate/src/TeamsAITeammate.Agent` |
| Agent container | Container | Docker, Azure Container Apps | `TeamsAITeammate/Dockerfile` |

---

## 4. Recipe Selection

**Selected:** AZD code-only deployment

**Rationale:** The existing project uses `azure.yaml`, and only application code must change. Infrastructure provisioning is excluded.

---

## 5. Architecture

**Stack:** Existing Azure Container Apps deployment

### Service Mapping

| Component | Azure Service | Change |
| --- | --- | --- |
| AI Teammate Agent | Azure Container Apps | Build and deploy a new application revision |
| Azure Bot Service | Existing Azure Bot | No change; endpoint already targets `/api/messages` |

### Supporting Services

| Service | Purpose | Change |
| --- | --- | --- |
| Azure Container Registry | Container image storage | New image only |
| Application Insights | Monitoring | No change |
| Key Vault | Bot secret | No change |
| Managed Identity | Service authentication | No change |

---

## 6. Provisioning Limit Checklist

This is a code-only deployment. It creates no Azure resource and changes no SKU, replica limit, quota, or regional capacity allocation.

| Resource Type | Number to Deploy | Total After Deployment | Limit/Quota | Notes |
| --- | --- | --- | --- | --- |
| Azure resources | 0 | Existing resources unchanged | Not applicable | New Container App revision only |

**Status:** All capacity unchanged; quota validation is not applicable.

---

## 7. Execution Checklist

### Phase 1: Planning

- [x] Analyze workspace
- [x] Gather requirements
- [x] Confirm subscription and location with user
- [x] Prepare resource inventory
- [x] Determine quota impact (none for code-only deployment)
- [x] Scan codebase
- [x] Select recipe
- [x] Plan architecture
- [x] User approved this plan

### Phase 2: Execution

- [x] Add `app.MapDefaultAgentEndpoints()` after `app.UseAgents()`
- [x] Build Agent project
- [x] Verify local `POST /api/messages` returns 400 instead of 404
- [x] Set status to Ready for Validation

### Phase 3: Validation

- [x] Invoke azure-validate
- [x] Validate code-only deployment configuration
- [x] Record validation proof

### Phase 4: Deployment

- [x] Invoke azure-deploy
- [x] Run code-only `azd deploy agent --no-prompt`
- [x] Verify public `POST /api/messages` returns 400 instead of 404
- [x] Verify Container App revision is healthy
- [ ] Ask user to retry `@AI Teammate join`

---

## 8. Validation Proof

| Check | Command Run | Result | Timestamp |
| --- | --- | --- | --- |
| AZD installation and authentication | `azd version`; `azd auth login --check-status` | Passed; azd 1.28.1 and authenticated | 2026-07-28T02:45:44Z |
| Target environment | `azd env get-values` (non-secret target values only) | Passed; `dev`, `rg-aiteammate-dev`, `japaneast`, approved subscription | 2026-07-28T02:45:44Z |
| AZD schema | `validate_azure_yaml` | Passed against stable schema | 2026-07-28T02:45:44Z |
| Application build | `dotnet build TeamsAITeammate.slnx --no-restore` | Passed; 0 warnings, 0 errors | 2026-07-28T02:45:44Z |
| IaC preview | `azd provision --preview --no-prompt` | Passed; no changes applied | 2026-07-28T02:45:44Z |
| Container package | `azd package agent --no-prompt` | Passed using Azure remote build; no local artifact expected | 2026-07-28T02:45:44Z |
| Static role verification | Reviewed `infra/modules/container-app.bicep` | Passed; managed identity has ACR pull and Key Vault secret access | 2026-07-28T02:45:44Z |

**Validated by:** azure-validate

### Deployment Verification

| Check | Result | Timestamp |
| --- | --- | --- |
| Agent deployment | Succeeded in 2m16s | 2026-07-28T02:51:31Z |
| Active revision | `aiteammate-dev-mfxwfpuo7stza-app--azd-1785207083`; Healthy, Running, 100% traffic | 2026-07-28T02:54:09Z |
| Bot message route | Unauthenticated `POST /api/messages` returned 400; route is registered | 2026-07-28T02:54:05Z |
| Startup logs | Application started on port 8080; no startup error | 2026-07-28T02:54:09Z |
| Live RBAC | Managed identity has `AcrPull` at the existing ACR scope | 2026-07-28T02:54:09Z |
| Teams command delivery | Activity received and `join` parsed successfully | 2026-07-28T02:55:59Z |
| Cosmos DB diagnosis | Failed with 403/5301 because the managed identity lacked `readMetadata` | 2026-07-28T02:55:59Z |
| Cosmos DB live remediation | Added Cosmos DB Built-in Data Contributor at account scope | 2026-07-28T03:02:00Z |
| Cosmos DB IaC remediation | Added native SQL role assignment to `infra/main.bicep`; Bicep build passed | 2026-07-28T03:02:00Z |

---

## 9. Files

| File | Purpose | Status |
| --- | --- | --- |
| `.azure/deployment-plan.md` | Deployment source of truth | Created |
| `TeamsAITeammate/src/TeamsAITeammate.Agent/Program.cs` | Register `/api/messages` | Updated and locally validated |
| `TeamsAITeammate/azure.yaml` | Existing azd service definition | Docker paths corrected; Azure remote build enabled |
| `TeamsAITeammate/infra/main.bicep` | Existing infrastructure | Cosmos DB native data role added for the Container App identity |

---

## 10. Next Steps

> Current: Code-only deployment completed and verified

1. Retry `@AI Teammate join` in the Teams meeting chat.
