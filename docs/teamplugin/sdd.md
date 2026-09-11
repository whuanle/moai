# 团队插件模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 资料来源：[../aiplugin-dynamic/sdd.md](../aiplugin-dynamic/sdd.md)、[../aiplugin-static/sdd.md](../aiplugin-static/sdd.md) ｜ 证据：[local-dev/team-plugin-e2e.mjs](../../local-dev/team-plugin-e2e.mjs)

- 日期：2026-09-10
- 状态：数据库（授权表）+ 后端 API + 前端已实现（分自定义/动态两 Tab，能力对齐管理员面板）；team-plugin e2e 19/19 通过
- 领域：`src/teamplugin`（Shared/Core/Api），前端 `ui/src/pages/teams/plugins`（+ 复用 `ui/src/pages/plugins/components` 弹窗）

## 1. 目标

把插件使用权收敛到团队维度。现有系统插件（custom/dynamic/static）面向全管理员，缺少团队隔离：

1. **系统插件私有授权**：系统插件可为公开（所有团队可用）或私有（`is_public=false`，需按团队授权）。私有系统插件通过 `plugin_team_authorization` 表授权团队，被授权团队即可使用。
2. **团队插件**：团队可创建归本团队所有的自定义（MCP/OpenAPI）与动态插件，其它团队不可见；Owner/Admin 管理，Member 使用，非成员 404。

## 2. 数据模型

- 复用既有 `plugin`（含 `is_system/team_id/is_public`）、`plugin_custom`、`plugin_dynamic`、`plugin_function`。团队插件 `plugin.is_system=false`、`plugin.team_id=teamId`、`plugin.is_public`（团队内均私有，true 等效团队可见）。
- **新增表** `plugin_team_authorization(id / plugin_id(uuid) / team_id(int) + 审计)`，bigint 软删除；partial 唯一 `(plugin_id, team_id) WHERE is_deleted=0`。授权对象为**系统插件记录 id（plugin.id）**。
- DDL：`asserts/plugin_team_authorization.sql`。无 EF Migration，已有库需手动执行。

## 3. 权限矩阵（Handler 层判定，复用团队角色）

| 操作 | Owner/Admin | Member | 非成员 | 管理员 |
|---|---|---|---|---|
| 团队插件 创建/编辑/删除 | ✅ | 403 | 404 | - |
| 团队可用插件列表 | ✅ | ✅ | 404 | - |
| 系统插件私有授权 查询/更新 | - | - | - | ✅（`/ai/plugin/*` 门禁在 Controller） |

团队可用插件列表 = 本团队自有插件（custom/dynamic）+ 可用系统插件（`is_public=true` 或 已在 `plugin_team_authorization` 授权本团队）。

## 4. API 契约

### 系统插件私有授权（管理员，`/api/ai/plugin/{pluginId}/authorization`）

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| GET | `/api/ai/plugin/{pluginId}/authorization` | 查询授权；公开插件返回 `isPublic=true` 且 items 空 | `QueryPluginTeamAuthorizationCommandResponse` |
| PUT | `/api/ai/plugin/{pluginId}/authorization` | 全量替换授权团队 `{teamIds}` | Empty |

### 团队插件（`/api/team/{teamId}/plugin`，权限判定在 Handler）

| 方法 | 路由 | 说明 | 出参 |
|---|---|---|---|
| GET | `/api/team/{teamId}/plugin/list` | 团队可用插件列表（含 myRole/canManage） | `QueryTeamPluginsCommandResponse` |
| GET | `/api/team/{teamId}/plugin/dynamic_templates` | 可用动态模板（注册表发现，成员可访问） | `QueryPluginListCommandResponse` |
| GET | `/api/team/{teamId}/plugin/{pluginId}/detail` | 自定义插件详情（团队自有/可用系统插件） | `QueryCustomPluginDetailCommandResponse` |
| POST | `/api/team/{teamId}/plugin/{pluginId}/functions` | 插件函数列表 | `QueryCustomPluginFunctionsListCommandResponse` |
| POST | `/api/team/{teamId}/plugin/{pluginId}/refresh_mcp` | 刷新 MCP 工具列表（Owner/Admin） | Empty |
| POST | `/api/team/{teamId}/plugin/pre_upload_openapi` | 预上传 OpenAPI 文件（Owner/Admin） | `PreUploadOpenApiFilePluginCommandResponse` |
| POST | `/api/team/{teamId}/plugin/run` | 运行团队可用插件（成员） | `PluginRunResult` |
| POST | `/api/team/{teamId}/plugin/dynamic` | 创建/更新团队动态实例 | Empty |
| POST | `/api/team/{teamId}/plugin/mcp` | 导入/更新团队 MCP 插件 | `SimpleGuid` |
| POST | `/api/team/{teamId}/plugin/openapi` | 导入/更新团队 OpenAPI 插件 | `SimpleGuid` |
| DELETE | `/api/team/{teamId}/plugin/{pluginId}` | 删除团队插件 | Empty |

命令均实现 `IUserIdContext`，Controller 复用 `IUserContextProvider.SetUserContext` 注入当前用户；Handler 内用 `ITeamService.GetMyRoleAsync` 判角色。列表/详情/函数/运行仅需成员；创建/编辑/删除/刷新/预上传需 Owner/Admin。

前端不再自绘简化表格：自定义/动态两个 Tab 复用管理员面板同款弹窗组件（`McpPluginModal`/`OpenApiModal`/`FunctionListModal`/`PluginRunDrawer`），通过可选注入的 `loadDetail`/`loadFunctions`/`uploadFile`/`runPlugin` 适配团队接口；`TeamPlugins` 仅负责拉取一次列表并按 `kind` 分发。

## 5. 关键决策

- **D1 授权对象用插件记录 Id（Guid）**：与 aiplugin 系统插件管理（`QueryPluginManageListCommand` 返回 `Id`）一致，前端 `pluginId` 直传。
- **D2 团队插件复用既有 plugin 表 + TeamId**：不新建插件类型表，`is_system=false` 区分系统/团队，`team_id` 区分归属团队；静态插件无需团队副本（所有团队可用）。
- **D3 角色判定在 Handler**：团队插件对齐 variable/wiki 模块，用 `ITeamService`（带 Redis 缓存）；非成员 404 不泄露团队存在性。
- **D4 复用 aiplugin 连接器**：MCP/OpenAPI 导入复用 `McpServerConnector`/`OpenApiDocumentParser`（已由 internal 改为 public），避免重复解析逻辑。
- **D5 团队插件实例 key/名称在团队内唯一**：`plugin.team_id + plugin_name` 唯一校验；不与系统插件注册表 key 冲突。

## 6. 已知问题 / 下阶段

- 团队插件运行通过 `/api/team/{teamId}/plugin/run` 开放给成员：仅允许运行团队自有插件或公开/已授权的系统插件（`IsPluginAvailableAsync`），非成员 404。MCP/OpenAPI 自定义插件与管理员一致不走该运行入口（返回 404），动态实例与静态插件可运行。
- 团队插件不共享到其它团队（仅支持系统插件的授权）；跨团队共享/授权留待后续。
- `plugin_team_authorization` 仅支持系统插件，团队插件之间的授权/共享未纳入。
- 分类列表 `/api/classify/list` 仅管理员可读：团队页仅管理员加载分类筛选/编辑，非管理员成员按列表返回的 `classifyName` 展示分类，保存时 `classifyId=0`。
- 动态实例 key 全局唯一（`IDynamicInstanceResolver` 按 key 解析），跨团队冲突返回 409；同团队同 key 视为更新实例。
