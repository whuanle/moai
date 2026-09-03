# 静态插件（StaticPlugin）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/static-plugin-e2e.mjs](../../local-dev/static-plugin-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景编号引用 BDD，不重复步骤。

## 验收流程

按要求启动后端（:5210）后，执行 [@STP-S1](../aiplugin-static/bdd.md#stp-s1) 至 [@STP-S9](../aiplugin-static/bdd.md#stp-s9)：

1. 登录 admin / abcd123456，进入 `/plugins`，切到「静态插件」Tab（[@STP-S1](./bdd.md#stp-s1) 至 [@STP-S2](./bdd.md#stp-s2)）。
2. 确认列表展示内存发现 + DB 记录合并去重；无 DB 记录的项分类显示「未分类」，且有「运行」「编辑」操作。
3. 点击「运行」→ 右侧抽屉打开，Monaco 展示请求参数示例（[@STP-S7](./bdd.md#stp-s7)）；修改参数点击运行，展示结果。
4. 点击「编辑」→ 弹窗修改标题/描述/分类（[@STP-S3](./bdd.md#stp-s3) 至 [@STP-S4](./bdd.md#stp-s4)）；保存后刷新列表，该插件归入新分类。
5. 非管理员（member）访问被 403（[@STP-S9](./bdd.md#stp-s9)）。

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 静态插件显示在「未分类」 | DB 无该插件记录（首次发现） | 点击编辑任意字段写回，即可归入所选分类 |
| 保存报 400 | classifyId 不存在或非法 | 选择已有 plugin 分类，或留 0（未分类） |
| 保存报 404 | pluginKey 不在内存注册表 | 确认该插件已注册（重启后端加载） |
| 运行报 404「插件不存在」 | key 未注册 | 检查 `[AiPlugin]` 注解与 key |
| 运行报 400「请求参数解析失败」 | requestJson 与请求模型不匹配 | 以 Monaco 示例为准修改参数 |

## 种子说明

- 静态插件默认无 DB 记录（无实例）；仅当用户编辑写回后才在 `plugin` + `plugin_static` 表生成记录。
- 内置示例：`static_echo`（`StaticEchoPlugin`，`MoAI.AIPlugin.Static`）。
