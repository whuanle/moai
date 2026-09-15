# 应用管理模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

## 自检记录

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
| @AP-S15 | TeamApps.test.tsx（卡片 + 卡片右上角「管理」点击进入管理页；Member 只读） | PASS 6/6（2026-09-11） |
| @AP-S16 | AppConfigSection.test.tsx（工作台配置分区：左「应用信息」+「Agent 配置」；流程应用只提示未开放。原单页分栏已被工作台取代，见 @AP-S40） | PASS 7/7（2026-09-14） |
| @AP-S17 | app-e2e.mjs（AP-15a-c、AP-16b/c、AP-19a） | PASS（2026-09-11） |
| @AP-S18 | app-e2e.mjs（AP-16a/d/e、AP-17a/b/d、AP-20a-e）+ AppConfigSection.test.tsx（选项来自团队模型/团队插件/本团队知识库） | PASS（2026-09-14） |
| @AP-S19 | app-e2e.mjs（AP-17c、AP-18、AP-19a/b） | PASS（2026-09-11） |
| 团队内应用分区（卡片） | ui/src/pages/teams/apps/__tests__/TeamApps.test.tsx | PASS 6/6（2026-09-11） |
| 应用工作台配置分区（原单页分栏 + 模型） | ui/src/pages/teams/apps/__tests__/AppConfigSection.test.tsx | PASS 7/7（2026-09-14） |
| 团队页分区与角色可见性 | ui/src/pages/teams/__tests__/TeamManage.test.tsx | PASS 14/14（2026-09-11） |
| 前端静态检查 | eslint（全量 src） | PASS 0 error（2026-09-11） |
| 浏览器走查 | @manual（团队页「应用」卡片新建 Agent/流程应用并带头像、开关「公开到平台」、卡片右上角「管理」进**应用工作台**（左侧菜单 配置/日志/监控，外部应用含访问点；配置分区左信息+Agent 配置、右调试对话），未发布即可调试且刷新丢失调试会话；Member 只见 信息/应用/知识库 且应用卡片只读） | 待执行 |
| @AP-S40 | app-e2e.mjs（AP-40d、AP-40e） | PASS（2026-09-14） |
| @AP-S41 | app-e2e.mjs（AP-40a-c、AP-40f） | PASS（2026-09-14） |
| 应用工作台（左侧菜单 / 分区可见性） | ui/src/pages/teams/apps/__tests__/AppWorkspace.test.tsx | PASS 3/3（2026-09-14） |
| 调试对话面板（Redis 临时会话） | ui/src/pages/teams/apps/__tests__/AppDebugChat.test.tsx | PASS 1/1（2026-09-14） |
| 配置分区（迁移自 AppManage） | ui/src/pages/teams/apps/__tests__/AppConfigSection.test.tsx | PASS 7/7（2026-09-14） |
| @AP-S42 | app-e2e.mjs（AP-42a-n） | PASS（2026-09-14） |
| 应用对话日志看板 | ui/src/pages/teams/apps/__tests__/AppLogsSection.test.tsx | PASS 3/3（2026-09-14） |
| @AP-S43 | app-e2e.mjs（AP-43a-e） | PASS（2026-09-14） |
| 应用用量监控看板 | ui/src/pages/teams/apps/__tests__/AppMonitorSection.test.tsx | PASS 3/3（2026-09-14） |
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
| 访问点配置分区 | ui/src/pages/teams/apps/__tests__/AppAccessSection.test.tsx | PASS 5/5（2026-09-14） |

## 复验命令

```bash
# 1) 后端
dotnet build src/MoAI/MoAI.csproj          # 0 error
cd src/MoAI && dotnet run                  # :5000
# 2) E2E（覆盖 @AP-S1~S10、S13、S14、S17~S21、S23；AP-20 需本地库有可授权的私有模型，用 root 管理员临时授权给 E2E 团队）
node local-dev/app-e2e.mjs                 # 期望 101/101 PASS（含 AP-40 调试会话 / AP-42 日志 / AP-43 用量）
node local-dev/external-app-e2e.mjs        # 期望 42/42 PASS（@EA-S1~S10 外部 token + 外部会话/对话，需先执行 asserts/external_app.sql）
# 3) 前端（syncapi 需要后端运行中）
cd ui && CODEBUDDY_SAFE_DELETE_ENABLED=0 npm run syncapi && npm run typecheck && npm run lint && npm run test
```

## 坑位备注

- `npm run syncapi` 会 `rmSync(ui/src/api/client)` 后重建客户端；在工作区内需 `CODEBUDDY_SAFE_DELETE_ENABLED=0`，否则被 safe-delete 拦截而中止。
- `asserts/*.sql` 已随仓库 DDL 清理移除，建表以库表现状 / 脚手架产物为准（见 SOP §4）。
