# 静态插件（StaticPlugin）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-sdd](../database-scaffold/sdd.md) ｜ 规范：[../cqrs-conventions.md](../cqrs-conventions.md) ｜ 证据：[local-dev/static-plugin-e2e.mjs](../../local-dev/static-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@STP-Sxx），本文不重复。

## 目标

在已有 `aiplugin` 插件引擎（`PluginRegistry` + `PluginExecutor`）基础上，落地**静态插件**的完整管理闭环：静态插件无实例、无配置，注册表内存中存在但 DB 默认无记录。需要实现：

1. **静态插件列表**：合并「内存注册表发现的静态插件」与「DB 中的静态插件记录」，去重后返回；无 DB 记录的插件分类归「未分类」。
2. **运行静态插件**：右侧抽屉，Monaco JSON 编辑器展示请求参数示例，运行后展示结果。
3. **编辑写回**：一旦修改静态插件信息（标题/描述/分类），写回或创建 DB 记录，下次查询归入对应分类。

## 组件

```
src/aiplugin/
├── MoAI.AIPlugin.Shared/
│   ├── Commands/SaveStaticPluginCommand.cs        {pluginKey,title,description,classifyId} → EmptyCommandResponse
│   ├── Queries/Responses/QueryPluginManageListCommandResponseItem.cs  （增强：+PluginKey,+ParamsExample）
│   └── Queries/QueryPluginManageListCommand.cs    （沿用，无需改）
├── MoAI.AIPlugin.Core/
│   ├── Commands/SaveStaticPluginCommandHandler.cs  校验 + 写回/创建 DB
│   └── Queries/QueryPluginManageListCommandHandler.cs  （增强：合并内存 + DB 去重）
├── MoAI.AIPlugin.Static/                          （示例插件：StaticEchoPlugin 沿用）
└── MoAI.AIPlugin.Api/
    └── Controllers/StaticPluginController.cs      [Route("/ai/plugin/static")]，门禁在 Controller
ui/src/
├── api/plugin.ts                                   +saveStaticPlugin, +runPlugin
└── pages/plugins/
    ├── Plugins.tsx                                静态 Tab：操作列（运行/编辑）+ 抽屉 + 编辑弹窗
    └── components/PluginRunDrawer.tsx              Monaco JSON 编辑器 + 运行结果
```

三层依赖：`Api → Core → Shared`。Api 引用 `MoAI.Account.Shared`（管理员校验）、`MoAI.AIPlugin.Shared`。

## API 契约

路由前缀 `/ai/plugin/static`，认证自动追加 `[Authorize]`；管理员门禁在 Controller 层（`GetUserStateAsync().IsAdmin`，否则 403「只有管理员可以管理插件」）。

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| POST | `/ai/plugin/static/save` | admin | `{pluginKey,title,description,classifyId}` → `EmptyCommandResponse`，写回/创建 DB |
| POST | `/ai/plugin/run`（沿用 `PluginController`） | admin | `{key,requestJson}` → `PluginRunResult`，运行静态插件 |

`QueryPluginManageListCommandResponseItem` 新增两个字段：
- `pluginKey`（string?）：仅静态插件有；内存发现但无 DB 记录时即为 key，用于编辑写回。
- `paramsExample`（string?）：仅静态插件有；来自 `PluginTypeHelper.GetStaticExample(...,"GetParamsExampleValue")`，抽屉 Monaco 初始值。

## 关键决策

1. **合并去重主键 = 插件 key**：内存发现（`registry.GetAll().Where(!IsDynamic)`）与 DB 记录（`PluginStatics ⋈ Plugins`）按 `pluginKey` 合并；**DB 记录优先**（已有分类/标题/描述），内存-only 项 `classifyId=0`、`classifyName=null`（前端渲染「未分类」）、`IsSystem=true`。
2. **key 不可变**：key 是引擎注册标识，编辑不修改 key；编辑只改 title/description/classifyId。
3. **写回策略**：DB 存在 `plugin_key == pluginKey` 的记录则更新；否则**新增** `PluginEntity`（`IsSystem=true`、`TeamId=0`、`Type=native`、`PluginName=pluginKey`、`ClassifyId=请求值`）+ `PluginStaticEntity`（`PluginKey=pluginKey`）。新增只在用户首次编辑时发生。
4. **分类校验**：`classifyId` 若非 0 需在 `classify` 表存在且 `Type=plugin`，否则 400；允许 0（未分类）。
5. **运行接口复用**：静态插件已由 `RunPluginCommand` / `PluginExecutor` 处理（静态无 configJson），前端直接复用 `POST /ai/plugin/run`，不新增运行端点。
6. **前端编辑器**：`@monaco-editor/react`（Monaco）——与 VS Code 同款内核，支持 JSON 高亮/校验/折叠，符合「vscode 编辑器」诉求；抽屉 `maskClosable={false}`。
7. **门禁位置**：权限只在 Controller 层（admin），Handler 层不注入用户上下文；目标数据规则（key 定位、分类校验）在 Handler 层。
8. **i18n**：新增文案同时写 `zh-CN` 与 `en-US` `common.json`。

## 已知问题

- 动态插件（dynamic）的本轮不做，仅静态；动态实例管理（多实例、key 唯一、配置编辑）后续迭代。
- `paramsExample`/`pluginKey` 只对静态插件有值；自定义（custom）插件走现有 `CustomPluginPanel`，不涉及本模块。
- Monaco 体积较大（worker 按需加载），首次打开抽屉可能短暂白屏（可接受，后续可做懒加载）。
