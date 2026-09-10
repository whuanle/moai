# 前端管理页（Settings / OauthConnect）设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../../../docs/settings/sdd.md](../../../docs/settings/sdd.md)（设置项契约）、[../../../docs/oauthconnect/sdd.md](../../../docs/oauthconnect/sdd.md)（渠道契约） ｜ 证据：`cd ui && npm run test`（回归）+ 手工走查
> 规范：[../../../docs/DOC-STANDARD.md](../../../docs/DOC-STANDARD.md)。行为场景见 BDD（@FE-PG-Sxx），本文不重复。

## 目标

两个管理页：`/settings`（系统设置，当前仅 Neo4j 知识图谱配置，**root 专属**；读写字典式 `setting` 表）与 `/oauthconnect`（OAuth 渠道 CRUD，前端管理页的样式基准页）。权限三层：AppSider 菜单可见性 → 页面内 `Navigate` 兜底 → 后端 403 最终防线。其中 `/settings` 菜单与页面均限 root（`userInfo.isRoot === true`）；`/oauthconnect` 限 admin。

菜单结构（AppSider）：mainNav（所有人：overview/app/wiki/team）+ adminNav（isAdmin 才渲染，前置分隔线：plugin/users/oauthconnect/settings；其中 settings 仅 root 可见）。

## 组件

### Settings.tsx（/settings，root 专属）

- 结构：`Page` + 单 Card「知识图谱（Neo4j）」：一行开关（名称 + 描述 + `Switch`），开启后展开连接地址/用户名/密码三个 `Input`（密码为 `Input.Password`），底部右对齐「保存」按钮（`disabled={!graphDirty || loading}`）。
- 加载：`getSettings()` 在 items 中按 `OPEN_NEO4J` 等 key 取值，`value === 'true'` 转 bool；脏标记：开关或字段变更置 dirty，保存成功或重载后清零。
- 保存：先写 `OPEN_NEO4J`，仅在开启时依次写 `NEO4J_URI`/`NEO4J_USERNAME`/`NEO4J_PASSWORD`；关闭不写连接项（[@SET-S16](../../../docs/settings/bdd.md#set-s16)/[@SET-S17](../../../docs/settings/bdd.md#set-s17)）。保存失败自动 `load()` 重拉，恢复为库中真值（[@FE-PG-S8](./bdd.md#fe-pg-s8)）。
- 非 root 访问 `Navigate` 重定向 `/dashboard`（[@SET-S18](../../../docs/settings/bdd.md#set-s18)）。

### OauthConnect.tsx（/oauthconnect）

- 列表：`<Page title>` + `DataTable` 六列（名称/类型 Tag/Key ellipsis/图标 32px `resolveStorageUrl`/授权地址 copyable/操作），`pagination={false}`、`sticky` 表头、操作列 `fixed:'right'` 图标按钮（编辑/删除，Tooltip+aria-label，删除保留 Popconfirm 红色确认）；工具栏「新建」主按钮 + `onRefresh`；`isAdmin` 为 true 才加载。（2026-09-02 与 /users 页统一 UI 约定）
- 新建/编辑 Modal（`maskClosable={false}`、`destroyOnClose`）字段：name 必填（≤50）；provider Select（custom/feishu/dingTalk/gitHub，**编辑态禁用**）；key 必填；secret 新建必填/编辑可选（留空 = 不变，带提示）；iconUrl 必填（`IconPicker`：URL 输入 + 上传按钮 + 32px 预览）；wellKnown 仅 custom/gitHub 类显示且必填（飞书/钉钉隐藏，切换 provider 时清空）。
- 提供商辅助：`providerNoDiscovery`（feishu/dingTalk 无 OIDC 发现端点）；`providerDefaults` 内置官方图标，切换类型时 iconUrl 为空则自动填充（不覆盖已填值）；`providerTagColor/providerLabel` 渲染类型标签。

## API 契约

| 函数（`api/settings.ts` / `api/oauthconnect.ts`） | HTTP | 说明 |
|---|---|---|
| `getSettings()` | `GET /settings` | 字典 items |
| `saveSetting(key, value)` | `PUT /settings` | value 恒字符串；`SettingKeys.neo4jEnabled/neo4jUri/neo4jUsername/neo4jPassword = 'OPEN_NEO4J'/'NEO4J_URI'/'NEO4J_USERNAME'/'NEO4J_PASSWORD'` |
| `getOAuthConnections()` | `GET /oauthconnect/connections` | 渠道 items |
| `createOAuthConnection(p)` | `POST /oauthconnect/connections` | `{ name, provider, key, secret, iconUrl, wellKnown? }` |
| `updateOAuthConnection(id, p)` | `PUT /oauthconnect/connections/{id}` | body 附 `oAuthConnectionId`；**后端历史缺陷已修复（2026-09-02 实测 200）** |
| `deleteOAuthConnection(id)` | `DELETE /oauthconnect/connections/{id}` | |

## 关键决策

1. 权限分层：菜单渲染控制只是体验层，页面内 `if (!isRoot) return <Navigate to="/dashboard" replace />` 兜底，后端门禁（PUT 403）为最终防线。
2. Settings 保存失败回滚重拉，避免界面与库中真值不一致。
3. 渠道编辑不改类型（provider 禁用）、secret 留空不改（[@FE-PG-S15](./bdd.md#fe-pg-s15)）。
4. 图标上传走 storage 直传，库中只存 objectKey，展示经 `resolveStorageUrl`。

## 已知问题

- **编辑接口修复史**：`PUT /oauthconnect/connections/{id}` 曾因后端路由回填 Command 被自动验证拦截**恒 400**（2026-09-01 发现，当时以删除重建绕行）；**后端已修复（Validate 移除路由回填字段规则），2026-09-02 实测 200**，编辑功能已恢复可用。详见 [SOP 历史验收存档](./sop.md) 与上游 [../../../docs/oauthconnect/sop.md](../../../docs/oauthconnect/sop.md) 排障表。
- **`/plugin` 是占位导航**：adminNav「插件」在路由表无对应路由，点击落入 `*` 兜底回 `/dashboard`（mainNav 的 /app、/wiki、/team 同为占位）。
- 两页 `<Page>` 均不渲染页头标题（现行规范：设置类无标题），OauthConnect 为标准写法。
- 两页均有组件测试：Settings 见 `ui/src/pages/settings/__tests__/Settings.test.tsx`（root 开关/连接项保存/非 root 重定向）；OauthConnect 仍待补。
- Settings 当前仅知识图谱一组设置（`SettingKeys` 四个常量）；新增需同步 `SettingKeys`、Card 字段与 i18n（`settings.*`）。
- `oauthconnect.*` i18n 共 31 键；Modal 内 `placeholder="https://..."` 为技术 URL 示例，未走 t()。
