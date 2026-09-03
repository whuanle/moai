# 分类管理（Classify）运维手册（SOP）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 证据：[local-dev/classify-e2e.mjs](../../local-dev/classify-e2e.mjs)
> 规范：[../DOC-STANDARD.md](../DOC-STANDARD.md)。场景编号引用 BDD，不重复步骤。

## 验收流程

按要求启动后端（:5210）后，执行 [@CLS-S1](../classify/bdd.md#cls-s1) 至 [@CLS-S11](../classify/bdd.md#cls-s11)：

1. 登录 admin / abcd123456，进入 `/classify`。
2. 依次点击 plugin/app/kb 三个 Tab，确认每类列表加载正常（[@CLS-S1](./bdd.md#cls-s1) 至 [@CLS-S3](./bdd.md#cls-s3)）。
3. 新增分类→列表刷新出现（[@CLS-S4](./bdd.md#cls-s4)）；连续新增同名被拒（[@CLS-S5](./bdd.md#cls-s5)）。
4. 修改分类名称并刷新确认（[@CLS-S7](./bdd.md#cls-s7)）；改名冲突被拒（[@CLS-S8](./bdd.md#cls-s8)）。
5. 删除未引用分类成功（[@CLS-S9](./bdd.md#cls-s9)）；删除有插件引用的分类被拒（[@CLS-S6](./bdd.md#cls-s6)）。
6. 用 member 账号访问被 403（[@CLS-S10](./bdd.md#cls-s10)）。

## 常见问题

| 症状 | 原因 | 处理 |
|---|---|---|
| 新增分类报 409 | 同类型下已存在同名分类 | 更换名称 |
| 删除分类报 409 | 该类型下仍有资源引用 | 先转移/删除资源 |
| 非管理员访问被 403 | 未通过 admin 门禁 | 用 admin 登录 |
| 参数非法报 400 | 名称/描述超长 | 名称 ≤20、描述 ≤255 |

## 种子说明

- `classify` 表种子含 `{plugin, app, kb}` 三类，各 33 项。需重置时由 `ClassifySeed` 在 EF 迁移时生成。
