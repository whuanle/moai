# 前端 Kiota API 层（api-layer）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-API-S3 | [Feedback/__tests__/feedback.test.ts](../../src/design-system/components/Feedback/__tests__/feedback.test.ts)：parseApiErrorResponse / resolveErrorMessage 优先级 / 4xx→message、5xx→notification 路由 | PASS 14/14（2026-09-01） |
| @FE-API-S4 | 同上：isNetworkError 判定 + 网络错误→notification 路由 | PASS（2026-09-01） |
| @FE-API-S1、@FE-API-S2、@FE-API-S5 | `ui/src/api/kiota.ts` FilterRequestHandler 走查（401 豁免/清态跳转/异常分流三分支） | PASS（2026-09-01，代码级） |
| @FE-API-S6、@FE-API-S7 | kiota.ts 工厂走查（Bearer/匿名 Provider、baseUrl） | PASS（2026-09-01，代码级） |
| @FE-API-S8 | 生成物现状快照：typecheck 0 错 + lock 版本/目录走查（本轮未重跑 syncapi，避免覆盖生成物） | PASS（2026-09-01，快照级） |
| @FE-API-S9 | 历史实测（1.34.x 必挂，见 [../../../docs/user-management/tdd.md](../../../docs/user-management/tdd.md)）；当前依赖精确锁 preview.93 | 历史证实（2026-09-01） |
| @FE-API-S10 ~ @FE-API-S12 | 5000 遗留值与 includes('login') 粗匹配走查（SDD 已知问题） | 走查一致（2026-09-01） |

## 回归命令

```bash
cd ui && npm run typecheck && npm run lint    # tsc 0 错；eslint 忽略 src/api/client（2026-09-01 实测）
cd ui && npx vitest run src/design-system/components/Feedback src/pages/users   # 18/18（2026-09-01）
cd ui && npm run test                          # 全量 42/42（2026-09-01）
grep -o '"kiotaVersion": "[^"]*"' src/api/client/kiota-lock.json               # 应为 1.27.0
```

## 覆盖率说明

- @FE-API-S3/S4 的归一化与路由函数有单测；**中间件链路（清态/跳转/重抛）为代码走查**，无组件级单测。
- syncapi 本轮未实际重跑（避免覆盖当前生成物/后端未运行误伤），以生成物快照 + lock 版本证据替代；完整操作按 [SOP 第 1 节](./sop.md)。
