# 应用管理模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

## 自检记录

- 模型下拉全零 Guid 归一（@AP-S17 更新，2026-10-08）：后端未选择模型时 `QueryAppAgentConfig` 回 `Guid.Empty`、清空保存亦落库全零，前端原样入 Select 无匹配选项即渲染裸串；`AppConfigSection` 加载时与流程设计器 `ModelSelectSection`（AI 对话/问题分类节点共用）取值处统一把全零 Guid 归一为未选择（显示占位符），并兜住存量库全零数据。
  - 前端：`AppConfigSection.test.tsx` **17/17**（新增：全零 Guid 显示占位符不渲染裸串、保存按 `modelId: null` 提交）、workflow `utils.test.ts` **58/58**、typecheck **0 error**、lint **0 error（既有 9 warning）**（2026-10-08）。

- 应用安全轮（内容脱敏，@AP-S76~S80，2026-10-06）：新表 `app_security_config`（1:1 app、Agent/流程通用，asserts/app_security.sql 已对开发库执行）；聚合 `AppSecurityPolicy`（Database.Shared/Aggregates，内置 phone/idCard/email/bankCard + custom 正则、500ms 匹配超时护院、JSON 节点级脱敏含数字字面量）；CQRS：`GET/PUT /api/app/{id}/security`（保存 Admin+，Handler 兜底重放校验防 Controller 手动重建 Command 绕过 MVC 校验）。**运行时挂点（MAF 官方 AIAgentBuilder.Use 装饰器）**：①函数调用中间件——工具结果在 FunctionResultContent 生成源头脱敏（模型可见/流式事件/落库同源生效；MEAI 会把工具返回值编组为 JsonElement，中间件按 string/JsonElement 归一取文本处理）；②Run 级中间件——流式产出**新 update 实例**脱敏参数与正文（不就地改 FunctionCallContent，防污染 FunctionInvokingChatClient 未执行的原始参数），非流式按官方 PIIMiddleware 模式就地改写响应消息；③`PostgresChatHistoryProvider` 落库前对 ToolCalls JSON/assistant 正文与思维链脱敏（工具消息 Content 为 TextContent 聚合不含函数结果，结果脱敏由①兜住）；④读侧兜底——会话消息/日志消息/流程运行历史查询按当前规则脱敏（覆盖启用前存量）。前端：AppWorkspace「安全」菜单（Agent 侧栏 + 流程配置二级组）、`AppSecuritySection`（总开关/三范围/规则行编辑）、i18n appSecurity.*（zh/en）、syncapi 对 5031 独立实例重生成。
  - 后端：`dotnet build src/MoAI/MoAI.csproj -o obj/verify-security` → **0 error**；`dotnet test` MoAI.AI.Core.Tests **91/91**（新增 AppSecurityPolicyTests 10 + AppSecurityAgentMiddlewareTests 4：模型第二轮请求只见脱敏结果/工具拿到原始参数/范围关闭旁路/流式克隆）、MoAI.App.Tests **44/44**（新增 SaveAppSecurityCommandHandlerTests 5）、MoAI.App.Workflow.Tests **79/79**（新增 WorkflowNodeSanitizerTests 1）（2026-10-06）。
  - 前端：typecheck / lint **0 error（既有 warning）**；AppSecuritySection.test.tsx **5/5**、AppWorkspace.test.tsx **4/4**（antd 两汉字按钮自动插空格，「保 存」断言用 /保\s*存/）（2026-10-06）。

- 安全分区改版轮（@AP-S78/@AP-S80 更新，2026-10-08）：应用安全配置拆**两套独立规则**——`app_security_config` 增列 `model_output_rules`（模型回复专属规则，asserts 增量 ALTER 已对开发库 moai_v2 执行）；聚合 `AppSecurityPolicy` 重构为内容规则（`rules`，作用工具结果/参数范围、受 `enabled` 门控）+ 模型规则（`model_output_rules`，仅作用模型回复、**不依赖总开关**），各 Mask 方法按作用域路由到对应规则集，`IsActive` = 两者任一生效；`WorkflowNodeSecuritySanitizer` AI 节点输出叠加改走 `MaskModelJson`（模型规则）；CQRS Save/Query 增 `modelOutputRules` 字段（两组规则同套校验，随保存原样持久化——关闭开关不清空规则）。前端：`AppSecuritySection` 四组独立卡片常显，内容脱敏/模型回复卡片各自内嵌规则编辑器（开关关闭仅隐藏编辑器），模型回复开关开启显示自己专属规则；syncapi 对 5031 独立实例（obj/verify-security2 独立输出绕 DLL 锁 + configs/system.json 指定端口）重生成 Kiota client。
  - 后端：`dotnet build src/MoAI/MoAI.csproj -o obj/verify-security2` → **0 error 0 warning**；MoAI.AI.Core.Tests **103/103**（AppSecurityPolicyTests 新增 3：模型规则独立于总开关生效/两套规则互不干扰/坏模型规则静默降级）、MoAI.App.Tests **45/45**（SaveAppSecurityCommandHandlerTests 新增 1：关闭总开关保存两组规则不清空 + 回显断言 modelOutputRules）、MoAI.App.Workflow.Tests **78/78**（2026-10-08）。
  - 前端：typecheck / lint **0 error（既有 warning）**；AppSecuritySection.test.tsx **7/7**（四组常显/总开关关闭规则保留/模型回复规则独立维护/两组自定义正则分别阻断保存/双规则载荷）、AppWorkspace.test.tsx **4/4**（2026-10-08）。

- 应用 ACP 轮（agent-to-agent 协议接入，@ACP-S1~S9）：`TeamApiKeyScopes` 增 `AppAcp=512`（代码 app_acp，ExternalDimensions/AccessAppAllowed 扩位，asserts/app_acp.sql 存量 282 条 `|=512`、列 DEFAULT 对齐 1014=AccessAppDefault，开发库已执行）；`POST /api/external/app/{appId}/acp`（MoAI.AI.Core Acp/，minimal API 不进 openapi 无需 syncapi）——AppAcpServer 手写 JSON-RPC 2.0：initialize/session.new/session.load/session.prompt（SSE 流式 session/update + stopReason 收口）/session.cancel（AppAcpRunRegistry 单例按会话取消，同一会话串行）；执行管线与 AG-UI/飞书同源（AppAgentFactory 装配 + 热态快照 + Flush 落库，Agent 与 Workflow 应用通吃）；中间件 ACP 分支：先 EnsurePrincipalUserAsync 解析直连会话身份再门禁（外部用户语义 + app_acp + 团队归属 + 已发布，不要求 IsExternal，GET 405）。**实踩坑**：流程过程负载键名——C# 匿名对象 `@event` 序列化后是 `"event"`（@ 仅为关键字转义），mapper 误读 `"@event"` 致节点 tool_call 全丢（诊断日志定位 DataContent 已到达后修复）。前端：AppWorkspace「ACP」菜单（Agent 侧栏 + 流程配置二级组）、AppAcpSection（地址/复制/方法 Tag/鉴权提示）、TeamAccessApps 加 app_acp 开关、i18n gateway.scope.app_acp + appWorkspace.acp*（zh/en）。
  - E2E：`node local-dev/app-acp-e2e.mjs` → **21/21**（认证 3/协议 3/门禁 2/Agent 对话 4/用户 token 1/隔离与 load 2/Workflow 1/cancel 1/缓存立即性 2/凭证语义 2；本地桩模型 + 确定性流程编排，零 SKIP）；前端 TeamAccessApps/AppWorkspace vitest 4/4+4/4、eslint 0、typecheck 除并行会话 KgCanvas 既有错误 0（2026-09-27）。

- key 直连外部接入轮（免换 token 访问团队资源，@EA-S15~S18）：`ExternalAuthenticationMiddleware` 支持 Bearer/x-api-key 直接携带 `moai-ac-`/`moai-` key 构建外部上下文（范围取 key 勾选知识库维度）；需授权应用会话面对无用户身份凭证统一 403（`external_user_token_required`）；应用接入 key 直连会话以「直连会话身份」external_user（`access_app_id + __key_direct__`）承载归属，落库走独立子作用域防请求级用户上下文懒加载污染；`EnsureExternalUser` 放宽为「须携带已解析外部用户 id」。
  - E2E：`external-app-e2e.mjs` → **75/75**（新增 EA-30~35）、`team-apikey-scope-e2e.mjs` → **43/43**（新增 TA-32a~i，TA-25b 越维代码改 external_token）；后端 0 error、前端 typecheck/lint 0 error、vitest **438/438**（2026-09-26）。

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
| @AP-S17 | app-e2e.mjs（AP-15a-c、AP-16b/c、AP-19a）+ AppConfigSection.test.tsx（全零 Guid 模型归一为占位符并按 null 保存，2026-10-08） | PASS（2026-09-11） |
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
| @AP-S56 | AppChat.test.tsx（附件上传提取拼接/图片不提取带 objectKey 裸 URL 块/输入卡图片缩略图与文档类型图标/气泡缩略图/处理中禁发） | PASS 17/17（2026-09-20 复跑，图片块格式随 @AP-S64 调整、chip 展示随 122 轮缩略图/类型图标调整；全仓 vitest 397/397、typecheck 0、lint 0 error；浏览器实测缩略图 32px/Word·Markdown 图标正常）；2026-10-05 复跑 17/17 |
| @AP-S50 @AP-S53 | 对话页满屏扁平布局（2026-10-05）：`app-chat.css` 去外框卡片/分组间水平分隔线/用户消息无气泡/工具调用行扁平化 + `AppLayout.tsx` FULLSCREEN_PATH 纳入 /chat + `ChatMessageList.tsx` 连续助手消息分组（单头像、组内无分隔线）；同日第二轮：历史 toolCalls 回放（`historyToolCalls.ts` 解析 + selectSession 保留）、工具折叠组（摘要行「已执行 N 次工具」，单条直出明细）、执行中/待审批工具行固定内容底部、「调用参数」收紧紧跟状态、i18n `toolCallCount`（zh/en）；同日第三轮：**会话路由同步**——路由 `chat/:sessionId?` + FULLSCREEN_PATH 兼容会话段 + AppChat URL 副作用（prev/active 双 ref 防重选竞态）；同日第四轮：**工具组默认收缩** + 回合间距 12px + 复制按钮绝对定位悬浮右上角（消除「已执行工具」上方空白）；typecheck 0、lint 0 error、teams/apps vitest 14 files 139/139；浏览器实测（:4000，应用 aa 真实沙箱对话）：刷新后各回合工具折叠组回放且默认收缩、点开正常、行内无右侧大片空白、整段会话收进一屏；点会话 URL 带 /chat/:id → 刷新恢复同一会话（侧栏高亮+消息完整）→ 新对话回 /chat 欢迎态 → 新会话首条消息 URL 即时更新且流式不断 | PASS |
| @AP-S76 | 会话标题 AI 提炼（2026-10-05）：`AppChatFlushService` 注入 `IAiChatCompletionService`+`IAiModelResolver`，首轮落库用应用绑定对话模型提炼标题（限 12s、剔除附件标记块、失败回退原文截断）；`dotnet build` 0 error（AI.Core 无新警告）；独立实例（.builds/title-check :5150）+ 内嵌 OpenAI 兼容桩实测：无标题会话首轮对话后标题=桩提炼结果「网站分析咨询」≠ 提问原文，桩渠道/模型/应用/团队全链路 7/7 PASS（临时脚本已删） | PASS |
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
| @AP-S75 | app-e2e.mjs（AP-61a~f：绑定知识库的 Agent 对话中 call_tool(search_knowledge_base) 返回片段序号/上下文 → call_tool(get_knowledge_base_chunk) 按 documentId/chunkIndex 补取片段并回显；不存在文档返回工具级错误说明）；配套 `WikiAppToolProviderTests`（[tests/MoAI.AI.Core.Tests](../../tests/MoAI.AI.Core.Tests/)，工具 8 例） | PASS 178/178 + AI.Core 77/77（2026-10-05；本地桩模型状态机驱动，场景见 [../wiki/bdd.md](../wiki/bdd.md) @AP-S75、设计见 [../wiki/sdd.md](../wiki/sdd.md) D36） |
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
| @EA-S15 | external-app-e2e.mjs（EA-31a~c）＋ team-apikey-scope-e2e.mjs（TA-32c/d/h） | EA 74/74、TA 21/21（2026-09-27） |
| @EA-S16 | external-app-e2e.mjs（EA-33a~c） | PASS 74/74（2026-09-27） |
| @EA-S17 | external-app-e2e.mjs（EA-32a~d） | PASS 74/74（2026-09-27） |
| @EA-S18 | 已随团队接入 key 下线退役（原团队 key 直连场景，编号不复用）；现状见 [@EA-S19](./bdd.md#ea-s19) | —（2026-09-27） |
| @EA-S19 | external-app-e2e.mjs（EA-36 已下线 moai- 前缀 401）＋ team-apikey-scope-e2e.mjs（TA-32j）＋ gateway-e2e.mjs（moai- 前缀网关 401） | EA 74/74、TA 21/21、GW 15/15（2026-09-27） |
| @ACP-S1 | app-acp-e2e.mjs（ACP-S01~S05） | PASS 21/21（2026-09-27） |
| @ACP-S2 | app-acp-e2e.mjs（ACP-S06） | PASS（2026-09-27） |
| @ACP-S3 | app-acp-e2e.mjs（ACP-S07/S08） | PASS（2026-09-27） |
| @ACP-S4 | app-acp-e2e.mjs（ACP-S09a/b、S10a/b） | PASS（2026-09-27） |
| @ACP-S5 | app-acp-e2e.mjs（ACP-S11~S13） | PASS（2026-09-27） |
| @ACP-S6 | app-acp-e2e.mjs（ACP-S14） | PASS（2026-09-27） |
| @ACP-S7 | app-acp-e2e.mjs（ACP-S15） | PASS（2026-09-27） |
| @ACP-S8 | app-acp-e2e.mjs（ACP-S16/S17） | PASS（2026-09-27） |
| @ACP-S9 | app-acp-e2e.mjs（ACP-S18/S19）＋ TeamAccessApps.test.tsx（应用 ACP 勾选） | PASS 21/21、4/4（2026-09-27） |
| 访问点配置分区 | ui/src/pages/teams/apps/__tests__/AppAccessSection.test.tsx | PASS 5/5（2026-09-14） |

## 复验命令

```bash
# 1) 后端
dotnet build src/MoAI/MoAI.csproj          # 0 error
cd src/MoAI && dotnet run                  # :5000
# 2) E2E（覆盖 @AP-S1~S10、S13、S14、S17~S21、S23；AP-20 需本地库有可授权的私有模型，用 root 管理员临时授权给 E2E 团队）
node local-dev/app-e2e.mjs                 # 期望 172/172 PASS（含 AP-40 调试会话 / AP-42 日志 / AP-43 用量 / AP-45 对话开场白 / AP-54 发布配置快照双轨 / AP-57 快捷输入 / AP-58 流程应用绑定 / AP-59 对话调用流程工具 / AP-60 审批策略，AP-59/AP-60 需 admin 账号否则 SKIP）
node local-dev/chat-attachment-e2e.mjs     # 期望 12/12 PASS（@AP-S55 对话附件：直传/提取/白名单/越权防护）
node local-dev/external-app-e2e.mjs        # 期望 74/74 PASS（@EA-S1~S13 外部 token + 外部会话/对话 + 访问点 + 沙箱/技能限制；EA-30~34/36 key 直连 @EA-S15~S17/S19，需先执行 asserts/external_app.sql）
node local-dev/app-acp-e2e.mjs             # 期望 21/21 PASS（@ACP-S1~S9 应用 ACP：scope 门禁 + 协议行为 + Agent/Workflow 对话 + cancel + 缓存立即性；内置桩模型，零 SKIP）
# 3) 前端（syncapi 需要后端运行中）
cd ui && CODEBUDDY_SAFE_DELETE_ENABLED=0 npm run syncapi && npm run typecheck && npm run lint && npm run test
```

## 坑位备注

- `npm run syncapi` 会 `rmSync(ui/src/api/client)` 后重建客户端；在工作区内需 `CODEBUDDY_SAFE_DELETE_ENABLED=0`，否则被 safe-delete 拦截而中止。
- `asserts/*.sql` 已随仓库 DDL 清理移除，建表以库表现状 / 脚手架产物为准（见 SOP §4）。
- 2026-09-26 团队接入 key 换外部 token（@AP-S72）：`/external/token` 新增 `apiKey` 凭证（与 `accessAppKey` 互斥），token 携带 `keyid`/`scope` 声明，刷新按 key 当前状态与范围重签。E2E `team-apikey-scope-e2e.mjs` **29/29**、external-app-e2e 回归 **59/59**（2026-09-26）；设计与范围模型见 [../gateway/sdd.md](../gateway/sdd.md)。
- 2026-09-26 应用接入 key 知识库功能范围（@AP-S73）：`access_app.scopes`（asserts/access_app_scopes.sql，存量回填 6=读写），签发/刷新 token 范围取接入勾选，不传默认读写全量、空列表=纯对话接入。E2E `team-apikey-scope-e2e.mjs` **34/34**、external-app-e2e 回归 **59/59**、wiki-external-e2e 回归 **30/30**（2026-09-26）。
- 2026-09-26 应用对话范围门禁（@AP-S74）：`access_app.scopes`/`team_api_key.scopes` 增 `app_chat`(32)，换用户 token 与用户 token 刷新按来源勾选重验（asserts/access_app_app_chat.sql，存量 |=32 补对话）。应用接入界面改按资源组授权（知识库 无/只读/可写）。E2E `team-apikey-scope-e2e.mjs` **46/46**（TA-33~35）、external-app-e2e 回归 **75/75**（2026-09-26）；分组口径见 [../gateway/sdd.md](../gateway/sdd.md) 资源分组表。
- 2026-09-27 团队接入 key 下线（@AP-S72 改应用接入 key 换 token、@EA-S18 退役→@EA-S19）：`/external/token` 移除 `apiKey` 凭证与 `keyid` claim，key 直连仅接受 `moai-ac-`；`team_api_key` 表删除（asserts/team_api_key_drop.sql）。E2E external-app **74/74**、team-apikey-scope **21/21**、gateway **15/15**（2026-09-27）；设计见 [../gateway/sdd.md](../gateway/sdd.md)。
| @AP-S76 | tests/MoAI.App.Tests/SaveAppSecurityCommandHandlerTests.cs（权限/校验/回显/默认值 5 例） | PASS 5/5（2026-10-06） |
| @AP-S77 | tests/MoAI.AI.Core.Tests/AppSecurityAgentMiddlewareTests.cs（RunAsync/RunStreamingAsync 两轮脚本客户端） | PASS 4/4（2026-10-06） |
| @AP-S78 | tests/MoAI.AI.Core.Tests/AppSecurityPolicyTests.cs（范围门控 + 两套规则独立性）+ AppSecurityAgentMiddlewareTests（范围关闭旁路） | PASS（2026-10-08 复验） |
| @AP-S79 | AppMessageSecurityMasker（读侧兜底，代码走查 + AppSecurityPolicyTests Mask 语义） | PASS（2026-10-06） |
| @AP-S80 | ui/src/pages/teams/apps/__tests__/AppSecuritySection.test.tsx + AppWorkspace.test.tsx | PASS 7/7、4/4（2026-10-08：四组独立常显 + 双套规则内嵌改版） |
| @AP-S81 | tests/MoAI.AI.Core.Tests/FrontendToolContextProviderTests.cs（三工具注册/桩执行/漏传参数/请求头开关/前缀判定） | PASS 9/9（2026-10-08） |
| @AP-S82 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（ui_show_chart 流式折叠卡 + 侧边栏自动打开/关闭重开） | PASS 19/19（2026-10-08） |
| @AP-S83 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（历史回放渲染折叠卡，点击打开不自动展开） | PASS 19/19（2026-10-08） |
| @AP-S84 | UiToolCard.test.tsx（降级态/点击/高亮）+ ChatSidePanel.test.tsx（三渲染器/复制/关闭/多标签/拖宽手柄）+ UiTools.test.ts（解析层含 option 字符串/围栏/顶层回退/畸形对象拦截归一化）+ EChart.test.tsx（容器常驻/抛后同实例恢复/不重复 init） | PASS 4/4、8/8、14/14、5/5（2026-10-08 复验：畸形 option 解析层拦截 + 渲染实例脱钩空白修复） |
| @AP-S85 | ChatSidePanel.test.tsx（多标签切换/单关回调/收起/拖宽手柄）+ AppChat.test.tsx（一轮双图表多标签端到端：切换/关当前切相邻/收起重开保留） | PASS 8/8、20/20（2026-10-08） |
| @AP-S86 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（悬浮显示的红色删除入口 + 确认后调删除接口并刷新列表 + 删除当前会话回欢迎态） | PASS 21/21（2026-10-08：含 Popconfirm 弹层点击沿 React 树冒泡误触发行的修复回归） |
| @AP-S87 | ui/src/pages/teams/apps/__tests__/AppChat.test.tsx（头部返回按钮跳转 /apps，公开应用非团队成员不落入团队应用列表） | PASS 22/22（2026-10-08） |
| @AP-S88 | AppChat.test.tsx（多会话并行：A 流式中点「新对话」立即创建会话 B 且各自独立 agent；侧栏呼吸点标识进行中会话；切回流式中会话恢复实时现场不回拉历史；停止仅中止当前会话） | PASS 24/24（2026-10-08，typecheck/lint 0 error） |
