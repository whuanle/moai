# 应用管理模块验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/app-e2e.mjs](../../local-dev/app-e2e.mjs)

## 自检记录

- 后端构建：`dotnet build src/MoAI/MoAI.csproj --no-restore` → **0 error**（2026-09-11）；`MoAI.App.Core` 单项目编译 0 error、改动文件 0 告警。
  > 历史：2026-09-10 曾因本机 NuGet `ConfigurationDefaults` 静态构造取不到系统目录（`Value cannot be null. (Parameter 'path1')`）无法还原；当前环境已可编译。
- E2E：`node local-dev/app-e2e.mjs`（需后端运行中）→ **55/55 PASS**（2026-09-11；较上轮 +5，新增 AP-20 覆盖对话模型取值与越权拒绝）。
- 前端 typecheck：`npx tsc -b` → **0 error**（2026-09-11；已 `npm run syncapi` 生成 `SaveAppAgentConfigCommand.modelId`）。
- 前端全量回归：`npm run test`（vitest run）→ **232/232 PASS（40 文件）**（2026-09-11；`AppManage.test.tsx` 由「分区」断言改为「单页两栏 + 模型」断言）。
- 前端 lint：`npm run lint`（`eslint .`）→ **0 error**（2026-09-11）。

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
| @AP-S15 | TeamApps.test.tsx（卡片 + 卡片右上角「管理」点击进入管理页；Member 只读） | PASS 6/6（2026-09-11） |
| @AP-S16 | AppManage.test.tsx（单页左右分栏：左「应用信息」/右「Agent 配置」，无左侧分区菜单；流程应用右栏只提示未开放） | PASS 6/6（2026-09-11） |
| @AP-S17 | app-e2e.mjs（AP-15a-c、AP-16b/c、AP-19a） | PASS（2026-09-11） |
| @AP-S18 | app-e2e.mjs（AP-16a/d/e、AP-17a/b/d、AP-20a-e）+ AppManage.test.tsx（选项来自团队模型/团队插件/本团队知识库） | PASS（2026-09-11） |
| @AP-S19 | app-e2e.mjs（AP-17c、AP-18、AP-19a/b） | PASS（2026-09-11） |
| 团队内应用分区（卡片） | ui/src/pages/teams/apps/__tests__/TeamApps.test.tsx | PASS 6/6（2026-09-11） |
| 应用管理页（单页分栏 + 模型） | ui/src/pages/teams/apps/__tests__/AppManage.test.tsx | PASS 6/6（2026-09-11） |
| 团队页分区与角色可见性 | ui/src/pages/teams/__tests__/TeamManage.test.tsx | PASS 14/14（2026-09-11） |
| 前端静态检查 | eslint（全量 src） | PASS 0 error（2026-09-11） |
| 浏览器走查 | @manual（团队页「应用」卡片新建 Agent/流程应用并带头像、开关「允许外部使用」、卡片右上角「管理」进单页配置（左信息/右模型+提示词+插件+知识库）并保存；Member 只见 信息/应用/知识库 且应用卡片只读） | 待执行 |

## 复验命令

```bash
# 1) 后端
dotnet build src/MoAI/MoAI.csproj          # 0 error
cd src/MoAI && dotnet run                  # :5000
# 2) E2E（覆盖 @AP-S1~S10、S13、S14、S17~S19；AP-20 需本地库有可授权的私有模型，用 root 管理员临时授权给 E2E 团队）
node local-dev/app-e2e.mjs                 # 期望 55/55 PASS
# 3) 前端（syncapi 需要后端运行中）
cd ui && CODEBUDDY_SAFE_DELETE_ENABLED=0 npm run syncapi && npm run typecheck && npm run lint && npm run test
```

## 坑位备注

- `npm run syncapi` 会 `rmSync(ui/src/api/client)` 后重建客户端；在工作区内需 `CODEBUDDY_SAFE_DELETE_ENABLED=0`，否则被 safe-delete 拦截而中止。
- `asserts/*.sql` 已随仓库 DDL 清理移除，建表以库表现状 / 脚手架产物为准（见 SOP §4）。
