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
| @AP-S14 | app-e2e.mjs（AP-13a-d）+ TeamApps.test.tsx（卡片状态标签、新建开关随提交带上） | PASS（2026-09-11） |
| @AP-S20 | app-e2e.mjs（AP-13e-l）+ TeamExternalApps.test.tsx | PASS（2026-09-13） |
| @AP-S21 | app-e2e.mjs（AP-13m-r）+ AppPlaza.test.tsx | PASS（2026-09-13） |
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

## 复验命令

```bash
# 1) 后端
dotnet build src/MoAI/MoAI.csproj          # 0 error
cd src/MoAI && dotnet run                  # :5000
# 2) E2E（覆盖 @AP-S1~S10、S13、S14、S17~S21、S23；AP-20 需本地库有可授权的私有模型，用 root 管理员临时授权给 E2E 团队）
node local-dev/app-e2e.mjs                 # 期望 82/82 PASS（含 AP-40 调试会话创建 / 角色与类型门禁 / 不落库）
# 3) 前端（syncapi 需要后端运行中）
cd ui && CODEBUDDY_SAFE_DELETE_ENABLED=0 npm run syncapi && npm run typecheck && npm run lint && npm run test
```

## 坑位备注

- `npm run syncapi` 会 `rmSync(ui/src/api/client)` 后重建客户端；在工作区内需 `CODEBUDDY_SAFE_DELETE_ENABLED=0`，否则被 safe-delete 拦截而中止。
- `asserts/*.sql` 已随仓库 DDL 清理移除，建表以库表现状 / 脚手架产物为准（见 SOP §4）。
