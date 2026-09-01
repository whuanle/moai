# 前端管理页（Settings / OauthConnect）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射。两页无组件测试，场景均为手工走查；后端接口自动化见上游 TDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @FE-PG-S1 ~ @FE-PG-S16（全部场景） | @manual 浏览器走查（[SOP 第 4 节](./sop.md)） | PASS（2026-09-02，见 SOP 历史验收存档） |
| @FE-PG-S6（后端 403 门禁） | [../../../docs/settings/tdd.md](../../../docs/settings/tdd.md)、[../../../docs/oauthconnect/tdd.md](../../../docs/oauthconnect/tdd.md) | PASS（2026-09-02，见上游 TDD） |
| @FE-PG-S15（编辑 PUT 接口） | 后端修复后实测 `PUT /api/oauthconnect/connections/{id}` 返回 200（上游 [../../../docs/oauthconnect/sop.md](../../../docs/oauthconnect/sop.md) 排障表） | PASS 实测 200（2026-09-02） |
| 两页编译与全量回归 | `cd ui && npm run test`（13 文件 42 用例）+ `npm run typecheck` | PASS 42/42、0 错误（2026-09-01） |

## 回归命令

```bash
cd ui && npm run lint && npm run test && npm run typecheck
```

## 覆盖率说明

- 已知未覆盖：两页零组件自动化（表单校验、脏标记回滚、Modal 交互均走查）；补测试的标准流程见 [../dashboard-testing/sop.md](../dashboard-testing/sop.md) 第 2 节。
- 历史断言更新：2026-09-01 版本文档曾将「编辑提交」标记为预期 400（后端缺陷）；该缺陷已于后端修复，2026-09-02 实测 200，场景 [@FE-PG-S15](./bdd.md#fe-pg-s15) 改按成功路径验收。
