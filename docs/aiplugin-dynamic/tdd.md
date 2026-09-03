# 动态插件（DynamicPlugin）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景复述见 BDD，本文只做编号→验证物映射。

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @DYN-S1 | local-dev/dynamic-plugin-e2e.mjs#merge-list | 待验证 |
| @DYN-S2 | local-dev/dynamic-plugin-e2e.mjs#empty-list | 待验证 |
| @DYN-S3 | local-dev/dynamic-plugin-e2e.mjs#create-instance | 待验证 |
| @DYN-S4 | local-dev/dynamic-plugin-e2e.mjs#dup-with-template | 待验证 |
| @DYN-S5 | local-dev/dynamic-plugin-e2e.mjs#dup-with-instance | 待验证 |
| @DYN-S6 | local-dev/dynamic-plugin-e2e.mjs#invalid-key | 待验证 |
| @DYN-S7 | local-dev/dynamic-plugin-e2e.mjs#missing-template | 待验证 |
| @DYN-S8 | local-dev/dynamic-plugin-e2e.mjs#update-instance | 待验证 |
| @DYN-S9 | local-dev/dynamic-plugin-e2e.mjs#update-keep-key | 待验证 |
| @DYN-S10 | local-dev/dynamic-plugin-e2e.mjs#run-instance | 待验证 |
| @DYN-S11 | local-dev/dynamic-plugin-e2e.mjs#run-missing | 待验证 |
| @DYN-S12 | local-dev/dynamic-plugin-e2e.mjs#delete-instance | 待验证 |
| @DYN-S13 | local-dev/dynamic-plugin-e2e.mjs#delete-missing | 待验证 |
| @DYN-S14 | local-dev/dynamic-plugin-e2e.mjs#non-admin | 待验证 |

## 前端测试

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| 实例列表与模板加载 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS 3/3（2026-09-03） |
| 新建实例弹窗 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS（2026-09-03） |
| 删除实例 | ui/src/pages/plugins/__tests__/DynamicPluginPanel.test.tsx | PASS（2026-09-03） |

## 构建与回归

```bash
dotnet build src/MoAI/MoAI.csproj                                   # 后端 0 error
cd ui && npm run typecheck && npm run lint && npm run test          # 前端全绿
node local-dev/dynamic-plugin-e2e.mjs                               # 动态插件 e2e（需后端 5210 运行中）
```
