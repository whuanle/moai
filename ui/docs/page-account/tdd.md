# 前端账号设置页（AccountSettings）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。本页无组件测试（`ui/src/pages/account/` 下无 `__tests__`），全部场景为手工走查；后端同流程的自动化证据见上游 TDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-PA-S1 ~ @FE-PA-S20（全部场景） | @manual 浏览器走查（[SOP 第 4 节](./sop.md)） | PASS（2026-09-01，见 SOP 历史验收存档） |
| 改密/资料/绑定后端行为 | [../../../docs/account/tdd.md](../../../docs/account/tdd.md)（后端自助接口自动化） | PASS（2026-09-02，见上游 TDD） |
| 头像直传后端行为 | [../../../docs/storage/tdd.md](../../../docs/storage/tdd.md)（存储链路自动化） | PASS（2026-09-02，见上游 TDD） |
| 本页编译与全量回归 | `cd ui && npm run test`（13 文件 42 用例）+ `npm run typecheck` | PASS 42/42、0 错误（2026-09-01） |

## 回归命令

```bash
cd ui && npm run lint && npm run test && npm run typecheck
```

## 覆盖率说明

- 已知未覆盖：本页零组件自动化（表单校验、弹窗 message 链路均走查）；补测试的标准流程见 [../dashboard-testing/sop.md](../dashboard-testing/sop.md) 第 2 节（页面测试范式）。
- 走查前置：后端与至少一个第三方渠道（可用本地模拟 OIDC）就绪，见 [SOP 第 4 节](./sop.md)。
