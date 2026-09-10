# 知识图谱模块（前端）实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为 MoAI 前端新增知识图谱模块：`/kg` 只读卡片墙、团队详情「知识图谱」tab（管理）、`/team/:teamId/kg/:graphId/:section?` 工作台（实体 / 关系 / 模型 Schema / 设置），v1 以列表表单维护节点与边。

**Architecture:** React 19 + antd 5（经 `@/design-system`）+ Kiota 生成客户端（经手写封装 `src/api/knowledgeGraph.ts`）+ zustand 团队上下文 + i18next。工作台左侧 `Menu`，右侧按 section 渲染子组件；实体/关系用 `DataTable` + `Modal` 表单，Schema 用两张表 + 表单，设置用 `Form`。全部文案双语；危险操作用 `Popconfirm`。

**Tech Stack:** React 19、TypeScript、Vite、antd 5、react-router 7、zustand、i18next、Kiota（preview.93）、vitest + Testing Library。

**前置条件：** 后端 [知识图谱后端计划](./2026-09-10-knowledge-graph-backend.md) 已完成并 `dotnet run`，执行 `cd ui && npm run syncapi` 生成客户端。

**关联真源：** [设计稿](../specs/2026-09-10-knowledge-graph-design.md) ｜ [前端规范](../../../ui/docs/frontend-conventions.md) ｜ [design-system](../../../ui/docs/design-system/README.md)

---

## 文件结构

**新增**
- `ui/src/api/knowledgeGraph.ts` — Kiota 封装
- `ui/src/api/__tests__/knowledgeGraph.test.ts`
- `ui/src/pages/knowledgegraph/KnowledgeGraphList.tsx` — `/kg` 卡片墙
- `ui/src/pages/knowledgegraph/KnowledgeGraphDetail.tsx` — 工作台外壳
- `ui/src/pages/knowledgegraph/KnowledgeGraphEntities.tsx` — 实体列表
- `ui/src/pages/knowledgegraph/KnowledgeGraphRelations.tsx` — 关系列表
- `ui/src/pages/knowledgegraph/KnowledgeGraphSchema.tsx` — schema 管理
- `ui/src/pages/knowledgegraph/KnowledgeGraphSettings.tsx` — 图谱设置
- `ui/src/pages/teams/knowledgegraph/TeamKnowledgeGraphs.tsx` — 团队 tab
- `ui/src/pages/knowledgegraph/__tests__/*.test.tsx`

**修改**
- `ui/src/router/index.tsx` — 路由
- `ui/src/pages/teams/TeamManage.tsx` — 团队 tab
- `ui/src/i18n/locales/zh-CN/common.json`、`en-US/common.json` — 文案

---

## Task 1: API 封装

**Files:**
- Create: `ui/src/api/knowledgeGraph.ts`
- Test: `ui/src/api/__tests__/knowledgeGraph.test.ts`

- [ ] **Step 1: 先执行 syncapi 生成客户端**

Run: `cd ui && npm run syncapi`
Expected: `src/api/client` 重新生成，出现 `knowledgeGraph` 路径与 `byId`、`nodes`、`edges`、`entityTypes`、`relationTypes`、`schema`、`templates` 构建器

> 生成后先确认方法名（`client.api.knowledgeGraph.byId(...).nodes.byNodeId(...)` 等），若 Kiota 命名与下文不同，以生成物为准调整。

- [ ] **Step 2: 写失败测试**

`ui/src/api/__tests__/knowledgeGraph.test.ts`（用 `vi.hoisted` 伪造生成物，风格对齐 `wiki.test.ts`）：

```ts
import { describe, expect, it, vi, beforeEach } from 'vitest'

const mocks = vi.hoisted(() => ({
  listGet: vi.fn(),
  createPost: vi.fn(),
  detailGet: vi.fn(),
  deleteFn: vi.fn(),
  schemaGet: vi.fn(),
  nodesListPost: vi.fn(),
  nodesPost: vi.fn(),
  nodePut: vi.fn(),
  nodeDelete: vi.fn(),
}))

vi.mock('@/api/kiota', () => ({
  getApiClient: () => ({
    api: {
      knowledgeGraph: {
        list: { get: mocks.listGet },
        post: mocks.createPost,
        templates: { get: vi.fn().mockResolvedValue({ items: [] }) },
        byId: () => ({
          get: mocks.detailGet,
          delete: mocks.deleteFn,
          schema: { get: mocks.schemaGet },
          nodes: {
            list: { post: mocks.nodesListPost },
            post: mocks.nodesPost,
            byNodeId: () => ({ put: mocks.nodePut, delete: mocks.nodeDelete }),
          },
        }),
      },
    },
  }),
}))

import { getKnowledgeGraphs, createKnowledgeGraph, deleteKnowledgeGraph, getKnowledgeGraphNodes, createKnowledgeGraphNode } from '../knowledgeGraph'

describe('knowledgeGraph api', () => {
  beforeEach(() => vi.clearAllMocks())

  it('getKnowledgeGraphs 传递 teamId 字符串并归一 items', async () => {
    mocks.listGet.mockResolvedValue({ teamId: '7', myRole: 2, enabled: true, items: [{ kgId: '1', name: '域图' }] })
    const res = await getKnowledgeGraphs(7)
    expect(mocks.listGet).toHaveBeenCalledWith({ queryParameters: { teamId: '7' } })
    expect(res.items?.[0].name).toBe('域图')
    expect(res.enabled).toBe(true)
  })

  it('deleteKnowledgeGraph 使用字符串 id', async () => {
    mocks.deleteFn.mockResolvedValue(undefined)
    await deleteKnowledgeGraph(3)
    expect(mocks.deleteFn).toHaveBeenCalled()
  })

  it('getKnowledgeGraphNodes 归一 total', async () => {
    mocks.nodesListPost.mockResolvedValue({ total: '5', items: [] })
    const res = await getKnowledgeGraphNodes(1, { pageNo: 1, pageSize: 20 })
    expect(res.total).toBe(5)
  })
})
```

- [ ] **Step 3: 运行确认失败**

Run: `cd ui && npx vitest run src/api/__tests__/knowledgeGraph.test.ts`
Expected: FAIL（模块不存在）

- [ ] **Step 4: 实现封装**

`ui/src/api/knowledgeGraph.ts`：

```ts
import { getApiClient } from '@/api/kiota'

export interface KnowledgeGraphItem {
  kgId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  templateKey?: string | null
  createTime?: string | null
}

export interface KnowledgeGraphListResult {
  teamId?: string | number | null
  myRole?: number | null
  enabled?: boolean | null
  items?: KnowledgeGraphItem[] | null
}

export interface KnowledgeGraphDetail {
  kgId?: string | number | null
  teamId?: string | number | null
  name?: string | null
  description?: string | null
  templateKey?: string | null
  myRole?: number | null
  enabled?: boolean | null
  createTime?: string | null
}

export interface KnowledgeGraphTemplateItem {
  key?: string | null
  name?: string | null
  description?: string | null
  entityTypes?: string[] | null
  relationTypes?: string[] | null
}

export interface KnowledgeGraphEntityTypeItem {
  entityTypeId?: string | number | null
  name?: string | null
  color?: string | null
  description?: string | null
}

export interface KnowledgeGraphRelationTypeItem {
  relationTypeId?: string | number | null
  name?: string | null
  color?: string | null
  description?: string | null
  sourceTypeId?: string | number | null
  targetTypeId?: string | number | null
}

export interface KnowledgeGraphSchema {
  entityTypes?: KnowledgeGraphEntityTypeItem[] | null
  relationTypes?: KnowledgeGraphRelationTypeItem[] | null
}

export interface KnowledgeGraphNodeItem {
  nodeId?: string | null
  entityTypeId?: string | number | null
  name?: string | null
  description?: string | null
}

export interface KnowledgeGraphEdgeItem {
  edgeId?: string | null
  relationTypeId?: string | number | null
  sourceNodeId?: string | null
  targetNodeId?: string | null
}

export interface PagedResult<T> { items?: T[] | null; total?: string | number | null }

export async function getKnowledgeGraphs(teamId: number): Promise<KnowledgeGraphListResult> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.list.get({ queryParameters: { teamId: String(teamId) } })
  return { teamId: res?.teamId, myRole: res?.myRole, enabled: res?.enabled ?? false, items: res?.items ?? [] }
}

export async function createKnowledgeGraph(payload: { teamId: number; name: string; description?: string; templateKey?: string | null }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    templateKey: payload.templateKey ?? undefined,
  })
  return Number(res?.value ?? 0)
}

export async function getKnowledgeGraphTemplates(): Promise<KnowledgeGraphTemplateItem[]> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.templates.get()
  return res?.items ?? []
}

export async function getKnowledgeGraphDetail(kgId: number): Promise<KnowledgeGraphDetail> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).get()
  return res ?? {}
}

export async function updateKnowledgeGraph(kgId: number, payload: { name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).put({ name: payload.name, description: payload.description })
}

export async function deleteKnowledgeGraph(kgId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).delete()
}

export async function getKnowledgeGraphSchema(kgId: number): Promise<KnowledgeGraphSchema> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).schema.get()
  return { entityTypes: res?.entityTypes ?? [], relationTypes: res?.relationTypes ?? [] }
}

export async function createEntityType(kgId: number, payload: { name: string; color?: string; description?: string }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.post(payload)
  return Number(res?.value ?? 0)
}

export async function updateEntityType(kgId: number, entityTypeId: number, payload: { name: string; color?: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.byTypeId(String(entityTypeId)).put(payload)
}

export async function deleteEntityType(kgId: number, entityTypeId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).entityTypes.byTypeId(String(entityTypeId)).delete()
}

export async function createRelationType(kgId: number, payload: { name: string; color?: string; description?: string; sourceTypeId?: number | null; targetTypeId?: number | null }): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).relationTypes.post({
    ...payload,
    sourceTypeId: payload.sourceTypeId != null ? String(payload.sourceTypeId) : undefined,
    targetTypeId: payload.targetTypeId != null ? String(payload.targetTypeId) : undefined,
  })
  return Number(res?.value ?? 0)
}

export async function updateRelationType(kgId: number, relationTypeId: number, payload: { name: string; color?: string; description?: string; sourceTypeId?: number | null; targetTypeId?: number | null }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).relationTypes.byTypeId(String(relationTypeId)).put({
    ...payload,
    sourceTypeId: payload.sourceTypeId != null ? String(payload.sourceTypeId) : undefined,
    targetTypeId: payload.targetTypeId != null ? String(payload.targetTypeId) : undefined,
  })
}

export async function deleteRelationType(kgId: number, relationTypeId: number): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).relationTypes.byTypeId(String(relationTypeId)).delete()
}

export async function getKnowledgeGraphNodes(
  kgId: number,
  params: { entityTypeId?: number | null; keyword?: string; pageNo?: number; pageSize?: number },
): Promise<PagedResult<KnowledgeGraphNodeItem>> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).nodes.list.post({
    entityTypeId: params.entityTypeId != null ? String(params.entityTypeId) : undefined,
    keyword: params.keyword,
    pageNo: params.pageNo ?? 1,
    pageSize: params.pageSize ?? 20,
  })
  return { items: res?.items ?? [], total: Number(res?.total ?? 0) }
}

export async function createKnowledgeGraphNode(kgId: number, payload: { entityTypeId: number; name: string; description?: string }): Promise<string> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).nodes.post({ entityTypeId: String(payload.entityTypeId), name: payload.name, description: payload.description })
  return String(res?.value ?? '')
}

export async function updateKnowledgeGraphNode(kgId: number, nodeId: string, payload: { entityTypeId: number; name: string; description?: string }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).nodes.byNodeId(nodeId).put({ entityTypeId: String(payload.entityTypeId), name: payload.name, description: payload.description })
}

export async function deleteKnowledgeGraphNode(kgId: number, nodeId: string): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).nodes.byNodeId(nodeId).delete()
}

export async function getKnowledgeGraphEdges(
  kgId: number,
  params: { relationTypeId?: number | null; nodeId?: string; pageNo?: number; pageSize?: number },
): Promise<PagedResult<KnowledgeGraphEdgeItem>> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).edges.list.post({
    relationTypeId: params.relationTypeId != null ? String(params.relationTypeId) : undefined,
    nodeId: params.nodeId,
    pageNo: params.pageNo ?? 1,
    pageSize: params.pageSize ?? 20,
  })
  return { items: res?.items ?? [], total: Number(res?.total ?? 0) }
}

export async function createKnowledgeGraphEdge(kgId: number, payload: { relationTypeId: number; sourceNodeId: string; targetNodeId: string }): Promise<string> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.byId(String(kgId)).edges.post({
    relationTypeId: String(payload.relationTypeId),
    sourceNodeId: payload.sourceNodeId,
    targetNodeId: payload.targetNodeId,
  })
  return String(res?.value ?? '')
}

export async function updateKnowledgeGraphEdge(kgId: number, edgeId: string, payload: { relationTypeId: number }): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).edges.byEdgeId(edgeId).put({ relationTypeId: String(payload.relationTypeId) })
}

export async function deleteKnowledgeGraphEdge(kgId: number, edgeId: string): Promise<void> {
  const client = getApiClient()
  await client.api.knowledgeGraph.byId(String(kgId)).edges.byEdgeId(edgeId).delete()
}
```

- [ ] **Step 5: 运行测试通过 + 类型检查**

Run: `cd ui && npx vitest run src/api/__tests__/knowledgeGraph.test.ts && npm run typecheck`
Expected: PASS / 0 error

- [ ] **Step 6: 提交**

```bash
git add ui/src/api/knowledgeGraph.ts ui/src/api/__tests__/knowledgeGraph.test.ts ui/src/api/client
git commit -m "feat(knowledge-graph-ui): API 封装"
```

---

## Task 2: i18n

**Files:**
- Modify: `ui/src/i18n/locales/zh-CN/common.json`
- Modify: `ui/src/i18n/locales/en-US/common.json`

- [ ] **Step 1: 增加 `knowledgegraph` 块（zh-CN）**

在 zh-CN `common.json` 顶层（`wiki` 块之后）追加：

```json
"knowledgegraph": {
  "title": "知识图谱",
  "create": "新建知识图谱",
  "createTitle": "新建知识图谱",
  "editTitle": "编辑知识图谱",
  "name": "名称",
  "namePlaceholder": "请输入图谱名称",
  "desc": "简介",
  "template": "模板",
  "templateBlank": "空白 / 自定义",
  "edit": "编辑",
  "delete": "删除",
  "deleteConfirm": "确认删除该知识图谱？其中的节点与关系将一并清除。",
  "createSuccess": "创建成功",
  "saveSuccess": "保存成功",
  "deleteSuccess": "删除成功",
  "empty": "还没有知识图谱，请在所属团队下新建一个",
  "notFound": "知识图谱不存在或已被删除",
  "disabled": "未开启知识图谱能力，请让超级管理员在系统设置中配置 Neo4j",
  "menuEntities": "实体",
  "menuRelations": "关系",
  "menuSchema": "模型",
  "menuSettings": "设置",
  "entity": {
    "create": "新建实体",
    "edit": "编辑实体",
    "colName": "名称",
    "colType": "类型",
    "colDesc": "描述",
    "namePlaceholder": "请输入实体名称",
    "typePlaceholder": "请选择实体类型",
    "deleteConfirm": "确认删除该实体？其关联的关系也会被删除。"
  },
  "relation": {
    "create": "新建关系",
    "edit": "编辑关系",
    "colSource": "起点",
    "colType": "关系",
    "colTarget": "终点",
    "sourcePlaceholder": "请选择起点实体",
    "targetPlaceholder": "请选择终点实体",
    "typePlaceholder": "请选择关系类型",
    "deleteConfirm": "确认删除该关系？"
  },
  "schema": {
    "title": "模型 / Schema",
    "entityTypes": "实体类型",
    "relationTypes": "关系类型",
    "addEntityType": "新增实体类型",
    "addRelationType": "新增关系类型",
    "name": "名称",
    "color": "颜色",
    "desc": "描述",
    "sourceType": "起点类型",
    "targetType": "终点类型",
    "anyType": "任意",
    "deleteEntityConfirm": "确认删除该实体类型？",
    "deleteRelationConfirm": "确认删除该关系类型？"
  },
  "settings": {
    "basicTitle": "基础信息",
    "templateKey": "创建模板",
    "createTime": "创建时间"
  },
  "tabLabel": "知识图谱",
  "noPermission": "仅团队管理员可管理知识图谱与模型"
}
```

- [ ] **Step 2: 同步 en-US**

在 en-US `common.json` 追加同结构英文块（key 完全一致）：

```json
"knowledgegraph": {
  "title": "Knowledge Graph",
  "create": "New Knowledge Graph",
  "createTitle": "New Knowledge Graph",
  "editTitle": "Edit Knowledge Graph",
  "name": "Name",
  "namePlaceholder": "Enter graph name",
  "desc": "Description",
  "template": "Template",
  "templateBlank": "Blank / Custom",
  "edit": "Edit",
  "delete": "Delete",
  "deleteConfirm": "Delete this knowledge graph? Its nodes and relations will be removed too.",
  "createSuccess": "Created",
  "saveSuccess": "Saved",
  "deleteSuccess": "Deleted",
  "empty": "No knowledge graph yet. Create one under your team.",
  "notFound": "Knowledge graph not found or removed",
  "disabled": "Knowledge graph is disabled. Ask the super admin to configure Neo4j in system settings.",
  "menuEntities": "Entities",
  "menuRelations": "Relations",
  "menuSchema": "Schema",
  "menuSettings": "Settings",
  "entity": {
    "create": "New Entity",
    "edit": "Edit Entity",
    "colName": "Name",
    "colType": "Type",
    "colDesc": "Description",
    "namePlaceholder": "Enter entity name",
    "typePlaceholder": "Select entity type",
    "deleteConfirm": "Delete this entity? Its relations will be removed too."
  },
  "relation": {
    "create": "New Relation",
    "edit": "Edit Relation",
    "colSource": "Source",
    "colType": "Relation",
    "colTarget": "Target",
    "sourcePlaceholder": "Select source entity",
    "targetPlaceholder": "Select target entity",
    "typePlaceholder": "Select relation type",
    "deleteConfirm": "Delete this relation?"
  },
  "schema": {
    "title": "Schema",
    "entityTypes": "Entity Types",
    "relationTypes": "Relation Types",
    "addEntityType": "Add Entity Type",
    "addRelationType": "Add Relation Type",
    "name": "Name",
    "color": "Color",
    "desc": "Description",
    "sourceType": "Source Type",
    "targetType": "Target Type",
    "anyType": "Any",
    "deleteEntityConfirm": "Delete this entity type?",
    "deleteRelationConfirm": "Delete this relation type?"
  },
  "settings": {
    "basicTitle": "Basic Info",
    "templateKey": "Template",
    "createTime": "Created"
  },
  "tabLabel": "Knowledge Graph",
  "noPermission": "Only team admins can manage knowledge graphs and schema"
}
```

- [ ] **Step 3: 提交**

```bash
git add ui/src/i18n
git commit -m "feat(knowledge-graph-ui): 文案（zh/en）"
```

---

## Task 3: 路由与 `/kg` 卡片墙

**Files:**
- Modify: `ui/src/router/index.tsx`
- Create: `ui/src/pages/knowledgegraph/KnowledgeGraphList.tsx`
- Test: `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphList.test.tsx`

- [ ] **Step 1: 路由**

`ui/src/router/index.tsx` 顶部 import 区追加：

```tsx
import { KnowledgeGraphList } from '@/pages/knowledgegraph/KnowledgeGraphList'
import { KnowledgeGraphDetail } from '@/pages/knowledgegraph/KnowledgeGraphDetail'
```

children 中 `wiki` 路由附近追加：

```tsx
{ path: 'kg', element: <KnowledgeGraphList /> },
{ path: 'team/:teamId/kg/:graphId/:section?', element: <KnowledgeGraphDetail /> },
```

- [ ] **Step 2: 写失败测试**

`ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphList.test.tsx`：

```tsx
import { describe, expect, it, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router'
import { KnowledgeGraphList } from '../KnowledgeGraphList'
import { useAppStore } from '@/store/app'
import { getKnowledgeGraphs } from '@/api/knowledgeGraph'
import { getMyTeams } from '@/api/team'

vi.mock('@/api/knowledgeGraph', () => ({
  getKnowledgeGraphs: vi.fn(),
}))
vi.mock('@/api/team', () => ({ getMyTeams: vi.fn().mockResolvedValue([]) }))
vi.mock('@/api/kiota', () => ({ getApiClient: vi.fn(() => ({})) }))

function renderPage() {
  return render(<MemoryRouter><KnowledgeGraphList /></MemoryRouter>)
}

describe('KnowledgeGraphList', () => {
  beforeEach(() => {
    vi.clearAllMocks()
    useAppStore.setState({
      userInfo: { accessToken: 't', userId: '1', userName: 'o' },
      currentTeamId: null,
      myTeams: [{ teamId: '7', name: 'Alpha', myRole: 2 }],
    })
  })

  it('聚合团队下的知识图谱卡片', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({
      teamId: '7', myRole: 2, enabled: true,
      items: [{ kgId: '1', teamId: '7', name: '支付域图谱', description: '运维' }],
    })
    renderPage()
    expect(await screen.findByText('支付域图谱')).toBeInTheDocument()
    expect(getKnowledgeGraphs).toHaveBeenCalledWith(7)
  })

  it('未开启能力时显示提示', async () => {
    vi.mocked(getKnowledgeGraphs).mockResolvedValue({ teamId: '7', myRole: 2, enabled: false, items: [] })
    renderPage()
    expect(await screen.findByText(/未开启知识图谱能力/)).toBeInTheDocument()
  })
})
```

- [ ] **Step 3: 运行确认失败**

Run: `cd ui && npx vitest run src/pages/knowledgegraph/__tests__/KnowledgeGraphList.test.tsx`
Expected: FAIL

- [ ] **Step 4: 实现页面**

`ui/src/pages/knowledgegraph/KnowledgeGraphList.tsx`（镜像 `pages/wiki/Wiki.tsx` 的聚合逻辑，纯只读；增加 `enabled` 提示）：

```tsx
import { useCallback, useEffect, useState } from 'react'
import { useNavigate } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Card, Empty, Page } from '@/design-system'
import { neutralColors, spacing } from '@/design-system/theme'
import { useAppStore } from '@/store/app'
import { getMyTeams, type TeamItem } from '@/api/team'
import { getKnowledgeGraphs, type KnowledgeGraphItem } from '@/api/knowledgeGraph'

interface CardItem extends KnowledgeGraphItem {
  teamName?: string
  myRole?: number | null
  graphEnabled?: boolean
}

export function KnowledgeGraphList() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const myTeams = useAppStore((state) => state.myTeams)
  const setMyTeams = useAppStore((state) => state.setMyTeams)
  const [items, setItems] = useState<CardItem[]>([])
  const [enabled, setEnabled] = useState(true)

  const load = useCallback(async () => {
    const teams: TeamItem[] = myTeams.length > 0 ? myTeams : await getMyTeams()
    if (myTeams.length === 0) setMyTeams(teams)
    const collected: CardItem[] = []
    let anyEnabled = false
    for (const team of teams) {
      const teamId = Number(team.teamId)
      if (!teamId) continue
      try {
        const res = await getKnowledgeGraphs(teamId)
        if (res.enabled) anyEnabled = true
        for (const g of res.items ?? []) {
          collected.push({ ...g, teamName: team.name ?? undefined, myRole: res.myRole, graphEnabled: res.enabled ?? false })
        }
      } catch {
        // 单个团队失败不中断
      }
    }
    setEnabled(anyEnabled)
    setItems(collected)
  }, [myTeams, setMyTeams])

  useEffect(() => { void load() }, [load])

  return (
    <Page>
      {!enabled && (
        <Alert type="warning" showIcon message={t('knowledgegraph.disabled')} style={{ marginBottom: spacing.md }} />
      )}
      {items.length === 0 ? (
        <Empty description={t('knowledgegraph.empty')} />
      ) : (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(260px, 1fr))', gap: spacing.md }}>
          {items.map((item) => (
            <Card
              key={String(item.kgId)}
              hoverable
              onClick={() => navigate(`/team/${item.teamId}/kg/${item.kgId}/entities`)}
            >
              <div style={{ fontSize: 16, fontWeight: 600 }}>{item.name}</div>
              <div style={{ color: neutralColors.textSecondary, marginTop: spacing.xs }}>{item.description || '-'}</div>
              <div style={{ color: neutralColors.textSecondary, marginTop: spacing.sm, fontSize: 12 }}>{item.teamName}</div>
            </Card>
          ))}
        </div>
      )}
    </Page>
  )
}
```

> 若 `@/design-system` 未导出 `Empty`/`Alert`，改用其导出的对应封装或 antd 原生（禁止直接 import antd `Table`/`Form`，`Empty`/`Alert` 不受限）。实施时以 `ui/src/design-system/components/index.ts` 导出为准。

- [ ] **Step 5: 测试通过 + 类型检查**

Run: `cd ui && npx vitest run src/pages/knowledgegraph/__tests__/KnowledgeGraphList.test.tsx && npm run typecheck`
Expected: PASS / 0 error

- [ ] **Step 6: 提交**

```bash
git add ui/src/router/index.tsx ui/src/pages/knowledgegraph
git commit -m "feat(knowledge-graph-ui): /kg 卡片墙与路由"
```

---

## Task 4: 团队详情「知识图谱」tab

**Files:**
- Create: `ui/src/pages/teams/knowledgegraph/TeamKnowledgeGraphs.tsx`
- Modify: `ui/src/pages/teams/TeamManage.tsx`

- [ ] **Step 1: 实现 TeamKnowledgeGraphs**

镜像 `pages/teams/wikis/TeamWikis.tsx`（`Modal` + `Popconfirm` + `Form` + 模板选择）：

```tsx
import { useCallback, useEffect, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, Card, Col, Form, Input, Modal, Popconfirm, Row, Space, feedback } from '@/design-system'
import { EditOutlined, DeleteOutlined, PlusOutlined } from '@ant-design/icons'
import {
  createKnowledgeGraph, deleteKnowledgeGraph, getKnowledgeGraphTemplates,
  getKnowledgeGraphs, updateKnowledgeGraph, type KnowledgeGraphItem, type KnowledgeGraphTemplateItem,
} from '@/api/knowledgeGraph'

const ROLE_MEMBER = 0

interface FormValues { name: string; description?: string; templateKey?: string }

export function TeamKnowledgeGraphs({ teamId }: { teamId: number }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<KnowledgeGraphItem[]>([])
  const [myRole, setMyRole] = useState<number | null>(null)
  const [enabled, setEnabled] = useState(true)
  const [templates, setTemplates] = useState<KnowledgeGraphTemplateItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<FormValues>()
  const isAdminPlus = myRole !== null && myRole !== ROLE_MEMBER

  const load = useCallback(async () => {
    const res = await getKnowledgeGraphs(teamId)
    setItems(res.items ?? [])
    setMyRole(res.myRole ?? null)
    setEnabled(res.enabled ?? false)
  }, [teamId])

  useEffect(() => { void load() }, [load])
  useEffect(() => {
    if (enabled) void getKnowledgeGraphTemplates().then(setTemplates)
  }, [enabled])

  const openCreate = () => {
    setEditing(null)
    form.resetFields()
    setOpen(true)
  }
  const openEdit = (record: KnowledgeGraphItem) => {
    setEditing(record)
    form.setFieldsValue({ name: record.name ?? '', description: record.description ?? undefined })
    setOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing) {
        await updateKnowledgeGraph(Number(editing.kgId), { name: values.name, description: values.description })
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createKnowledgeGraph({ teamId, name: values.name, description: values.description, templateKey: values.templateKey })
        feedback.success(t('knowledgegraph.createSuccess'))
      }
      setOpen(false)
      void load()
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: KnowledgeGraphItem) => {
    await deleteKnowledgeGraph(Number(record.kgId))
    feedback.success(t('knowledgegraph.deleteSuccess'))
    void load()
  }

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        {isAdminPlus && (
          <Button type="primary" icon={<PlusOutlined />} disabled={!enabled} onClick={openCreate}>
            {t('knowledgegraph.create')}
          </Button>
        )}
      </Space>
      {!enabled && <div style={{ marginBottom: 16 }}>{t('knowledgegraph.disabled')}</div>}
      <Row gutter={[16, 16]}>
        {items.map((item) => (
          <Col key={String(item.kgId)} xs={24} sm={12} md={8} lg={6}>
            <Card>
              <div style={{ fontWeight: 600 }}>{item.name}</div>
              <div style={{ opacity: 0.65, minHeight: 22 }}>{item.description || '-'}</div>
              {isAdminPlus && (
                <Space>
                  <Button type="text" size="small" icon={<EditOutlined />} aria-label={t('knowledgegraph.edit')} onClick={() => openEdit(item)} />
                  <Popconfirm title={t('knowledgegraph.deleteConfirm')} onConfirm={() => void handleDelete(item)}>
                    <Button type="text" size="small" danger icon={<DeleteOutlined />} aria-label={t('knowledgegraph.delete')} />
                  </Popconfirm>
                </Space>
              )}
            </Card>
          </Col>
        ))}
      </Row>

      <Modal
        open={open}
        title={editing ? t('knowledgegraph.editTitle') : t('knowledgegraph.createTitle')}
        onOk={() => void handleSubmit()}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="name" label={t('knowledgegraph.name')} rules={[{ required: true, message: t('knowledgegraph.namePlaceholder') }]}>
            <Input maxLength={50} placeholder={t('knowledgegraph.namePlaceholder')} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.desc')}>
            <Input.TextArea maxLength={255} rows={2} />
          </Form.Item>
          {!editing && (
            <Form.Item name="templateKey" label={t('knowledgegraph.template')} initialValue="blank">
              <Select
                options={templates.map((x) => ({
                  value: x.key ?? '',
                  label: `${x.name}${x.entityTypes?.length ? `（${x.entityTypes.join(' / ')}）` : ''}`,
                }))}
              />
            </Form.Item>
          )}
        </Form>
      </Modal>
    </>
  )
}
```

> `Select` 需从 `@/design-system` 导入（若未封装，用 antd `Select`；`Select` 不在禁止列表，与 `Table`/`Form` 不同）。`feedback`、`Modal`、`Popconfirm` 均来自 `@/design-system`。

- [ ] **Step 2: 注册团队 tab**

`ui/src/pages/teams/TeamManage.tsx`：

找到 `SECTION_KEYS`（约 line 53），在 `'knowledge'` 后追加 `'kg'`：

```tsx
const SECTION_KEYS = ['info', 'members', 'gateway', 'knowledge', 'kg', 'plugins', 'variables', 'settings'] as const
```

`menuItems`（约 line 354）在 `knowledge` 之后追加：

```tsx
{ key: 'kg', icon: <ShareAltOutlined />, label: t('knowledgegraph.tabLabel') },
```

（`ShareAltOutlined` 从 `@ant-design/icons` 引入；也可复用 `BookOutlined`。）

内容渲染分支（约 line 445，`knowledge` 分支后）追加：

```tsx
) : section === 'kg' ? (
  <DSCard styles={{ body: { padding: spacing.lg } }}>
    <TeamKnowledgeGraphs teamId={teamId} />
  </DSCard>
```

并在文件顶部 import `TeamKnowledgeGraphs`。

- [ ] **Step 3: typecheck + lint**

Run: `cd ui && npm run typecheck && npm run lint`
Expected: 0 error

- [ ] **Step 4: 提交**

```bash
git add ui/src/pages/teams
git commit -m "feat(knowledge-graph-ui): 团队知识图谱 tab"
```

---

## Task 5: 工作台外壳

**Files:**
- Create: `ui/src/pages/knowledgegraph/KnowledgeGraphDetail.tsx`
- Test: `ui/src/pages/knowledgegraph/__tests__/KnowledgeGraphDetail.test.tsx`

- [ ] **Step 1: 实现外壳**

镜像 `pages/wiki/WikiDetail.tsx` 的 `Page` + `Layout/Sider/Menu` 结构：

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router'
import { useTranslation } from 'react-i18next'
import { Alert, Card, Layout, Menu, Page } from '@/design-system'
import { spacing } from '@/design-system/theme'
import { ApartmentOutlined, DeploymentUnitOutlined, ProfileOutlined, SettingOutlined } from '@ant-design/icons'
import { getKnowledgeGraphDetail, type KnowledgeGraphDetail as GraphDetail } from '@/api/knowledgeGraph'
import { KnowledgeGraphEntities } from './KnowledgeGraphEntities'
import { KnowledgeGraphRelations } from './KnowledgeGraphRelations'
import { KnowledgeGraphSchema } from './KnowledgeGraphSchema'
import { KnowledgeGraphSettings } from './KnowledgeGraphSettings'

const SECTION_KEYS = ['entities', 'relations', 'schema', 'settings'] as const
type SectionKey = (typeof SECTION_KEYS)[number]

export function KnowledgeGraphDetail() {
  const { t } = useTranslation()
  const navigate = useNavigate()
  const params = useParams<{ teamId: string; graphId: string; section?: string }>()
  const teamId = Number(params.teamId)
  const graphId = Number(params.graphId)
  const rawSection = params.section ?? 'entities'
  const section: SectionKey = SECTION_KEYS.includes(rawSection as SectionKey) ? (rawSection as SectionKey) : 'entities'
  const [graph, setGraph] = useState<GraphDetail | null>(null)

  const load = useCallback(async () => {
    setGraph(await getKnowledgeGraphDetail(graphId))
  }, [graphId])

  useEffect(() => { void load() }, [load])

  const menuItems = useMemo(() => [
    { key: 'entities', icon: <ProfileOutlined />, label: t('knowledgegraph.menuEntities') },
    { key: 'relations', icon: <DeploymentUnitOutlined />, label: t('knowledgegraph.menuRelations') },
    { key: 'schema', icon: <ApartmentOutlined />, label: t('knowledgegraph.menuSchema') },
    { key: 'settings', icon: <SettingOutlined />, label: t('knowledgegraph.menuSettings') },
  ], [t])

  return (
    <Page breadcrumb={[{ title: <Link to={`/team/${teamId}`}>{t('knowledgegraph.title')}</Link> }, { title: graph?.name ?? '' }]}>
      {graph && graph.enabled === false && (
        <Alert type="warning" showIcon message={t('knowledgegraph.disabled')} style={{ marginBottom: spacing.md }} />
      )}
      <Layout style={{ background: 'transparent', gap: spacing.md }}>
        <Sider width={200}>
          <Menu mode="inline" items={menuItems} selectedKeys={[section]} onClick={({ key }) => navigate(`/team/${teamId}/kg/${graphId}/${key}`)} />
        </Sider>
        <Layout.Content>
          {section === 'entities' && <KnowledgeGraphEntities graphId={graphId} graphEnabled={graph?.enabled !== false} />}
          {section === 'relations' && <KnowledgeGraphRelations graphId={graphId} />}
          {section === 'schema' && <KnowledgeGraphSchema graphId={graphId} myRole={graph?.myRole ?? null} />}
          {section === 'settings' && <KnowledgeGraphSettings graph={graph} onChanged={load} />}
        </Layout.Content>
      </Layout>
    </Page>
  )
}
```

- [ ] **Step 2: 渲染测试**

`KnowledgeGraphDetail.test.tsx`：mock `@/api/knowledgeGraph` 的 `getKnowledgeGraphDetail` 及相关子组件 API，断言语义菜单与「未开启」提示。测试用 `MemoryRouter initialEntries={['/team/7/kg/1/entities']}` 并提供 `Route`。

- [ ] **Step 3: 提交**

```bash
git add ui/src/pages/knowledgegraph/KnowledgeGraphDetail.tsx ui/src/pages/knowledgegraph/__tests__
git commit -m "feat(knowledge-graph-ui): 工作台外壳"
```

---

## Task 6: 实体列表（实体页）

**Files:**
- Create: `ui/src/pages/knowledgegraph/KnowledgeGraphEntities.tsx`

- [ ] **Step 1: 实现**

```tsx
import { useCallback, useEffect, useMemo, useState } from 'react'
import { useTranslation } from 'react-i18next'
import { Button, DataTable, Form, Input, Modal, Popconfirm, Select, Space, feedback } from '@/design-system'
import { PlusOutlined } from '@ant-design/icons'
import {
  createKnowledgeGraphNode, deleteKnowledgeGraphNode, getKnowledgeGraphNodes,
  updateKnowledgeGraphNode, getKnowledgeGraphSchema, type KnowledgeGraphNodeItem, type KnowledgeGraphEntityTypeItem,
} from '@/api/knowledgeGraph'

interface FormValues { entityTypeId: number; name: string; description?: string }

export function KnowledgeGraphEntities({ graphId, graphEnabled }: { graphId: number; graphEnabled: boolean }) {
  const { t } = useTranslation()
  const [items, setItems] = useState<KnowledgeGraphNodeItem[]>([])
  const [total, setTotal] = useState(0)
  const [pageNo, setPageNo] = useState(1)
  const [keyword, setKeyword] = useState('')
  const [entityTypes, setEntityTypes] = useState<KnowledgeGraphEntityTypeItem[]>([])
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<KnowledgeGraphNodeItem | null>(null)
  const [saving, setSaving] = useState(false)
  const [form] = Form.useForm<FormValues>()
  const pageSize = 20

  const load = useCallback(async () => {
    const res = await getKnowledgeGraphNodes(graphId, { keyword, pageNo, pageSize })
    setItems(res.items ?? [])
    setTotal(Number(res.total ?? 0))
  }, [graphId, keyword, pageNo])

  const loadSchema = useCallback(async () => {
    const schema = await getKnowledgeGraphSchema(graphId)
    setEntityTypes(schema.entityTypes ?? [])
  }, [graphId])

  useEffect(() => { void load() }, [load])
  useEffect(() => { void loadSchema() }, [loadSchema])

  const typeName = useMemo(() => {
    const map = new Map<number, string>()
    for (const x of entityTypes) map.set(Number(x.entityTypeId), x.name ?? '')
    return map
  }, [entityTypes])

  const openCreate = () => { setEditing(null); form.resetFields(); setOpen(true) }
  const openEdit = (record: KnowledgeGraphNodeItem) => {
    setEditing(record)
    form.setFieldsValue({ entityTypeId: Number(record.entityTypeId), name: record.name ?? '', description: record.description ?? undefined })
    setOpen(true)
  }

  const handleSubmit = async () => {
    const values = await form.validateFields()
    setSaving(true)
    try {
      if (editing?.nodeId) {
        await updateKnowledgeGraphNode(graphId, editing.nodeId, values)
        feedback.success(t('knowledgegraph.saveSuccess'))
      } else {
        await createKnowledgeGraphNode(graphId, values)
        feedback.success(t('knowledgegraph.createSuccess'))
      }
      setOpen(false)
      void load()
    } finally {
      setSaving(false)
    }
  }

  const handleDelete = async (record: KnowledgeGraphNodeItem) => {
    if (!record.nodeId) return
    await deleteKnowledgeGraphNode(graphId, record.nodeId)
    feedback.success(t('knowledgegraph.deleteSuccess'))
    void load()
  }

  const columns = [
    { title: t('knowledgegraph.entity.colName'), dataIndex: 'name', key: 'name' },
    { title: t('knowledgegraph.entity.colType'), key: 'type', render: (_: unknown, r: KnowledgeGraphNodeItem) => typeName.get(Number(r.entityTypeId)) ?? '-' },
    { title: t('knowledgegraph.entity.colDesc'), dataIndex: 'description', key: 'description' },
    {
      title: '', key: 'action', width: 130,
      render: (_: unknown, r: KnowledgeGraphNodeItem) => (
        <Space size={4}>
          <Button type="link" size="small" onClick={() => openEdit(r)}>{t('knowledgegraph.edit')}</Button>
          <Popconfirm title={t('knowledgegraph.entity.deleteConfirm')} onConfirm={() => void handleDelete(r)}>
            <Button type="link" size="small" danger>{t('knowledgegraph.delete')}</Button>
          </Popconfirm>
        </Space>
      ),
    },
  ]

  return (
    <>
      <Space style={{ marginBottom: 16 }}>
        <Button type="primary" icon={<PlusOutlined />} disabled={!graphEnabled || entityTypes.length === 0} onClick={openCreate}>
          {t('knowledgegraph.entity.create')}
        </Button>
        <Input.Search allowClear placeholder="搜索名称" onSearch={(v) => { setPageNo(1); setKeyword(v) }} style={{ width: 220 }} />
      </Space>
      <DataTable
        rowKey={(r) => String(r.nodeId)}
        columns={columns}
        dataSource={items}
        pagination={{ current: pageNo, pageSize, total, onChange: setPageNo }}
      />
      <Modal
        open={open}
        title={editing ? t('knowledgegraph.entity.edit') : t('knowledgegraph.entity.create')}
        onOk={() => void handleSubmit()}
        onCancel={() => setOpen(false)}
        confirmLoading={saving}
        destroyOnHidden
        maskClosable={false}
      >
        <Form form={form} layout="vertical">
          <Form.Item name="entityTypeId" label={t('knowledgegraph.entity.colType')} rules={[{ required: true, message: t('knowledgegraph.entity.typePlaceholder') }]}>
            <Select options={entityTypes.map((x) => ({ value: Number(x.entityTypeId), label: x.name ?? '' }))} />
          </Form.Item>
          <Form.Item name="name" label={t('knowledgegraph.entity.colName')} rules={[{ required: true, message: t('knowledgegraph.entity.namePlaceholder') }]}>
            <Input maxLength={200} />
          </Form.Item>
          <Form.Item name="description" label={t('knowledgegraph.entity.colDesc')}>
            <Input.TextArea maxLength={1000} rows={2} />
          </Form.Item>
        </Form>
      </Modal>
    </>
  )
}
```

> `DataTable` 的 props 以 `@/design-system` 实际签名为准；若其要求 `columns` 结构不同，按既有页面（如 `WikiDocuments.tsx`）写法调整。禁止直接 import antd `Table`。

- [ ] **Step 2: 单测**

mock `@/api/knowledgeGraph`，断言渲染节点行与"新建"按钮在无实体类型时禁用。

- [ ] **Step 3: 提交**

```bash
git add ui/src/pages/knowledgegraph
git commit -m "feat(knowledge-graph-ui): 实体列表"
```

---

## Task 7: 关系列表（关系页）

**Files:**
- Create: `ui/src/pages/knowledgegraph/KnowledgeGraphRelations.tsx`

- [ ] **Step 1: 实现**

结构同实体页，差异：
- 依赖 `getKnowledgeGraphSchema`（拿到 `relationTypes`）与 `getKnowledgeGraphNodes`（选择起止节点，可用 `Select showSearch` 远程按关键字拉取）
- 列表列展示 `起点名 → 关系名 → 终点名`：需按 `nodeId` 映射名称。为拿到端点名称，额外用 `getKnowledgeGraphNodes(graphId, { pageSize: 100 })` 建立 `nodeId → name` 映射（v1 手动图谱节点少，可接受；后续可加按 id 批量查询接口）
- 新建表单字段：`relationTypeId`、`sourceNodeId`、`targetNodeId`
- 编辑表单只允许改 `relationTypeId`（对齐后端 `UpdateKnowledgeGraphEdgeCommand`）
- 删除调 `deleteKnowledgeGraphEdge`

列渲染：

```tsx
{ title: t('knowledgegraph.relation.colSource'), key: 'source', render: (_, r) => nodeName.get(r.sourceNodeId ?? '') ?? r.sourceNodeId },
{ title: t('knowledgegraph.relation.colType'), key: 'type', render: (_, r) => relationName.get(Number(r.relationTypeId)) ?? '-' },
{ title: t('knowledgegraph.relation.colTarget'), key: 'target', render: (_, r) => nodeName.get(r.targetNodeId ?? '') ?? r.targetNodeId },
```

- [ ] **Step 2: 单测 + 提交**

```bash
git add ui/src/pages/knowledgegraph
git commit -m "feat(knowledge-graph-ui): 关系列表"
```

---

## Task 8: 模型 / Schema 页

**Files:**
- Create: `ui/src/pages/knowledgegraph/KnowledgeGraphSchema.tsx`

- [ ] **Step 1: 实现**

两张 `DataTable`（实体类型 / 关系类型）+ 各自 `Modal` 表单：
- 权限：`myRole !== null && myRole !== 0` 才显示增删改按钮；否则只读
- 实体类型表单：`name`、`color`、`description`
- 关系类型表单：`name`、`color`、`description`、`sourceTypeId`、`targetTypeId`（`Select allowClear`，选项为实体类型，占位"任意"）
- 删除用 `Popconfirm`；后端返回 409（仍有节点/边）时由全局错误中间件提示
- 增删改后重新拉取 `getKnowledgeGraphSchema`

- [ ] **Step 2: 单测 + 提交**

```bash
git add ui/src/pages/knowledgegraph
git commit -m "feat(knowledge-graph-ui): schema 管理页"
```

---

## Task 9: 设置页

**Files:**
- Create: `ui/src/pages/knowledgegraph/KnowledgeGraphSettings.tsx`

- [ ] **Step 1: 实现**

- 基础信息表单：`name`、`description`，仅 `myRole !== 0` 可编辑，保存调 `updateKnowledgeGraph` 后 `onChanged()`
- 只读展示：创建模板 `templateKey`、创建时间 `formatDateTime(graph.createTime)`
- 删除图谱：`Popconfirm` + `deleteKnowledgeGraph`，成功后 `navigate('/kg')`

```tsx
import { formatDateTime } from '@/utils/format' // 以项目实际工具路径为准
```

- [ ] **Step 2: 单测 + 提交**

```bash
git add ui/src/pages/knowledgegraph
git commit -m "feat(knowledge-graph-ui): 图谱设置页"
```

---

## Task 10: 全量验证

- [ ] **Step 1: 前端全绿**

Run: `cd ui && npm run typecheck && npm run lint && npm run test`
Expected: 0 error，全部测试 PASS

- [ ] **Step 2: 手动联调（后端 + Neo4j + OPEN_NEO4J=true）**

1. root 在系统设置开启 Neo4j 并填连接
2. 团队 → 知识图谱 tab → 新建（选 `运维服务` 模板）
3. 工作台 → 模型：确认自动出现 服务/人员/项目 与 维护/依赖
4. 实体：新建 `PaymentService(服务)`、`王工(人员)`、`结算平台(项目)`
5. 关系：新建 `王工 -维护→ PaymentService`、`结算平台 -依赖→ PaymentService`
6. 尝试 `王工 -依赖→ PaymentService`（约束应拒绝，提示类型不符）
7. 关闭 `OPEN_NEO4J` 后回到 `/kg`，确认提示"未开启"且新建禁用

- [ ] **Step 3: 提交（如有微调）**

```bash
git add ui
git commit -m "chore(knowledge-graph-ui): 联调修正"
```

---

## 自检

- **Spec 覆盖**：`/kg` 卡片墙（Task 3）、团队 tab 管理（Task 4）、工作台四段（Task 5/6/7/8/9）、模板选择（Task 4）、能力开关提示（Task 3/4/5/6）、权限只读（Task 4/8/9）、i18n 双语（Task 2）、Kiota 封装（Task 1）。
- **类型一致性**：封装函数名与后端 DTO 字段（`kgId/entityTypeId/relationTypeId/sourceNodeId/targetNodeId/total/enabled/myRole`）一致；页面只 import `@/api/knowledgeGraph`。
- **Kiota 命名风险**：生成物构建器名（`knowledgeGraph`/`byId`/`byNodeId`/`byEdgeId`/`byTypeId`）以 `npm run syncapi` 实际输出为准，Task 1 Step 1 已要求先核对。
- **design-system 导出风险**：`Empty`/`Select`/`Alert` 等是否由 `@/design-system` 导出以 `components/index.ts` 为准；违反"禁止直接 import antd Table/Form"会返工。

---

## Task 11（补充）: 外部接入（connected）UI

> 依据 [设计稿](../specs/2026-09-10-knowledge-graph-design.md) D11–D13。接入图只读，工作台只有 Schema + 设置。

**Files（改）**
- `ui/src/api/knowledgeGraph.ts` — 增 `mode` / `database` / 只读与内省字段
- `ui/src/i18n/locales/{zh-CN,en-US}/common.json` — 增接入相关文案
- `ui/src/pages/teams/knowledgegraph/TeamKnowledgeGraphs.tsx` — 建图/接入二选一
- `ui/src/pages/knowledgegraph/KnowledgeGraphDetail.tsx` — 按 mode 决定菜单
- `ui/src/pages/knowledgegraph/KnowledgeGraphSchema.tsx` — 接入只读展示

- [ ] **Step 1: API 封装增字段**

`KnowledgeGraphItem` / `KnowledgeGraphDetail` 增：

```ts
  mode?: string | null
  database?: string | null
  readOnly?: boolean | null
```

`KnowledgeGraphEntityTypeItem` 增 `count?: number | null`，`entityTypeId` 放宽为 `string | number | null`；`KnowledgeGraphRelationTypeItem` 同理增 `count`。

`KnowledgeGraphSchema` 增：

```ts
export interface KnowledgeGraphSchema {
  mode?: string | null
  database?: string | null
  readOnly?: boolean | null
  entityTypes?: KnowledgeGraphEntityTypeItem[] | null
  relationTypes?: KnowledgeGraphRelationTypeItem[] | null
  propertyKeys?: string[] | null
}
```

`getKnowledgeGraphSchema` 返回补齐 `mode/database/readOnly/propertyKeys`。

`createKnowledgeGraph` payload 增：

```ts
export async function createKnowledgeGraph(payload: {
  teamId: number; name: string; description?: string;
  mode?: 'managed' | 'connected'; templateKey?: string | null; database?: string | null;
}): Promise<number> {
  const client = getApiClient()
  const res = await client.api.knowledgeGraph.post({
    teamId: String(payload.teamId),
    name: payload.name,
    description: payload.description,
    mode: payload.mode ?? 'managed',
    templateKey: payload.templateKey ?? undefined,
    database: payload.database ?? undefined,
  })
  return Number(res?.value ?? 0)
}
```

- [ ] **Step 2: i18n 增键（两侧同步）**

zh-CN `knowledgegraph` 块内追加：

```json
"mode": "来源",
"modeManaged": "平台托管（新建）",
"modeConnected": "接入已有",
"database": "数据库名",
"databasePlaceholder": "请输入 Neo4j 数据库名",
"connectedBadge": "外部接入 · 只读",
"deleteConnectedConfirm": "仅从平台移除该接入，不影响外部数据，确认移除？",
"schemaConnected": {
  "labels": "标签（实体类型）",
  "relationshipTypes": "关系类型",
  "propertyKeys": "属性键",
  "count": "数量"
}
```

en-US 同步：

```json
"mode": "Source",
"modeManaged": "Managed (create new)",
"modeConnected": "Connect existing",
"database": "Database",
"databasePlaceholder": "Enter Neo4j database name",
"connectedBadge": "External connection · Read-only",
"deleteConnectedConfirm": "This only removes the connection from the platform and does not affect external data. Continue?",
"schemaConnected": {
  "labels": "Labels (Entity Types)",
  "relationshipTypes": "Relationship Types",
  "propertyKeys": "Property Keys",
  "count": "Count"
}
```

- [ ] **Step 3: 建图/接入弹窗**

`TeamKnowledgeGraphs.tsx` 表单增 `mode` 单选（`Radio.Group`），并按 `mode` 条件渲染：
- `managed`：显示「模板」`Select`（原逻辑）
- `connected`：显示「数据库名」`Input`（必填）

```tsx
<Form.Item name="mode" label={t('knowledgegraph.mode')} initialValue="managed">
  <Radio.Group>
    <Radio value="managed">{t('knowledgegraph.modeManaged')}</Radio>
    <Radio value="connected">{t('knowledgegraph.modeConnected')}</Radio>
  </Radio.Group>
</Form.Item>
<Form.Item noStyle shouldUpdate={(p, c) => p.mode !== c.mode}>
  {({ getFieldValue }) => getFieldValue('mode') === 'connected' ? (
    <Form.Item name="database" label={t('knowledgegraph.database')} rules={[{ required: true, message: t('knowledgegraph.databasePlaceholder') }]}>
      <Input maxLength={100} placeholder={t('knowledgegraph.databasePlaceholder')} />
    </Form.Item>
  ) : (
    <Form.Item name="templateKey" label={t('knowledgegraph.template')} initialValue="blank">
      <Select options={templates.map((x) => ({ value: x.key ?? '', label: x.name ?? '' }))} />
    </Form.Item>
  )}
</Form.Item>
```

`handleSubmit` 未编辑时传 `mode` + 条件字段。编辑态不允许改 `mode`/`database`（沿用只改名称/简介）。删除确认文案按卡片 `mode` 选择 `deleteConfirm` / `deleteConnectedConfirm`。卡片上对 `connected` 展示 `connectedBadge` 标签。

- [ ] **Step 4: 工作台按 mode 定菜单**

`KnowledgeGraphDetail.tsx`：

```tsx
const isConnected = graph?.mode === 'connected'
const menuItems = useMemo(() => isConnected
  ? [
      { key: 'schema', icon: <ApartmentOutlined />, label: t('knowledgegraph.menuSchema') },
      { key: 'settings', icon: <SettingOutlined />, label: t('knowledgegraph.menuSettings') },
    ]
  : [
      { key: 'entities', icon: <ProfileOutlined />, label: t('knowledgegraph.menuEntities') },
      { key: 'relations', icon: <DeploymentUnitOutlined />, label: t('knowledgegraph.menuRelations') },
      { key: 'schema', icon: <ApartmentOutlined />, label: t('knowledgegraph.menuSchema') },
      { key: 'settings', icon: <SettingOutlined />, label: t('knowledgegraph.menuSettings') },
    ], [t, isConnected])
```

- 默认 section：`isConnected ? 'schema' : 'entities'`（`section` 非法时回退）
- 顶部：`isConnected` 时用 `Tag color="blue"` 展示 `connectedBadge`，并保留 `enabled=false` 的告警
- 渲染：`isConnected` 只渲染 `schema` / `settings`；`entities` / `relations` 分支仅托管可用

- [ ] **Step 5: Schema 页接入只读展示**

`KnowledgeGraphSchema.tsx`：读取 `schema.mode`。
- `managed`：现有两张表 + 增删改
- `connected`：
  - 标签表：列 `Name` / `Count`，数据 `schema.entityTypes`
  - 关系类型表：列 `Name` / `Count`，数据 `schema.relationTypes`
  - 属性键：`Tag` 列表 `schema.propertyKeys`
  - 不渲染任何增删改按钮

- [ ] **Step 6: 验证 + 提交**

Run: `cd ui && npm run typecheck && npm run lint && npm run test`
Expected: 0 error，测试 PASS

手动联调：接入一个已存在的 Neo4j 库 → 工作台只显示模型/设置 → 模型展示内省标签/关系/属性键 → 无编辑入口 → 移除接入后外部库仍在。

```bash
git add ui
git commit -m "feat(knowledge-graph-ui): 外部接入只读工作台"
```

