# Wiki API Kiota Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Route every MoAI Wiki backend request through the generated Kiota client so authentication, 401 handling, error parsing, and user feedback are consistent.

**Architecture:** Keep `src/api/wiki.ts` as the stable page-facing facade and replace its private `fetch` helpers with generated request builders obtained from `getApiClient()`. Native `fetch` remains only for non-MoAI resources: static catalog files, backend-provided download URLs, and presigned object-storage uploads.

**Tech Stack:** TypeScript 5.7, React 19, Microsoft Kiota TypeScript runtime, Vitest 4

---

### Task 1: Add Wiki facade contract tests

**Files:**
- Create: `src/api/__tests__/wiki.test.ts`

- [ ] Mock `getApiClient()` with nested Wiki request builders and import the public functions from `src/api/wiki.ts`.
- [ ] Verify document listing calls `client.api.wiki.byId(String(wikiId)).documents.list.post()` with paging and filter fields.
- [ ] Verify delete and rename convert numeric IDs to strings and call the generated `delete` and `rename.put` builders.
- [ ] Verify embedding, partition, metadata generation, model options, and configuration use their generated builders and unwrap `value` responses.
- [ ] Run `npm test -- src/api/__tests__/wiki.test.ts`; expect failures while the facade still calls native `fetch`.

### Task 2: Migrate Wiki backend requests

**Files:**
- Modify: `src/api/wiki.ts`
- Test: `src/api/__tests__/wiki.test.ts`

- [ ] Remove `Env`, `useAppStore`, `parseApiErrorResponse`, `authedFetch`, and `postJson` from `src/api/wiki.ts`.
- [ ] Route document list, preupload, completion, deletion, download URL, rename, embedding detail/trigger, extraction, content, partitioning, metadata generation, embedding configuration, rerank configuration, and model options through `getApiClient()`.
- [ ] Convert route and `int64` body IDs with `String(...)`; preserve facade defaults and current return types.
- [ ] Keep `metadataModelId` in the public embedding trigger payload for caller compatibility, but do not send it because the synchronized `EmbeddingDocumentCommand` no longer contains that field.
- [ ] Use generated enum literal types for partition settings and let TypeScript reject unsupported values.
- [ ] Run `npm test -- src/api/__tests__/wiki.test.ts`; expect all facade contract tests to pass.

### Task 3: Verify unified error routing and allowed native fetches

**Files:**
- Test: `src/design-system/components/Feedback/__tests__/feedback.test.ts`
- Verify: `src/api/kiota.ts`, `src/api/wiki.ts`, `src/pages/wiki/WikiDocuments.tsx`, `src/utils/storage.ts`, `src/utils/pluginFile.ts`

- [ ] Run the Feedback tests to confirm business, server, and network errors still route to the expected message/notification channels.
- [ ] Search source code for native `fetch`; confirm no MoAI backend URL is assembled manually.
- [ ] Run `npm run typecheck`, `npm run lint`, and the Wiki page tests.
- [ ] Review the final diff for generated-client edits; `src/api/client/**` may contain the user's sync output but must not be manually changed by this migration.
