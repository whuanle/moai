# 团队智能体模块设计规格（SDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/sdd.md](../team/sdd.md) ｜ 证据：[local-dev/team-agent-e2e.mjs](../../local-dev/team-agent-e2e.mjs)

- 日期：2026-11-05（同日更新：系统内置智能体——规划师/构建师自动种入；**Phase 2 应用绑定与运行时派发落地**，见 [../app/sdd.md](../app/sdd.md) D52）；2026-11-06（**绑定与子代理协作解耦 + 子智能体过程实时可视化 moai.subagent**，见 [../app/sdd.md](../app/sdd.md) D53）
- 状态：**Phase 1 管理面已实现**（数据库 + API + 前端团队菜单 + 内置 plan/build 种子）；**Phase 2 应用绑定与运行时派发已实现**（Agent 应用配置绑定 `team_agent_ids/default_team_agent_id`（**绑定独立于子代理协作**：不开协作=仅主智能体提示词生效，开协作=子智能体注册 `subagent_{key}` 派发工具并可自动派发、过程经 `moai.subagent` 事件实时展示）、发布快照内嵌定义 `team_agent_defs`、`X-Moai-Team-Agent-Id` 切换、具名派发与声明即边界白名单——设计以 [../app/sdd.md](../app/sdd.md) D52/D53 为真源，行为 [@AP-S150](../app/bdd.md#ap-s150)~[@AP-S159](../app/bdd.md#ap-s159)，E2E `local-dev/team-agent-app-e2e.mjs`）

## 1. 目标

团队级"智能体"资产库：每个智能体 = **1 个主智能体 + N 个命名子智能体**，各自带系统提示词 / 工具依赖声明（插件 + 知识库 + 知识图谱）。为开放式任务的自主规划协作（对标 opencode 的 agent/subagent 体系）提供团队级定义资产：**Agent 应用是运行入口，团队智能体是被绑定的大脑**（Phase 2 落地绑定与 `subagent_{key}` 派发）。本期交付管理面：团队「智能体」菜单 + CRUD + 依赖声明与校验。**智能体不自带偏好模型**——模型归 agents 应用的对话模型配置。

## 2. 数据模型

- `team_agent`：`id(uuid) / team_id / key(30) / name(50) / description(500) / avatar_object_key / enabled / main_prompt(text) / main_plugin_ids(text JSON) / main_wiki_ids(text JSON) / main_graph_ids(text JSON) / subs(text JSON)` + 审计（软删除）
- `key`：智能体标识，团队内唯一，`^[a-z][a-z0-9_]{0,29}$`，**创建后不可变更**（Phase 2 作为 `agent_{key}`/工具名暴露）；`name` 为展示名（可中文），团队内唯一
- `subs` 为子智能体数组 JSON（`TeamAgentSubItem`：`key / name / description / prompt / pluginIds[] / wikiIds[] / graphIds[]`），**上限 20 个**；子智能体同样 key（`subagent_{key}` 工具名）+ 展示名；JSON 列先例 `app_agent_config`
- `is_system`：系统内置智能体标记（见 §5c）
- partial 唯一 `(team_id, name) WHERE is_deleted = 0` 与 `(team_id, key) WHERE is_deleted = 0 AND key <> ''`（key 排除空串：存量行兼容，新行必填即受约束）；删除后同名/同 key 可重建
- 依赖列存 uuid 字符串数组 / 数字数组（知识库/图谱 id long）；无物理外键（仓库约定）
- DDL：`asserts/team-agent.sql`（存量库；新库由 EnsureCreated 建成；早期草稿表的 `main_model_id` 列由幂等 DDL 回收）

## 3. 权限矩阵（Handler 层 `TeamAgentAccessGuard`，复用团队角色）

| 操作 | Owner/Admin | Member | 非成员 |
|---|---|---|---|
| 创建/更新/删除/头像 | ✅ | 403 | 404 |
| 列表（含 myRole）/ 详情 | ✅ | ✅ | 404 |

平台管理员视同放行（与 SkillAccessGuard 同口径）。

## 4. API

| 方法 | 路由 | 说明 |
|---|---|---|
| POST | `/api/team-agent` | 创建 `{teamId, key, name, description?, enabled, avatar?, mainPrompt, mainPluginIds[], mainWikiIds[], mainGraphIds[], subs[]}` → `{value: guid}` |
| POST | `/api/team-agent/seed-builtin` | 存量团队补种内置智能体（幂等），仅平台管理员 → `{value: 本次种入数}` |
| PUT | `/api/team-agent/{id}` | 更新（整文档替换，含 subs；不含 key——创建后不可变更） |
| DELETE | `/api/team-agent/{id}` | 软删除（内置智能体 400 拒绝） |
| GET | `/api/team-agent/list?teamId=&searchText=` | 团队内列表（含 myRole、key、subCount、isSystem、审计字段；内置排序在前） |
| GET | `/api/team-agent/{id}` | 详情（解析 subs 与依赖 JSON） |
| POST | `/api/team-agent/{id}/avatar` | 设置头像（objectKey 须已上传登记） |
| PUT | `/api/team-agent/{id}/enabled` | 启用/停用（列表卡片直切，不走整文档替换；内置智能体同样可启停） |

校验（`IModelValidator` + Handler）：key/name 均必填（key 正则 `^[a-z][a-z0-9_]{0,29}$`、name ≤50）、团队内 key/name 双唯一 409、描述 ≤500、主提示词必填 ≤50000、子提示词必填 ≤30000、子智能体 ≤20、`subs[].key` 同正则且组内唯一、`subs[].name` 必填 ≤50、`subs[].description` 必填 ≤500。

## 5. 依赖声明（插件 + 知识库 + 知识图谱，决策 D3）

依赖校验与应用配置绑定同口径（`TeamAgentDependencyGuard`，直查共享表）：

- **插件**：团队自有，或系统插件中已公开/已授权本团队（`PluginTeamAuthorizations`）
- **知识库**：仅本团队（`WikiEntity.TeamId`）
- **知识图谱**：仅本团队托管图（`Mode == KnowledgeGraphModes.Managed`，接入图不支持应用侧检索绑定）
- 主智能体与全部子智能体的依赖合并后统一校验、按序归一去重落库
- **弱依赖语义**：依赖声明是"能力建议"而非强限制——Phase 2 应用绑定智能体时，若应用未配置所需插件/知识库/图谱，**提示管理员"一键添加"补齐**（自动绑定声明项），不阻断绑定；子智能体 deps 仍作为运行时"工具白名单"（声明即边界，子会话仅注入声明项）

## 5b. 前端设计（团队页「智能体」分区）

- 菜单：`TeamManage.tsx` SECTION_KEYS 增 `agents`（成员可见，管理按钮 `canManage` 门禁），图标 DeploymentUnitOutlined
- 列表页 `TeamAgents.tsx`：**卡片列表**（同团队提示词款：头像 + 名称 + 启停/内置 Tag + 子智能体数徽标 + 两行省略描述 + 底栏操作与更新时间，响应式栅格 12/24/48 分页，空态 Empty）；**管理员卡片直切启停**（Switch size=small，乐观更新本地状态失败回滚，走专用 `PUT {id}/enabled` 不拉详情整替换），成员视图启停为只读 Tag；工具栏单行 `QueryBar`（搜索框 → 查询 → | → 新建，仅一个搜索框不传 onReset、不放刷新按钮，规范 frontend-conventions §7）；**Dify 式分层**：`TeamManage` 的 agents 分支不套外层白卡 `DSCard`——工具栏/分页裸排在页面底色上，每张智能体卡自带白色表面，数据量少时不出现"小容器大白板"；删除 Popconfirm（内置智能体无删除入口）；底栏操作为纯图标 + aria-label（卡片紧凑场景），成员底栏仅「详情」文字按钮
- 编辑页 `TeamAgentEditor.tsx`（独立页路由 `/team/:teamId/agent/new`、`/team/:teamId/agent/:agentId/edit`，内容大不塞弹窗）：**无卡片容器，左右 3:7 分栏**（Col lg 7/17）——「主智能体」区左侧信息列（头像直传 + key（编辑态禁用，addonBefore="key"）/名称同行、描述、插件/知识库/知识图谱三依赖 Select）右侧主提示词 VditorMarkdownEditor（定高 620px 填充容器）；**不含启停 Switch**（列表卡片直切，保存时保留原 enabled 值）；依赖为弱依赖不展示提示文案；页面顶行为「← 返回」链接（带图标回列表）+「保存」按钮相邻左对齐（操作集中在视线起点，不甩到页面最右）；「子智能体」区每项同构 3:7（左=key/名称/依赖/描述，右=与主智能体同款 Vditor 定高 420px，Form.Item 经 value/onChange 注入），分隔线用 token.colorSplit；加载态 Spin 包裹；依赖下拉与选中 Tag 均带头像（renderDepOption：Avatar 18 + 名称，label 节点即 Tag 内容）
- 详情弹窗 `TeamAgentDetailModal.tsx`：只读主信息 + 子智能体定义列表；依赖 id 经团队选项映射为名称 Tag
- API 封装 `ui/src/api/team-agent.ts`（Kiota 生成物之上，页面只调封装层）；i18n `teamAgents.*` + `team.teamAgents`，zh/en 同步

## 5c. 系统内置智能体（规划师 Plan / 构建师 Build）

- **定义在代码**：`TeamAgent.Core/Services/BuiltinTeamAgents.cs` 为唯一事实源（主提示词 + 子智能体 researcher/analyst/planner、executor/reviewer/fixer，依赖为空=通用可用）
- **自动种入**：团队创建时 `CreateTeamCommandHandler` 调用 `ITeamAgentSeeder.EnsureSeededAsync(teamId)`（Team.Core → TeamAgent.Core 单向引用，无环）；**按名称幂等**（同名已存在即跳过），种入失败仅告警不阻断团队创建
- **存量补种**：`POST /api/team-agent/seed-builtin`（仅平台管理员）遍历全部团队幂等补种
- **保护语义（决策 D7）**：`is_system=true` 的行**不可删除**（400，保住"每个团队都有 plan/build"的兜底）、**可编辑**（团队可定制提示词/依赖/启停；编辑后按名称幂等的补种不会重复种入——名称仍在）

## 6. 关键决策

- **D1** 模块命名 `MoAI.TeamAgent`：避让 `AppAgent*`/`SubAgent*`/`Microsoft.Agents.AI` 概念；BDD 缩写 **AG**（TA 系列已退役不复用）
- **D2** 子智能体与依赖存 JSON text 列：上限 20、整读整写、无跨行查询诉求，与 app_agent_config 先例一致，免建子表
- **D3** 依赖双角色（见 §5）：**弱依赖**——绑定缺失提示"一键添加"不阻断；子智能体 deps 仍为运行时白名单（声明即边界），随 Phase 2 派发生效
- **D4** key/name 双字段：key 复用技能 key 命名规则（小写蛇形，创建后不可变更），Phase 2 作为 `subagent_{key}` 工具名；name 为展示名（可中文）；主与子智能体一致
- **D5** Phase 1 纯管理面：不接应用绑定、不派发子会话、不上市场；`enabled` 停用位提前就位（Phase 2 绑定校验用）
- **D6** 更新为整文档替换（含 subs 全量覆盖），与编辑页一次保存心智一致
- **D7** 内置智能体 = 每团队独立副本（非 team_id=0 共享行）：随团队删除语义天然一致；不可删除可编辑——保兜底又给定制自由；定义在代码，未来"恢复出厂"可按名称重置

## 7. 已知问题 / 下阶段（Phase 2）

- ~~Agent 应用绑定团队智能体（绑定预检：依赖 ⊆ 应用绑定资源；发布快照携带智能体定义）~~ **已落地**（2026-11-05，[../app/sdd.md](../app/sdd.md) D52：绑定字段/耦合校验/主依赖预检 + 发布快照内嵌 `team_agent_defs`）
- ~~运行时派发：改造 `SubAgentInvoker`/`spawn_subagent` 为具名子智能体引用，子会话注入子智能体提示词 + 依赖白名单工具，深度恒 1~~ **已落地**（同上：`subagent_{key}` 具名工具替代通用 spawn、子会话提示词注入、声明即边界白名单、`X-Moai-Team-Agent-Id` 切换）
- 待办：派发统计（走统计模块新增 agent 类别）、子会话审计可见性（userType=subagent）
- 待办：市场上架（deps 作为跨团队安装要求重映射）、toolKey 级插件依赖、子智能体提示词团队变量插值
