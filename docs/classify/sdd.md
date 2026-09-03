# 分类管理（Classify）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../user-management/sdd.md](../user-management/sdd.md)（管理员门禁依赖 `userstate`）｜ 规范：[../cqrs-conventions.md](../cqrs-conventions.md) ｜ 证据：[local-dev/classify-e2e.mjs](../../local-dev/classify-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@CLS-Sxx），本文不重复。

## 目标

新建独立分类管理模块 `MoAI.Classify`，作为**全站唯一分类管理来源**，运营者（admin+）统一维护三种分类：插件（`plugin`）、应用（`app`）、知识库（`kb`）。前端按类型用 Tab 标签隔开，每类下可增删改分类项。

将 AiPlugin 中现存的插件分类 Command/Handler/Controller/前端 API 迁移收敛到本模块，避免两套分类管理并存。

## 组件

```
src/classify/
├── MoAI.Classify.Shared/  ClassifyTypes（常量：plugin/app/kb）
│                          Commands/CreateClassifyCommand({type,name,description}, IRequest<SimpleInt>)
│                          Commands/UpdateClassifyCommand({classifyId,name,description})
│                          Commands/DeleteClassifyCommand({classifyId})
│                          Queries/QueryClassifyListCommand({type}) + Responses/{ClassifyItem,QueryClassifyListCommandResponse}
│                          ClassifySharedModule
├── MoAI.Classify.Core/    Commands/{Create,Update,Delete}ClassifyCommandHandler（同类型名称唯一校验）
│                          Queries/QueryClassifyListCommandHandler
│                          ClassifyCoreModule
└── MoAI.Classify.Api/     Controllers/ClassifyController（[Route("/classify")]，门禁在此）
                           ClassifyApiModule
src/database/…/Entities/   ClassifyEntity（已存在，复用；Type 区分类型）
src/database/…/Seed/       ClassifySeed（补 kb 类型种子；plugin/app 沿用）
ui/src/                    api/classify.ts（封装 + ClassifyTypes 常量）、pages/classify/Classify.tsx（/classify）
```

三层依赖：`Api → Core → Shared`；均 `ProjectReference` 到 `MoAI.Infra.*` 与 `MoAI.Database.Shared`（Core），Api 额外引用 `MoAI.Account.Shared`（管理员校验用）。

## API 契约

路由前缀 `/classify`，认证自动追加 `[Authorize]`，管理员门禁在 Controller 层（`IUserAccountService.GetUserStateAsync().IsAdmin`，否则 403「只有管理员可以管理分类」）。

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| GET | `/classify/list?type=plugin\|app\|kb` | admin | 按类型返回分类列表 `{items:[{classifyId,name,description}]}` |
| POST | `/classify` | admin | `{type,name,description}` → `SimpleInt`（新建分类 id） |
| PUT | `/classify` | admin | `{classifyId,name,description}` 修改 |
| DELETE | `/classify` | admin | `{classifyId}` 软删除 |

## 关键决策

1. **类型常量**：`ClassifyTypes.Plugin="plugin"`、`App="app"`、`Kb="kb"`。复用现有 `ClassifyEntity.Type`（≤10 字符）存字符串。
2. **复用实体**：沿用 `ClassifyEntity`（已含 `Id/Type/Name/Description` + 全审计 + 软删除），**不新建表**；仅补 `kb` 类型种子。
3. **同类型内名称唯一**：校验 `Type + Name + IsDeleted==0` 唯一，冲突 409「分类名称已存在，请更换后重试。」（插件/应用/知识库各自独立命名空间）。
4. **删除策略**：软删除（`IsDeleted=1`）。先保留引用冲突提示占位——删除时校验同类型下是否仍有资源引用，若未来业务未实现则该类资源可删除；当前插件沿用校验 `Plugins.ClassifyId`（[@CLS-S6](./bdd.md#cls-s6)）。
5. **门禁位置**：权限只在 Controller 层判断（admin），Handler 层不注入用户上下文；目标数据规则（同类型字段过滤）在 Handler 层。
6. **前端 Tab**：`Page` + `Tabs`（plugin/app/kb），每 Tab 一张分类列表；增删改走弹窗（名称 + 描述），危险删除用 `Popconfirm`；文案走 i18n（zh-CN + en-US）。
7. **迁移收敛**：删除 AiPlugin 里重复的插件分类 Command/Handler/Query/响应类及 `PluginManageController` 中 classify 相关方法；`ui/src/api/plugin.ts` 与 `Plugins.tsx` 中分类管理部分改走新 `classify.ts`，分类 Tab 筛选仍可用。

## 种子数据

`ClassifySeed` 现有 `classifyTypes = { "prompt", "plugin", "app" }` 与 33 个名称。调整为 `{ "plugin", "app", "kb" }`（原 `prompt` 类型并入 `kb`），补 `kb` 类型同名分类种子。

## 已知问题

- 应用（`app`）与知识库（`kb`）业务层尚未实现（平台底座阶段），本模块只提供分类字典维护；资源引用校验暂以插件为准，`app/kb` 分类删除暂不校验引用（预留扩展点）。
- 遗留观察：`ClassifySeed` 种子 `description=name`，后续可改为中文语义描述。
