# 动态插件（DynamicPlugin）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/dynamic-plugin-e2e.mjs](../../local-dev/dynamic-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景编号引用 BDD，不重复步骤。

## 验收流程

按要求启动后端（:5210）后，执行 [@DYN-S1](../aiplugin-dynamic/bdd.md#dyn-s1) 至 [@DYN-S14](../aiplugin-dynamic/bdd.md#dyn-s14)：

1. 登录 admin / abcd123456，进入 `/plugin?tab=dynamic`（[@DYN-S1](./bdd.md#dyn-s1) 至 [@DYN-S2](./bdd.md#dyn-s2)）。
2. 点击「新建实例」→ 选择模板、填实例 key/标题/描述/分类、Monaco 填写配置（[@DYN-S3](./bdd.md#dyn-s3)）；重复 key 被 409（[@DYN-S4](./bdd.md#dyn-s4) 至 [@DYN-S5](./bdd.md#dyn-s5)）；不合规 key/不存在模板被拒（[@DYN-S6](./bdd.md#dyn-s6) 至 [@DYN-S7](./bdd.md#dyn-s7)）。
3. 编辑实例标题/配置（[@DYN-S8](./bdd.md#dyn-s8)）；确认实例 key 不可改（[@DYN-S9](./bdd.md#dyn-s9)）。
4. 点击「运行」→ 抽屉运行，传入请求参数，返回正确结果（[@DYN-S10](./bdd.md#dyn-s10)）；运行不存在实例被 404（[@DYN-S11](./bdd.md#dyn-s11)）。
5. 删除实例成功（[@DYN-S12](./bdd.md#dyn-s12)）；删除不存在实例被 404（[@DYN-S13](./bdd.md#dyn-s13)）。
6. 非管理员（member）访问被 403（[@DYN-S14](./bdd.md#dyn-s14)）。

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 新建实例报 409 | 实例 key 与注册表 key 或其它实例重复 | 更换实例 key |
| 新建实例报 400 | 实例 key 大小写/开头不合规，或分类不存在 | 实例 key 全小写+下划线、≤30；分类留 0 或选已有 |
| 新建/编辑报 404「模板不存在」 | templeteKey 未注册或非动态 | 选择模板下拉中的动态插件 |
| 运行报 404「插件不存在」 | key 不是已创建实例 key | 先从列表选择实例运行 |
| 无法编辑实例 key | 实例 key 是主键，不可变 | 需删除重建 |

## 种子说明

- 动态实例默认无 DB 记录；创建实例后才在 `plugin_dynamic` + `plugin` 表生成记录。
- 内置动态模板：`dynamic_greet`（`DynamicGreetPlugin`，`MoAI.AIPlugin.Dynamic`）。
