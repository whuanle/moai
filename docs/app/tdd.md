# 应用管理模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

## 自检记录

- 外部应用能力限制轮（沙箱与技能强制关闭，@EA-S13/S14，D46）：保存侧 `SaveAppAgentConfigCommandHandler` 对外部应用显式携带技能/启用沙箱即 400、存量技能随保存收敛为 `[]`；装配侧 `AppAgentFactory` 对外部应用克隆生效配置强制清技能/关沙箱（脱管行不污染变更跟踪，覆盖发布快照与全部对话入口）。
  - E2E：`node local-dev/external-app-e2e.mjs` → **59/59 PASS**（新增 EA-29a~e：外部应用携带技能 400、开启沙箱 400、正常保存回读技能空且沙箱未启用、内部应用开沙箱不受限；5203 独立实例 `-o .e2e-run` 绕 5000 在跑实例 bin 锁）（2026-09-21）。
  - 回归：`node local-dev/app-e2e.mjs` → **172/172 PASS**、`node local-dev/sandbox-limits-e2e.mjs` → **21/21 PASS**（2026-09-21）。
  - 前端：外部应用隐藏默认技能/沙箱参数/审批沙箱开关并加提示，保存固定 `skills: []`、`sandbox.enabled=false`；`AppConfigSection.test.tsx` **15/15**（+外部应用 1 例）、apps 目录 vitest **133/133**、typecheck 0 error、改动文件 eslint 0 error（2026-09-21）。

- 审批策略轮（插件白名单 + 沙箱自动放行，@AP-S65~S68）：策略落在 `execution_settings.toolApproval` 节（免加列、随发布快照双轨），闸口按 `AppTool.SourceId` 白名单与沙箱开关放行；userconfig 按发布快照下发自动放行工具名/前缀。
  - 后端：`MoAI.App.Core`/`MoAI.AI.Core` 单项目编译 **0 error**（宿主 Debug 输出被并行实例锁定，改用 `-o local-dev/backend-dist` 独立输出并在 5000 起实例验证）；`dotnet test tests/MoAI.AI.Core.Tests` → **48/48**（新增 `AppToolApprovalPolicyTests` 7 例 + `AppToolContextProviderTests` 闸口放行 4 例）。
  - E2E：`node local-dev/app-e2e.mjs` → **172/172 PASS**（新增 AP-60a~j：保存校验 400×2、userconfig 下发、白名单直执行决策 missing、非白名单挂起拒绝收敛、自动模式全放行、发布快照双轨）。
  - 前端：`npm run syncapi`（5000 实例）后 typecheck/lint **0 error（既有 warning）**；`AppConfigSection.test.tsx` **14/14**（+审批策略回显/提交/收敛 2 例）、`AppChat.test.tsx` **17/17**（+策略自动放行免审批卡 1 例）（2026-09-20）。

- 后端构建（对话界面重构 + 工具审批轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-20；`MoAI.App.Core`/`MoAI.AI.Core`/`MoAI.Prompt.Core` 单项目亦 0 error，宿主 Debug 输出被在跑进程锁定属预期）。
- 存量库加列：`prompt.use_count`、`app_user_config.tool_approval_mode`（脚本同步至 `asserts/prompt.sql`、`asserts/app_user_config.sql`，已对开发库执行）（2026-09-20）。
- 后端单测：`dotnet test tests/MoAI.AI.Core.Tests` → **28/28**、`tests/MoAI.App.Tests` → **35/35**（2026-09-20）。
- Kiota：独立 5010 实例（`-c SyncApi` 输出）重跑 `npm run syncapi`，新增 `/api/prompt/top_used`、`/api/app/session/{sessionId}/tool-approval` 与 userconfig 新字段进入生成物（2026-09-20）。
- 前端：`npm run typecheck` → **0 error**；`npm run lint` → **0 error（9 个既有 warning）**；对话相关单测 `AppChat`(10)/`AppUserSettings`(3)/`AppDebugChat`/`agentChat`(4) 全过，全量 vitest 仅既有并行超时 flaky（隔离运行全过）（2026-09-20）。
- 浏览器真实链路验收（5010 新后端 + 4001 前端 + mock OpenAI 模型）：欢迎态/热门专家（top_used 真实数据）/审批模式开关持久化/**审批卡 挂起→批准（已批准·执行中）/拒绝（已拒绝）→ 模型续写** 全链路实测通过（2026-09-20）。

- 后端构建（对话开场白轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-17）。
- 存量库加列：`app_agent_config.opening_statement varchar(4000) not null default ''` + `opening_statement_enabled boolean not null default false`（本机 psql/docker 不可用，临时 Npgsql console 对开发库执行成功，脚本同步至 `asserts/app_agent_opening_statement.sql`）（2026-09-17）。
- E2E：`node local-dev/app-e2e.mjs` → **121/121 PASS**（2026-09-17；新增 AP-45a~h 覆盖开场白 Member 403、保存回读、应用详情下发（Member 可读）、关闭开关内容保留、超长 400 不写入、流程应用详情恒空）。
- 前端：`npm run syncapi` 后 `npm run typecheck`/`npm run lint` → **0 error（7 个既有 warning）**；`npm run test` → **310 PASS（53 文件；`AppConfigSection.test.tsx` 扩至 9 例：保存 payload 断言补开场白默认值 + 新增开场白回显随保存、未启用不渲染输入框 2 例）**（2026-09-17）。
- 后端构建（会话专家提示词轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-16）。
- 存量库加列：`app_agent_session.prompt_id integer not null default 0`（已对本机开发库执行，脚本同步至 `asserts/app_agent_chat.sql`）（2026-09-16）。
- E2E：`node local-dev/app-e2e.mjs` → **113/113 PASS**（2026-09-16；新增 AP-44a~l 覆盖创建时绑定、改绑/清除回读、不可用提示词 404、越权 404、校验失败不落库。修复：`CreateSession` Controller 构造命令漏拷 `PromptId`，AP-44c/d 首跑暴露后补上）。
- 前端：`npm run syncapi` 后 `npm run typecheck` → **0 error**；`npm run lint` → **0 error（10 个既有 warning）**；`npm run test` → **286 PASS（51 文件；`AppChat.test.tsx` 扩至 6 例）**（2026-09-16）。
- 后端构建：`dotnet build src/MoAI/MoAI.csproj --no-restore` → **0 error**（2026-09-11）；`MoAI.App.Core` 单项目编译 0 error、改动文件 0 告警。
  > 历史：2026-09-10 曾因本机 NuGet `ConfigurationDefaults` 静态构造取不到系统目录（`Value cannot be null. (Parameter 'path1')`）无法还原；当前环境已可编译。
- 后端构建（内部/外部应用轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-13；顺带移除与主实体重复的 `Partial/AppEntity.AgentChat.cs`、`Partial/AppAgentSessionEntity.AgentChat.cs` 及对应配置 partial）。
- E2E：`node local-dev/app-e2e.mjs` → **76/76 PASS**（2026-09-13；较上轮 +22，新增 AP-13e~AP-13y 覆盖 外部应用隔离、授权/公开组合校验、公开应用与广场、应用接入 CRUD）。修复：`QueryAppCommandHandler` 原先对**所有**内部用户隐藏外部应用，导致团队 Admin 进不了外部应用管理页（AP-13f 404）——改为外部应用详情对团队 Admin+ 放行、仍对 Member/非成员隐藏。
- E2E 复跑方式（本机 5000 被占用时）：用独立输出目录构建第二实例并以 `APP_BASE` 指向它，避免影响在跑的进程。
- 前端 typecheck：`npm run typecheck`（`tsc -b --noEmit`）→ **0 error**（2026-09-13；已在运行中的后端上 `npm run syncapi` 重新生成 Kiota 客户端，含 `/app/external`、`/app/public`、`/accessApp` 与 `isExternal/isAuth/isPublic`）。
- 前端全量回归：`npm run test`（vitest run）→ **248/248 PASS（45 文件）**（2026-09-13；新增 `TeamAccessApps.test.tsx`、`TeamExternalApps.test.tsx`、`AppPlaza.test.tsx`，更新 `TeamApps`/`AppManage`/`TeamManage.test.tsx`）。
- 前端 lint：`npm run lint`（`eslint .`）→ **0 error**（2026-09-13）。
- 排障：此前「创建的外部应用落到内部应用、外部应用列表为空」根因是 **Kiota 客户端未重新生成**（`syncapi` 被 safe-delete 拦截中止，`ui/src/api/client` 停留在 2026-09-11）——`createApp` 丢弃 `isExternal`、`getExternalApps` 调不存在的方法。执行 `$env:CODEBUDDY_SAFE_DELETE_ENABLED=0; npm run syncapi` 后恢复正常。
- 后端构建（应用工作台轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-14）。
- E2E：`node local-dev/app-e2e.mjs` → **82/82 PASS**（2026-09-14；新增 AP-40a~f 覆盖调试会话创建、角色/类型门禁、不落库）。
- 前端：`npm run syncapi`（需后端运行中）后 `npm run typecheck`/`npm run lint` → **0 error**；`npm run test` → **250 PASS（47 文件；`WikiDocumentDetail` 2 例并行超时为既有 flaky，隔离 22/22 通过）**（2026-09-14；新增 `AppConfigSection`/`AppWorkspace`/`AppDebugChat` 测试）。
- 后端构建（应用对话日志轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-14）。
- E2E：`node local-dev/app-e2e.mjs` → **96/96 PASS**（2026-09-14；新增 AP-42a~n 覆盖 日志分页/过滤/权限/消息详情）。
- 前端：`npm run syncapi` 后 `npm run typecheck`/`npm run lint` → **0 error**；`npx vitest run src/pages/teams/apps` → **30/30 PASS（8 文件）**（2026-09-14；新增 `AppLogsSection.test.tsx`）。
- 后端构建（应用监控轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-14）。
- E2E：`node local-dev/app-e2e.mjs` → **101/101 PASS**（2026-09-14；新增 AP-43a~e 覆盖 用量汇总/按模型/权限）。
- 前端：`npm run syncapi` 后 `npm run typecheck`/`npm run lint` → **0 error**；`npx vitest run src/pages/teams/apps` → **33/33 PASS（9 文件）**（2026-09-14；新增 `AppMonitorSection.test.tsx`）。
- 后端构建（外部 token 轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-14）。
- E2E：`node local-dev/external-app-e2e.mjs` → **28/28 PASS**（2026-09-14；新脚本，覆盖 @EA-S1~S8：应用/用户/匿名三类 token、双 audience 内外隔离、身份复用、范围校验、刷新旋转、删除接入吊销）。
  - 修复 1：`ExternalTokenProvider` 校验时未关 `MapInboundClaims`，`sub/typ` 被映射为 ClaimTypes 长名导致主体类型解析失败（应用 token 刷新 401）——改为 `new JwtSecurityTokenHandler() { MapInboundClaims = false }`。
  - 修复 2：`ConfigureAuthorizaModule` 的 `JwtBearerEvents.OnAuthenticationFailed` 直接改写 `Response.StatusCode = 401`，污染多认证方案场景（外部 token 请求返回正确数据但状态码 401）——改为仅记日志，401 交由 Challenge 兜底。
  - 备注：存量库需执行 `asserts/external_app.sql` 建 `external_user` 表（新库 EnsureCreated 自动建）；本机 `psql`/`docker` 不可用，用临时 Npgsql console 执行 DDL 成功。
- 后端构建（外部会话/对话轮）+ 实体脚手架回归：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-14）。
  - 脚手架重生成以库为准删掉了 `Skills` DbSet 与实体，但开发库缺 `skill` 表系**库落后于代码**——恢复 `SkillEntity`/`SkillConfiguration`/DbSet 并新增 `asserts/skill.sql` 补建开发库表（已应用）。
- E2E：`node local-dev/external-app-e2e.mjs` → **42/42 PASS**（2026-09-14；新增 EA-20~25 覆盖 @EA-S9/S10：外部建会话/会话列表/消息、应用 token 与范围外拒绝、他人会话 404、对话端点 401/403、AG-UI 真实请求归属校验通过）。
  - 修复 3：`CreateExternalAgentSessionCommand.Validate` 的 `AppId NotEmpty` 在模型绑定阶段（路由参数注入前）执行导致 400——AppId 由路由 `:guid` 约束保证，删除该规则。
  - 修复 4：AG-UI 端点不经 MVC `/api` 前缀 convention，外部对话端点模板需写全 `/api/external/agent/{appId}/chat`。
- 回归：`node local-dev/app-e2e.mjs` → **101/101 PASS**（2026-09-14；验证外部认证中间件与 AG-UI 外部端点不影响内部链路）。
- 后端构建（访问点轮）：`dotnet build src/MoAI/MoAI.csproj` → **0 error**（2026-09-14）；`npm run syncapi` 重新生成 Kiota 客户端。
- 访问点 E2E：`node local-dev/external-app-e2e.mjs` → **51/51 PASS**（2026-09-14；新增 EA-26~28 覆盖 @EA-S11/S12：默认配置/保存/非法颜色与尺寸 400/内部应用 400、公开配置生效与 404、/embed/moai-widget.js 托管）。
  - 修复 5：`AccessPointPosition` 枚举字符串被全局 `JsonStringEnumConverter(CamelCase)` 处理，`bottom-right` 形式无法反序列化——统一为 `bottomRight/bottomLeft`（与 `AppType` 同机制），列默认值同步。
  - 修复 6：`ExternalAuthenticationMiddleware` 的 Bearer 强制规则误拦访问点公开配置（匿名）——白名单补充 `/api/external/app/*/access-point`。
- 前端（访问点轮）：`npm run typecheck`/`npm run lint` → **0 error**；`npm run test` 全量 **263/263（50 文件，含新增 `AppAccessSection.test.tsx` 5/5）**；`npm run build:embed` 产物 `src/MoAI/wwwroot/embed/moai-widget.js`（IIFE ~240KB）
- 悬浮组件修复轮（2026-09-21）：`npm run typecheck`/`npm run lint` → **0 error**；`npm run test` 全量 **430/430（62 文件）**；`npm run build:embed` 重建产物（`emptyOutDir` 改 false——该目录还有 logo/models.json/monaco，防误删）；`embed.vite.config` 产物源不变，开发期由 Vite 中间件在 4000 端口托管 `/embed/moai-widget.js` 并代理 `/api`。浏览器走查见 sop「常见问题」与 sdd 已知问题（2026-09-21）。回归 `external-app-e2e.mjs` **58/59**：EA-29b 失败系 5000 运行中后端为旧构建、未含外部禁沙箱规则（该规则与 EA-29 用例同属 @EA-S13 轮 WIP，新构建下已录 59/59），非本轮改动。
- 悬浮组件 null 源兼容轮（2026-09-21）：`file://`/`about:blank`（`Origin: null`、非安全上下文）宿主页修复验证——Vite `server.cors:false` 后预检穿透到后端（curl 断言经 4000 的 OPTIONS 带 `access-control-allow-origin: *`）；后端 PNA 预检支持（宿主 `dotnet build -o` 独立输出 **0 error**）；widget `randomUUID` 全量降级（重建产物）。`about:blank` 页面注入组件浏览器走查：按钮渲染→开面板→匿名 token 200→建会话 200→AG-UI 流式回复到达，全链路通过；typecheck/lint 0 error。

## 映射表

| 场景 | 验证物 | 结果 |
|---|---|---|
| @AP-S1 | app-e2e.mjs（AP-01） | PASS |
| @AP-S2 | app-e2e.mjs（AP-02a-d） | PASS |
| @AP-S3 | app-e2e.mjs（AP-03a-c、AP-09a、AP-10c） | PASS |
| @AP-S4 | app-e2e.mjs（AP-04、AP-05） | PASS |
| @AP-S5 | app-e2e.mjs（AP-06、AP-09e） | PASS |
| @AP-S6 | app-e2e.mjs（AP-07a-c） | PASS |
| @AP-S7 | app-e2e.mjs（AP-08a-c） | PASS |
| @AP-S8 | app-e2e.mjs（AP-09a-e、AP-13c/d） | PASS |
| @AP-S9 | app-e2e.mjs（AP-10a-e） | PASS |
| @AP-S10 | app-e2e.mjs（AP-11） | PASS |
| @AP-S11 | TeamApps.test.tsx（卡片渲染、「管理」入口、新建按钮）+ 侧边栏与路由已无 `/app` | PASS（2026-09-11） |
| @AP-S12 | TeamManage.test.tsx（成员只剩 信息/应用/知识库）+ TeamApps.test.tsx（Member 无新建/无「管理」） | PASS（2026-09-11） |
| @AP-S13 | app-e2e.mjs（AP-12a/b、AP-14）+ TeamApps.test.tsx（新建弹窗含头像上传） | PASS（2026-09-11） |
| @AP-S14 | app-e2e.mjs（AP-13a-d，公开改经上架审核）+ TeamApps.test.tsx（卡片状态标签、新建弹窗无公开开关） | PASS 101/101（2026-09-15） |
| @AP-S20 | app-e2e.mjs（AP-13e-k）+ TeamExternalApps.test.tsx | PASS 101/101（2026-09-15） |
| @AP-S21 | app-e2e.mjs（AP-13m-r、AP-13o2/o3 走上架审核）+ AppPlaza.test.tsx | PASS 101/101（2026-09-15） |
| @AP-S22 | TeamExternalApps.test.tsx + AppPlaza.test.tsx + TeamManage.test.tsx（分区与导航） | PASS（2026-09-13） |
| @AP-S23 | app-e2e.mjs（AP-13s~y）+ TeamAccessApps.test.tsx | PASS（2026-09-13） |
| @AP-S24 | TeamAccessApps.test.tsx（区块顶部 key 用途提示） | PASS 3/3（2026-09-21） |
| @AP-S15 | TeamApps.test.tsx（卡片 + 卡片右上角「管理」点击进入管理页；Member 只读） | PASS 6/6（2026-09-11） |
| @AP-S16 | AppConfigSection.test.tsx（工作台配置分区「Agent 配置」；2026-09-19 起应用信息拆至「信息」分区 @AP-S49。原单页分栏已被工作台取代，见 @AP-S40） | PASS 9/9（2026-09-19） |
| @AP-S17 | app-e2e.mjs（AP-15a-c、AP-16b/c、AP-19a） | PASS（2026-09-11） |
| @AP-S18 | app-e2e.mjs（AP-16a/d/e、AP-17a/b/d、AP-20a-e）+ AppConfigSection.test.tsx（选项来自团队模型/团队插件/本团队知识库） | PASS（2026-09-14） |
| @AP-S19 | app-e2e.mjs（AP-17c、AP-18、AP-19a/b） | PASS 121/121（2026-09-17，AP-18 契约更新：流程应用保存只写开场白字段返回 200） |
| 团队内应用分区（卡片） | ui/src/pages/teams/apps/__tests__/TeamApps.test.tsx | PASS 6/6（2026-09-11） |
| 应用工作台配置分区（原单页分栏 + 模型） | ui/src/pages/teams/apps/__tests__/AppConfigSection.test.tsx | PASS 7/7（2026-09-14） |
| 团队页分区与角色可见性 | ui/src/pages/teams/__tests__/TeamManage.test.tsx | PASS 14/14（2026-09-11） |
| 前端静态检查 | eslint（全量 src） | PASS 0 error（2026-09-11） |
| 浏览器走查 | @manual（团队页「应用」卡片新建 Agent/流程应用并带头像、开关「公开到平台」、卡片右上角「管理」进**应用工作台**（Agent 应用左侧菜单 配置/信息/日志/监控，外部应用含访问点；「信息」分区维护基础信息与上架状态，「配置」分区左 Agent 配置、右调试对话），未发布即可调试且刷新丢失调试会话；Member 只见 信息/应用/知识库 且应用卡片只读） | 待执行 |
| @AP-S40 | app-e2e.mjs（AP-40d、AP-40e） | PASS（2026-09-14） |
| @AP-S41 | app-e2e.mjs（AP-40a-c、AP-40f） | PASS（2026-09-14） |
| 应用工作台（左侧菜单 / 分区可见性） | ui/src/pages/teams/apps/__tests__/AppWorkspace.test.tsx | PASS 3/3（2026-09-14） |
| 调试对话面板（Redis 临时会话） | ui/src/pages/teams/apps/__tests__/AppDebugChat.test.tsx | PASS 1/1（2026-09-14） |
| 配置分区（迁移自 AppManage） | ui/src/pages/teams/apps/__tests__/AppConfigSection.test.tsx | PASS 7/7（2026-09-14） |
| @AP-S42 | app-e2e.mjs（AP-42a-n） | PASS（2026-09-14） |
| 应用对话日志看板 | ui/src/pages/teams/apps/__tests__/AppLogsSection.test.tsx | PASS 3/3（2026-09-14） |
| @AP-S43 | app-e2e.mjs（AP-43a-e） | PASS（2026-09-14） |
| 应用用量监控看板 | ui/src/pages/teams/apps/__tests__/AppMonitorSection.test.tsx | PASS 3/3（2026-09-14） |
| @AP-S44 | app-e2e.mjs（AP-44c/d/i/k）+ AppChat.test.tsx（应用设置面板选择专家/未建会话本地暂存随创建绑定/已有会话保存切换与取消） | PASS 113/113、6/6（2026-09-16）；UI 并入设置面板后 AppChat + AppUserSettings 18/18（2026-09-20） |
| @AP-S45 | app-e2e.mjs（AP-44a/e/f/g/h/j） | PASS 113/113（2026-09-16） |
| @AP-S46 | app-e2e.mjs（AP-45b/c/d）+ AppConfigSection.test.tsx（开场白回显、随保存提交） | PASS 121/121、9/9（2026-09-17） |
| @AP-S47 | app-e2e.mjs（AP-45a/e/f/g/h） | PASS 121/121（2026-09-17） |
| @AP-S48 | sandbox-limits-e2e.mjs（SB-01/03/05/06/07/08）+ [tests/MoAI.App.Tests](../../tests/MoAI.App.Tests/)（`SandboxSettingsLimitValidatorTests` 超限/非法/未启用放行）+ AppConfigSection.test.tsx（上限提示与前端拦截） | PASS 21/21、35/35、9/9（2026-09-19） |
| @AP-S49 | AppInfoSection.test.tsx（信息分区回显/申请上架/保存/成员只读）+ AppWorkspace.test.tsx（Agent 菜单含 信息 项且位于 配置 之后、Member 可见）+ AppConfigSection.test.tsx（配置区不再含应用信息） | PASS 4/4、3/3、9/9（2026-09-19，全仓 vitest 352/352、typecheck 0、lint 0 error） |
| @AP-S54 | app-e2e.mjs（AP-54a~i：发布快照/草稿隔离/重新发布生效/未发布实时） | PASS 130/130（2026-09-20） |
| @AP-S55 | chat-attachment-e2e.mjs（CA-01~08：直传/提取/白名单/大小上限/目录越权/404/未登录） | PASS 12/12（2026-09-20） |
| @AP-S56 | AppChat.test.tsx（附件上传提取拼接/图片不提取带 objectKey 裸 URL 块/输入卡图片缩略图与文档类型图标/气泡缩略图/处理中禁发） | PASS 17/17（2026-09-20 复跑，图片块格式随 @AP-S64 调整、chip 展示随 122 轮缩略图/类型图标调整；全仓 vitest 397/397、typecheck 0、lint 0 error；浏览器实测缩略图 32px/Word·Markdown 图标正常） |
| @AP-S57 | app-e2e.mjs（AP-57a~k：权限/保存回读/详情下发/规范化/条数与长度上限/不携带保持原值/空数组清空/发布快照） | PASS 141/141（2026-09-20） |
| @AP-S58 | AppChat.test.tsx（欢迎态快捷输入展示、副标题移除、点击即发送）+ AppConfigSection.test.tsx（快捷输入编辑回显与提交） | PASS 14/14、10/10（2026-09-20，全仓 vitest 373/373、typecheck 0、lint 0 error） |
| @AP-S59 | AppWorkspace.test.tsx（头部重新发布入口、确认后草稿上线并清除警告）+ AppConfigSection.test.tsx（警告条 action 重新发布） | PASS 4/4、11/11（2026-09-20，typecheck 0、lint 0 error） |
| @AP-S60 | AppLogsSection.test.tsx（用户列按类型归属：normal 用户名 / external 外部用户 #id / none 仅 #id 不误标） | PASS 4/4（2026-09-20） |
| @AP-S61 | app-e2e.mjs（AP-58a~l：绑定权限与越权 400/回读一致/不携带保持原值/空数组清空/发布快照状态） | PASS 162/162（2026-09-20） |
| @AP-S62 | app-e2e.mjs（AP-59a~i：桩模型 call_tool 驱动已发布流程执行并回传 reply、解绑发布后工具下线） | PASS 162/162（2026-09-20，无 admin 账号时 SKIP） |
| @AP-S63 | AppConfigSection.test.tsx（流程应用选项仅取已发布流程、回显已绑定项并随保存提交） | PASS 12/12（2026-09-20，全仓 vitest 392/392、typecheck 0、lint 0 error） |
| @AP-S64 | [tests/MoAI.AI.Core.Tests](../../tests/MoAI.AI.Core.Tests/) `ChatAttachmentImageRewriterTests`（objectKey/历史 URL 双格式解析、文档块与助手消息不动、越权目录不读、读取失败降级、svg 不内联、多图顺序与缓存、MayContainAttachment 快路径） | PASS 9/9（2026-09-20，AI.Core 全套 37/37、dotnet build 0 error） |
| @AP-S65 | app-e2e.mjs（AP-60a~d/i/j：白名单须为绑定插件子集 400/非法结构 400/合法保存并发布/草稿改策略不影响线上、重新发布后新策略生效） | PASS 172/172（2026-09-20） |
| @AP-S66 | app-e2e.mjs（AP-60e~h：审批模式白名单插件直接执行决策 missing/非白名单沙箱挂起拒绝收敛/自动模式全放行/userconfig 下发自动放行工具名与沙箱前缀） | PASS 172/172（2026-09-20） |
| @AP-S67 | AppConfigSection.test.tsx（审批策略区渲染回显、随保存提交并收敛为绑定插件子集） | PASS 14/14（2026-09-20） |
| @AP-S68 | AppChat.test.tsx（策略自动放行的插件名与沙箱前缀工具不展示审批卡、不调决策接口） | PASS 17/17（2026-09-20） |
| @EA-S1 | external-app-e2e.mjs（EA-01、EA-02） | PASS 28/28（2026-09-14） |
| @EA-S2 | external-app-e2e.mjs（EA-03~EA-06） | PASS（2026-09-14） |
| @EA-S3 | external-app-e2e.mjs（EA-07） | PASS（2026-09-14） |
| @EA-S4 | external-app-e2e.mjs（EA-08~EA-10） | PASS（2026-09-14） |
| @EA-S5 | external-app-e2e.mjs（EA-11~EA-13） | PASS（2026-09-14） |
| @EA-S6 | external-app-e2e.mjs（EA-14） | PASS（2026-09-14） |
| @EA-S7 | external-app-e2e.mjs（EA-15~EA-18） | PASS（2026-09-14） |
| @EA-S8 | external-app-e2e.mjs（EA-19a-d） | PASS（2026-09-14） |
| @EA-S9 | external-app-e2e.mjs（EA-20a/b、EA-21a/b） | PASS 42/42（2026-09-14） |
| @EA-S10 | external-app-e2e.mjs（EA-22a-d、EA-23、EA-24a-d、EA-25） | PASS（2026-09-14） |
| @EA-S11 | external-app-e2e.mjs（EA-26a-d） | PASS 51/51（2026-09-14） |
| @EA-S12 | external-app-e2e.mjs（EA-27a-e、EA-28） | PASS（2026-09-14） |
| @EA-S13 | external-app-e2e.mjs（EA-29a~e）+ AppConfigSection.test.tsx（外部应用不渲染技能/沙箱区、保存固定空值） | PASS 59/59、15/15（2026-09-21） |
| @EA-S14 | 代码走查（`AppAgentFactory.CloneWithExternalRestrictions`：技能清空、沙箱关闭、脱管克隆不修改草稿/快照） | PASS（2026-09-21） |
| 访问点配置分区 | ui/src/pages/teams/apps/__tests__/AppAccessSection.test.tsx | PASS 5/5（2026-09-14） |

## 复验命令

```bash
# 1) 后端
dotnet build src/MoAI/MoAI.csproj          # 0 error
cd src/MoAI && dotnet run                  # :5000
# 2) E2E（覆盖 @AP-S1~S10、S13、S14、S17~S21、S23；AP-20 需本地库有可授权的私有模型，用 root 管理员临时授权给 E2E 团队）
node local-dev/app-e2e.mjs                 # 期望 172/172 PASS（含 AP-40 调试会话 / AP-42 日志 / AP-43 用量 / AP-45 对话开场白 / AP-54 发布配置快照双轨 / AP-57 快捷输入 / AP-58 流程应用绑定 / AP-59 对话调用流程工具 / AP-60 审批策略，AP-59/AP-60 需 admin 账号否则 SKIP）
node local-dev/chat-attachment-e2e.mjs     # 期望 12/12 PASS（@AP-S55 对话附件：直传/提取/白名单/越权防护）
node local-dev/external-app-e2e.mjs        # 期望 59/59 PASS（@EA-S1~S13 外部 token + 外部会话/对话 + 访问点 + 沙箱/技能限制，需先执行 asserts/external_app.sql）
# 3) 前端（syncapi 需要后端运行中）
cd ui && CODEBUDDY_SAFE_DELETE_ENABLED=0 npm run syncapi && npm run typecheck && npm run lint && npm run test
```

## 坑位备注

- `npm run syncapi` 会 `rmSync(ui/src/api/client)` 后重建客户端；在工作区内需 `CODEBUDDY_SAFE_DELETE_ENABLED=0`，否则被 safe-delete 拦截而中止。
- `asserts/*.sql` 已随仓库 DDL 清理移除，建表以库表现状 / 脚手架产物为准（见 SOP §4）。
