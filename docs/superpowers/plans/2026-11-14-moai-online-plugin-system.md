# MoAI Online Plugin System Implementation Plan

> **For agentic workers:** Execute inline task-by-task with verification checkpoints. This plan spans the MoAI and MoPlatform repositories; preserve pre-existing working-tree changes in both.

**Goal:** Replace MoAI's built-in dynamic plugin templates with MoPlatform-distributed plugin packages that admins can install, persist under `configs/plugins`, enable/disable without unloading, and reuse locally stored package metadata and OSS icons.

**Architecture:** MoPlatform exposes a dedicated anonymous plugin-client route area, separated from admin APIs, with a persistent client fingerprint required and fingerprint/IP throttling. MoAI's plugin domain obtains marketplace metadata and signed package downloads through this API, verifies and safely installs packages to `configs/plugins/{key}`, uploads the package icon to MoAI storage, persists installation state and local metadata, then loads enabled assemblies into the existing registry via a shared-contract-aware `AssemblyLoadContext`; offlining only removes the plugin from active discovery/execution and never deletes files or unloads assemblies.

**Tech Stack:** .NET 10, Maomi modules, MediatR/CQRS, EF Core/PostgreSQL DB-first scaffold, S3-compatible `IStorageService`, `AssemblyLoadContext`/`AssemblyDependencyResolver`, React 19/TypeScript/antd/Kiota, MoPlatform OpenAPI and local E2E scripts.

---

## File map

### MoPlatform (`F:\workspace\mo_platform`)
- `src/infra/MoPlatform.Infra.Api/ApiApplicationModelConvention.cs` — allow the dedicated `client/` API zone without weakening admin/open authorization.
- `src/plugin/MoPlatform.Plugin.Shared/{Commands,Queries}/` — validated catalog and package-download messages/DTOs.
- `src/plugin/MoPlatform.Plugin.Core/Handlers/` — published-only catalog queries and short-lived package URL issuance; reuse platform storage and download event infrastructure.
- `src/plugin/MoPlatform.Plugin.Api/Controllers/PluginClientController.cs` — isolated `/api/client/plugin` anonymous endpoints; require fingerprint header at this boundary.
- `src/plugin/MoPlatform.Plugin.Core/Services/` and platform service registration — fingerprint validation and bounded per-fingerprint/per-IP throttling.
- `docs/plugin/{sdd,bdd,tdd,sop,protocol}.md`, `docs/api_interface.md`, `local-dev/plugin-e2e.mjs`, plugin tests — contract and evidence.

### MoAI (`F:\workspace\moai`)
- `src/aiplugin/MoAI.AIPlugin.Shared/` — installed-plugin DTOs and install/online/offline/list commands.
- `src/aiplugin/MoAI.AIPlugin.Core/Services/` — MoPlatform client, safe package installer, metadata reader, non-unloadable load context, startup package loader, online-state-aware registry.
- `src/aiplugin/MoAI.AIPlugin.Core/{Commands,Queries}/` and `src/aiplugin/MoAI.AIPlugin.Api/Controllers/PluginController.cs` — admin install/list/offline/online/catalog endpoints.
- `src/database/MoAI.Database.Shared/Entities/PluginPackageEntity.cs`, database context/configuration and `sql/migrations/` — installed package source/version/hash/path/manifest/enabled state. Use the repository's DB-first scaffold workflow for generated artifacts.
- `src/MoAI/{MainModule.cs,appsettings*.json,MoAI.csproj}` — remove built-in dynamic module from host composition, configure MoPlatform client, plugin directory and persistent fingerprint.
- `ui/src/pages/plugins/Plugins.tsx`, `ui/src/api/plugin.ts`, page tests and zh/en locale files — online marketplace and installed package state/actions.
- `docs/aiplugin-dynamic/{sdd,bdd,tdd,sop}.md`, `docs/api_interface.md`, E2E evidence and `AGENTS.md` only where current behavior references change.

## Task 1: Freeze and test MoPlatform client API contract

- Add `client/` as a separately documented API area; it must not inherit administrator claims or reuse `/api/admin/plugin` routes.
- Add endpoints to page published plugins, get published plugin detail/current manifest, and issue a short-lived package URL bound to an explicit current `versionId`.
- Require a bounded-format `X-MoAI-Client-Fingerprint` value on every client endpoint; reject missing/invalid values and apply rate limits keyed by both fingerprint and remote IP. Do not expose admin-only data or offline packages.
- Preserve existing admin/portal/open behavior and the in-progress current-package changes in `mo_platform`.
- Extend `local-dev/plugin-e2e.mjs` and platform tests for route isolation, missing fingerprint, fingerprint/IP throttling, published-only results, exact version binding, and offline denial.

## Task 2: Implement MoAI package installation and startup loading

- Add validated commands for catalog browse, install by platform plugin/version identifier, installed package listing, online, and offline. No delete/uninstall API or file removal is added.
- Add a MoPlatform client reading only `MoPlatform:Endpoint` plus a persisted random installation fingerprint from `configs/plugins/.client-fingerprint`; make first-run creation atomic and restrictive to the service account.
- For installation, fetch metadata and the short-lived URL server-side; validate plugin key, version, size ceiling, SHA-256, supported manifest schema/TFM/minimum client version; download to a temporary file, validate ZIP entry paths/count/expanded size and rootPath, then atomically replace `configs/plugins/{key}` only after all checks pass.
- Read `plugin.json` and root icon from the verified archive. Upload icon bytes to MoAI storage via `IStorageService`, save title/description/avatar object key and package provenance in DB, and store local display metadata in the installed directory so no later UI read depends on MoPlatform resources.
- Add a startup loader scanning only immediate child directories of `configs/plugins`, requiring key-directory/descriptor agreement, then load enabled packages through a dedicated ALC. Resolve MoAI plugin contracts and shared framework assemblies from the default context; use `AssemblyDependencyResolver` for private dependencies. Report a bad package without stopping other packages or host startup.
- The loader must not support unloading. Offline immediately blocks registry listing and execution but retains package files and loaded assembly; online reuses the already-loaded assembly or loads the installed package once.
- Remove `DynamicPluginsModule` from `MainModule` so its old built-in templates stop registering; retain source/project history and compatibility for old DB rows rather than deleting user data. Ensure the existing in-progress multi-tool changes continue to work for package assemblies.
- Add tests for fingerprint creation/reuse, package integrity/path/size validation, restart discovery, duplicate keys, shared contract identity, offline execution denial, no-uninstall semantics, and startup fault isolation.

## Task 3: Add admin marketplace UI and regression coverage

- Add an “online plugins” tab to the existing plugin management page: searchable published catalog, icon/name/summary/version/description, install action, installed/enabled state, and online/offline actions. Existing custom/static management tabs remain intact.
- Keep all API calls in `ui/src/api/plugin.ts`, use Kiota-backed MoAI APIs (no page-level fetch), use design-system components/tokens, and add matching zh-CN/en-US strings.
- Add page tests for available/installing/installed/offline/error states and assert that no uninstall action is presented.
- Regenerate Kiota client only after API shape stabilizes, then run typecheck/lint/tests.

## Task 4: Synchronize docs and verify both repositories

- Update both plugin-domain BDD/TDD/SDD/SOP suites with new never-reused scenario numbers, route/security and local installation contracts; synchronize MoPlatform plugin protocol and API interface, and MoAI API interface. Preserve and build on the current uncommitted documentation edits.
- Record the explicit security boundary: fingerprint and rate limits deter accidental/low-effort scraping but do not authenticate a client or prevent a distributed attacker from inventing fingerprints; stronger anti-abuse requires a provisioned secret, allowlist, or authenticated entitlement.
- Verify MoAI backend build/plugin tests/package E2E and frontend typecheck/lint/tests. Verify MoPlatform build/plugin tests/client API E2E. Do not run deployment migrations against shared services without explicit confirmation.

## Acceptance checklist

- [ ] MoPlatform passwordless client API is isolated from admin and requires fingerprint on each request.
- [ ] MoAI admins can browse/install an online plugin; package/icon/description are retained locally and in MoAI DB/OSS.
- [ ] Packages are safely verified and extracted under `configs/plugins/{key}` and automatically rediscovered after restart.
- [ ] Offline prevents discovery and execution while retaining installed files; no uninstall or dynamic unload exists.
- [ ] Existing custom/static plugins, multi-tool plugin support, team grants, and APIs remain compatible.
- [ ] Both repositories' docs and automated evidence map all new scenarios.
