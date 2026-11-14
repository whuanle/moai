# AI Model Catalog via MoPlatform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use inline execution and complete each task in order.

**Goal:** Replace MoAI's bundled OpenCode catalog download with MoPlatform's published, normalized model catalog.

**Architecture:** Add one shared `MoAI:MoPlatform:Endpoint` setting. The admin catalog sync endpoint will fetch MoPlatform's latest pointer, download and verify the referenced `models.json`, persist it atomically under `configs/`, then invalidate the in-memory catalog. The catalog reader will parse MoPlatform schema v1 and will no longer depend on a bundled OpenCode JSON file.

**Tech Stack:** .NET 10, System.Text.Json, HttpClient, existing MoAI CQRS handler and local-dev E2E script.

---

### Task 1: Configure MoPlatform endpoint

**Files:**
- Modify: `src/infra/MoAI.Infra.Configuration.Shared/SystemOptions.cs`
- Modify: `src/MoAI/appsettings.Development.json`

- [ ] Add a nested MoPlatform endpoint setting under `MoAI`, defaulting to `https://anyai.wiki`.
- [ ] Set the development endpoint explicitly to `https://anyai.wiki`.

### Task 2: Read and persist MoPlatform catalog

**Files:**
- Modify: `src/aichannel/MoAI.AIChannel.Core/Commands/SyncAIModelCatalogCommandHandler.cs`
- Modify: `src/aichannel/MoAI.AIChannel.Shared/Commands/SyncAIModelCatalogCommand.cs`
- Modify: `src/aichannel/MoAI.AIChannel.Api/Controllers/AIModelController.cs`

- [ ] Fetch `/api/portal/catalog/models/latest` from the configured platform endpoint.
- [ ] Download its `url`, validate the declared size and SHA-256, validate schema v1, and atomically save to `configs/models.json`.
- [ ] Update user-facing comments and diagnostics to identify MoPlatform as the source.

### Task 3: Consume normalized catalog and remove OpenCode bundle

**Files:**
- Modify: `src/aichannel/MoAI.AIChannel.Core/Services/AIModelCatalogService.cs`
- Modify: `src/aichannel/MoAI.AIChannel.Shared/Models/AIChannelModelMeta.cs`
- Modify: `src/MoAI/MoAI.csproj`
- Delete: `src/MoAI/models.json`

- [ ] Parse MoPlatform schema v1 channel/model records into existing AI-channel metadata, while retaining legacy provider-group parsing for manually supplied configs during transition.
- [ ] Treat missing or invalid local catalog as no metadata rather than relying on a packaged OpenCode catalog.
- [ ] Remove the bundled catalog copy rule and file.

### Task 4: Update regression coverage

**Files:**
- Modify: `local-dev/model-catalog-e2e.mjs`

- [ ] Update baseline and corrupt-file assertions for the no-bundled-catalog behavior.
- [ ] Assert that online sync stores a valid MoPlatform schema document and immediately affects channel model recognition.
- [ ] Run backend build and the catalog E2E where the running development backend can reach `https://anyai.wiki`.
