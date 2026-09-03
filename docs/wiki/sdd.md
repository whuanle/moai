# 知识库模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/wiki-e2e.mjs](../../local-dev/wiki-e2e.mjs)

- 日期：2026-09-02
- 状态：数据库 + API + 前端已实现（含二期文档内容层）；集合/上传为下阶段
- 领域：`src/wiki`（Shared/Core/Api），前端 `ui/src/pages/wiki`

## 1. 目标

知识库是团队下的第一类资源（对齐"按团队管理"路线）。本期交付：知识库 CRUD（团队作用域）+「当前团队」上下文与侧边栏切换器。

## 2. 数据模型

- `wiki_document`（二期）：`id / wiki_id / title(100) / content(text Markdown)` + 审计；索引 `idx_wiki_document_wiki_id`；无唯一约束（同库允许同名文档）
- `wiki`：`id / team_id / name / description` + 审计（bool 软删除，同 team 约定 D1）
- partial 唯一 `(team_id, name) WHERE is_deleted = false`：同团队未删除范围内名称唯一，删除后同名可重建；不同团队互不影响
- 索引 `idx_wiki_team_id`；DDL：`asserts/wiki.sql`

## 3. 权限（复用 Team 领域角色，Handler 层判定）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 创建/更新/删除 | ✅ | 403 | 404 |
| 列表/详情 | ✅ | ✅ | 404 |

- 角色判定注入 `MoAI.Team.Shared` 的 `ITeamService`（跨域接口复用，实现由 Team 模块注册）
- 列表/详情响应携带 `myRole`，前端据此渲染管理操作

## 4. API

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| POST | `/api/wiki` | 创建 `{teamId, name, description?}` | `SimpleLong` |
| GET | `/api/wiki/list?teamId=` | 团队知识库列表（含 myRole） | `QueryWikisCommandResponse` |
| GET | `/api/wiki/{id}` | 详情（含 myRole） | `QueryWikiCommandResponse` |
| PUT | `/api/wiki/{id}` | 更新 `{name, description?}` | Empty |
| DELETE | `/api/wiki/{id}` | 软删除 | Empty |
| GET | `/api/wiki/{wikiId}/documents` | 文档列表（不含正文，含 myRole） | `QueryWikiDocumentsCommandResponse` |
| POST | `/api/wiki/{wikiId}/documents` | 创建文档 `{title, content?}` | `SimpleLong` |
| GET | `/api/wiki/document/{documentId}` | 文档详情（含正文） | `QueryWikiDocumentCommandResponse` |
| PUT | `/api/wiki/document/{documentId}` | 更新文档 `{title, content}` | Empty |
| DELETE | `/api/wiki/document/{documentId}` | 删除文档 | Empty |

知识库删除（软删）后其文档一并不可访问（文档路由校验 wiki 存在性）。

## 5. 前端「团队模式」上下文

- `store/app.ts` 新增 `currentTeamId`（persist）与 `myTeams`（内存）
- 侧边栏（用户区下方）新增团队切换器：选择即写入上下文并跳转 `/wiki`；无团队时下拉内提供「新建团队」入口
- `/wiki` 页：未选团队显示引导空态；已选团队按 `myRole` 渲染新建/编辑/删除
- 团队增删后 `Teams.tsx` 同步 `myTeams`，侧边栏列表保持新鲜

## 6. 关键决策

- **D1** 权限复用团队角色：wiki 不引入新角色体系，Admin 及以上可管理
- **D2** 跨域接口复用：Core 引用 `MoAI.Team.Shared`（接口），不引用其实现项目
- **D3** 名称唯一性为团队作用域（团队间允许同名）
- **D4** 文档内容协作开放全员：Member 可创建/编辑文档（知识库是协作产物）；删除文档为破坏性操作需 Admin+；知识库结构管理维持 Admin+

## 7. 已知问题 / 下阶段

- ~~知识库内容层未实现~~ → 二期已交付文档层（wiki_document + 5 端点 + 前端编辑器）；集合/上传/富文本渲染下阶段
- 切换团队时若停留在非团队作用域页面不做强制跳转（仅选择器跳 /wiki）
