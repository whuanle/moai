# 静态插件（StaticPlugin）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/static-plugin-e2e.mjs](../../local-dev/static-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @STP-S1 | local-dev/static-plugin-e2e.mjs#merge-list | 待验证 |
| @STP-S2 | local-dev/static-plugin-e2e.mjs#merge-list-db-priority | 待验证 |
| @STP-S3 | local-dev/static-plugin-e2e.mjs#create-static | 待验证 |
| @STP-S4 | local-dev/static-plugin-e2e.mjs#update-static | 待验证 |
| @STP-S5 | local-dev/static-plugin-e2e.mjs#invalid-classify | 待验证 |
| @STP-S6 | local-dev/static-plugin-e2e.mjs#missing-plugin | 待验证 |
| @STP-S7 | local-dev/static-plugin-e2e.mjs#run-static | 待验证 |
| @STP-S8 | local-dev/static-plugin-e2e.mjs#run-missing | 待验证 |
| @STP-S9 | local-dev/static-plugin-e2e.mjs#non-admin | 待验证 |

## 前端测试

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| 抽屉打开与运行 | ui/src/pages/plugins/__tests__/StaticPluginPanel.test.tsx | 待验证 |
| 编辑弹窗写回 | ui/src/pages/plugins/__tests__/StaticPluginPanel.test.tsx | 待验证 |

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                                   # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test          # 前端全绿
node local-dev/static-plugin-e2e.mjs                                # 静态插件 e2e（需后端 5210 运行中）
```
