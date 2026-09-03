# 动态插件（DynamicPlugin）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../aiplugin-static/sdd.md](../aiplugin-static/sdd.md) ｜ 规范：[../cqrs-conventions.md](../cqrs-conventions.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。行为场景见 BDD（@DYN-Sxx），本文不重复。

## 目标

在已有 `aiplugin` 插件引擎（`PluginRegistry` + `PluginExecutor`）与静态插件能力之上，落地**动态插件**实例化管理。动态插件模板（如 `dynamic_greet`）存在于内存注册表，无实例时不可直接运行；需**先创建实例并填入配置**，一个模板可创建多个实例，实例 key 全局唯一。

需要实现：
1. **动态插件实例列表**：查询已创建的实例（DB `plugin_dynamic ⋈ plugin`），展示实例 key、模板 key、标题、分类、配置。
2. **创建/编辑实例**：填实例 key（小写+下划线、≤30、全局唯一）、模板 key、标题、描述、分类、配置（Monaco 编辑器）。创建时可校验配置；运行实例取存储的配置。
3. **运行实例**：`key=实例 key`，后端按实例 key 定位模板与配置并执行。
4. **删除实例**：软删除实例及关联 plugin 行。

## 组件

```
src/aiplugin/
├── MoAI.AIPlugin.Shared/
│   ├── Commands/SaveDynamicPluginCommand.cs        {pluginKey,templeteKey,title,description,classifyId,config} → EmptyCommandResponse
│   ├── Commands/DeleteDynamicPluginCommand.cs       {pluginKey} → EmptyCommandResponse
│   └── Queries/Responses/QueryPluginManageListCommandResponseItem.cs  （增强：+TempleteKey,+Config,+ConfigExample）
├── MoAI.AIPlugin.Core/
│   ├── Commands/RunPluginCommandHandler.cs          （增强：key 未命中注册表时走实例解析器）
│   └── Services/IDynamicInstanceResolver.cs         {Resolve(instanceKey) → (Template,ConfigJson)}
├── MoAI.AIPlugin.Custom/
│   ├── Commands/SaveDynamicPluginCommandHandler.cs  校验 + 创建/更新实例
│   ├── Commands/DeleteDynamicPluginCommandHandler.cs 软删除
│   ├── Services/DynamicInstanceResolver.cs          实例 key → 模板 key + 配置
│   ├── Queries/QueryPluginManageListCommandHandler.cs （增强：动态实例合并 + 模板字段填充）
│   └── CustomPluginModule.cs                        注册 IDynamicInstanceResolver
└── MoAI.AIPlugin.Api/
    └── Controllers/DynamicPluginController.cs       [Route("/ai/plugin/dynamic")]，门禁在 Controller

ui/src/
├── api/plugin.ts                                    +getDynamicTemplates,+saveDynamicPlugin,+deleteDynamicPlugin
└── pages/plugins/DynamicPluginPanel.tsx              实例列表 + 新建/编辑弹窗（Monaco 配置）+ 运行 + 删除
```

三层依赖：`Api → Core → Shared`；Core 增 `IDynamicInstanceResolver`（供 Run 解析）。Api 引用 `MoAI.Account.Shared`、`MoAI.AIPlugin.Shared`。

## API 契约

路由前缀 `/ai/plugin/dynamic`，认证自动追加 `[Authorize]`；管理员门禁在 Controller 层（`GetUserStateAsync().IsAdmin`，否则 403「只有管理员可以管理插件」）。

| 方法 | 路由 | 门禁 | 说明 |
|---|---|---|---|
| POST | `/ai/plugin/dynamic/save` | admin | `{pluginKey,templeteKey,title,description,classifyId,config}` → `EmptyCommandResponse`，创建/更新实例 |
| DELETE | `/ai/plugin/dynamic` | admin | `{pluginKey}` → `EmptyCommandResponse`，软删除实例 |
| POST | `/ai/plugin/run`（沿用 `PluginController`） | admin | `{key=实例key,requestJson}` → `PluginRunResult`，运行实例 |
| GET | `/ai/plugin`（`QueryAll`） | admin | 返回注册表模板列表，含 `configExample/paramsExample/isDynamic`（前端模板下拉） |

`QueryPluginManageListCommandResponseItem` 新增字段：
- `templeteKey`（string?）：动态实例的模板 key。
- `config`（string?）：动态实例存储的配置 JSON。
- `configExample`（string?）：动态模板配置示例（创建时 Monaco 初始值）。

## 关键决策

1. **实例 key 存 `plugin_dynamic.plugin_key`**；**模板 key 存 `plugin_dynamic.templete_key`**（`.NET` 字段 `TempleteKey`；用户新增该列）。`plugin` 行 `PluginName`=实例 key、`PluginId`→`plugin_dynamic.Id`、`Type=NativePlugin`、`IsSystem=true`。
2. **key 唯一性**：实例 key 全小写+下划线（`^[a-z_][a-z0-9_]*$`），≤30；创建时校验不与**注册表 key**（含静态/动态模板）重复，也不与已存在的动态实例重复（409「实例 Key 已被使用」）。
3. **模板校验**：`templeteKey` 必须在注册表且 `IsDynamic`，否则 404「动态插件模板不存在」。
4. **运行解析**：`RunPluginCommand` 的 `key` 先查注册表；未命中则用 `IDynamicInstanceResolver.Resolve(key)` 由 `plugin_dynamic.plugin_key`→`templete_key`→`registry.Get(templete_key)`，取该实例 `config` 初始化；`configJson` 无需前端传。
5. **编辑不可改实例 key**：更新只改 `templeteKey/config/title/description/classifyId`；实例 key 作为主键定位。
6. **分类校验**：`classifyId` 非 0 需在 `classify` 表存在且 `Type=plugin`，否则 400。
7. **删除**：软删除 `plugin_dynamic` 与该实例关联的 `plugin` 行（`IsDeleted=1`）。
8. **前端**：动态 Tab 用 `DynamicPluginPanel`；新建/编辑弹窗内含 Monaco 配置编辑器；运行复用 `PluginRunDrawer`（`paramsExample` 来自模板）。i18n zh/en 同步。

## 已知问题

- `templete_key` 列名拼写沿用用户给定的 `templete_key`（非 `template_key`）；如需修正属数据/DB 迁移事项。
- 动态模板的配置仍以 `configJson` 在 `PluginExecutor.InitAsync` 校验；此处实例 `config` 直接作为该值传入。
