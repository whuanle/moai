# MoPlatform Plugin Client Fingerprint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Execute inline, preserving existing worktree changes in both repositories.

**Goal:** Allow MoAI to request published MoPlatform plugin resources without a user login while isolating those endpoints from portal/admin/open APIs and applying client/IP throttling.

**Architecture:** MoPlatform adds a dedicated `/api/client/plugin/*` route area requiring a stable 256-bit `X-MoAI-Fingerprint`, with Redis-backed per-fingerprint and per-IP limits; the existing portal login download and admin APIs remain unchanged. MoAI persists a generated fingerprint under `configs/` and exposes a typed server-side plugin download client that sends the fingerprint.

**Tech Stack:** .NET 10, ASP.NET Core MVC middleware, MediatR, Redis, `IHttpClientFactory`, Node E2E.

---

### Task 1: Add a separately routed fingerprint client area in MoPlatform

**Files:**
- Modify: `F:/workspace/mo_platform/src/infra/MoPlatform.Infra.Api/ApiApplicationModelConvention.cs`
- Modify: `F:/workspace/mo_platform/src/MoPlatform/Filters/ApiAreaAuthorizationMiddleware.cs`
- Modify: `F:/workspace/mo_platform/src/MoPlatform/Modules/OpenApiModule.cs`
- Modify: `F:/workspace/mo_platform/src/MoPlatform/Program.cs`
- Create: `F:/workspace/mo_platform/src/plugin/MoPlatform.Plugin.Api/Controllers/ClientPluginController.cs`
- Create: `F:/workspace/mo_platform/src/plugin/MoPlatform.Plugin.Api/Services/PluginClientFingerprintGuard.cs`

- [x] Allow the `client/` route prefix and publish a separate client OpenAPI document.
- [x] Require valid 64-hex fingerprint headers and enforce Redis fixed-window limits by fingerprint and caller IP; use stricter limits for download URL issuance. The guard is registered by Maomi discovery, so the host module does not need explicit DI edits.
- [x] Expose client list/detail/manifest/download endpoints by reusing existing read/download handlers; keep portal login download and all admin routes untouched.

### Task 2: Generate and send a persistent MoAI fingerprint

**Files:**
- Create: `src/infra/MoAI.Infra.ExternalHttp/MoPlatform/MoPlatformClientFingerprint.cs`
- Create: `src/infra/MoAI.Infra.ExternalHttp/MoPlatform/MoPlatformPluginClient.cs`
- Modify: `src/infra/MoAI.Infra.ExternalHttp/InfraExternalHttpModule.cs`

- [x] Generate a random 256-bit fingerprint once and atomically persist it under `configs/`.
- [x] Implement typed list/detail/manifest/download clients calling `/api/client/plugin/*` with `X-MoAI-Client-Fingerprint`.

### Task 3: Add behavior documentation and regression coverage

**Files:**
- Modify: `F:/workspace/mo_platform/docs/plugin/{sdd,bdd,tdd,sop}.md`
- Modify: `F:/workspace/mo_platform/docs/api_interface.md`
- Modify: `F:/workspace/mo_platform/docs/README.md`
- Modify: `F:/workspace/mo_platform/local-dev/plugin-e2e.mjs`
- Modify: `docs/infra/{sdd,bdd,tdd,sop}.md`
- Modify: `docs/README.md`

- [x] Document client/API separation, fingerprint limits, and the fact the fingerprint is an identifier rather than a cryptographic credential.
- [x] Extend the platform plugin E2E to cover missing/malformed fingerprints, anonymous client download, valid signed URL, and unchanged portal login gate.
- [x] Build MoPlatform Release, run MoPlatform plugin unit tests, build MoAI, run MoAI infra tests, and check E2E syntax.
- [ ] Run `local-dev/plugin-e2e.mjs` after the active MoPlatform process on port 5100 is restarted with the new build; the current process serves no `/openapi/client.json` and locks its Debug output files.
