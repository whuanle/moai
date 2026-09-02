# OAuth 连接器（OauthConnect，OC）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @OC-S3 | [local-dev/audit-345.mjs](../../local-dev/audit-345.mjs)（"5-connections GET 200"） | PASS（2026-09-02） |
| @OC-S4、@OC-S16 | 同上（"5-DELETE 200 且软删除生效"） | PASS（2026-09-02） |
| @OC-S5 | 同上（"5-create feishu 200" + authorizeUrl=飞书固定地址断言） | PASS（2026-09-02） |
| @OC-S8 | 同上（"5-重名创建被拒"） | PASS（2026-09-02） |
| @OC-S12 | 同上（"5-PUT 200"，缺陷修复后实测） | PASS（2026-09-02） |
| @OC-S7、@OC-S18 ~ @OC-S20 | @manual 本地模拟 OIDC Provider 全链路 12/12（记录见 [../user-management/sop.md](../user-management/sop.md) 历史验收存档·第三轮） | PASS（2026-09-02） |
| @OC-S1、@OC-S2、@OC-S6、@OC-S9 ~ @OC-S11、@OC-S13 ~ @OC-S15、@OC-S17 | @manual HTTP 走查（[SOP 第 3 节](./sop.md)） | PASS（2026-09-01，第二轮深度 API 68/68 互证） |
| @OC-S21 ~ @OC-S24 | @manual 浏览器走查（[SOP 第 3 节](./sop.md)） | PASS（2026-09-01/02，全页面回归） |

## 回归命令

```bash
node local-dev/audit-345.mjs   # 需后端运行于 :5210；脚本含轮3/4 断言，OC 相关 6 条
```

## 覆盖率说明

- 自动化 5 项（列表/软删除/建飞书/重名/PUT/删）覆盖核心写路径；创建 Custom、钉钉、401/403 门禁为手动（需可达的外部 OIDC 端点与 member 账号）。
- 已知未覆盖：Custom 发现端点不可达的 500 路径（[@OC-S11](./bdd.md#oc-s11)，缺陷记录未修复，不自动化）。
