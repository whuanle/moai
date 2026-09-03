# 分类管理（Classify）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/classify-e2e.mjs](../../local-dev/classify-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @CLS-S1 | local-dev/classify-e2e.mjs#query-list | 待验证 |
| @CLS-S2 | local-dev/classify-e2e.mjs#query-list | 待验证 |
| @CLS-S3 | local-dev/classify-e2e.mjs#query-list | 待验证 |
| @CLS-S4 | local-dev/classify-e2e.mjs#create-classify | 待验证 |
| @CLS-S5 | local-dev/classify-e2e.mjs#duplicate-classify | 待验证 |
| @CLS-S6 | local-dev/classify-e2e.mjs#delete-used-classify | 待验证 |
| @CLS-S7 | local-dev/classify-e2e.mjs#update-classify | 待验证 |
| @CLS-S8 | local-dev/classify-e2e.mjs#update-duplicate-classify | 待验证 |
| @CLS-S9 | local-dev/classify-e2e.mjs#delete-classify | 待验证 |
| @CLS-S10 | local-dev/classify-e2e.mjs#non-admin | 待验证 |
| @CLS-S11 | local-dev/classify-e2e.mjs#validation | 待验证 |

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                                   # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test          # 前端全绿
node local-dev/classify-e2e.mjs                                     # 分类 e2e（需后端 5210 运行中）
```
