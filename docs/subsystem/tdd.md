# 子系统接入（Subsystem，SUB）验证映射（TDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md)（场景定义） ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md)
> 本文只做「场景 → 验证物 → 结果」映射；场景语义见 BDD 编号，设计依据见 SDD。

## 场景映射表

| 场景 | 验证物 | 结果（日期） |
|---|---|---|
| @SUB-S1 | [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-01/02） | PASS 35/35（2026-10-28） |
| @SUB-S2 | @manual HTTP 走查（[SOP 第 4 节](./sop.md)） | PASS（2026-10-28） |
| @SUB-S3 | 同上（SUB-04：secret 长度 48、哈希入库由单测佐证） | PASS 35/35（2026-10-28） |
| @SUB-S4 | 同上（SUB-03） | PASS（2026-10-28） |
| @SUB-S5 | 同上（SUB-05） | PASS（2026-10-28） |
| @SUB-S6、@SUB-S7 | 同上（SUB-06/07） | PASS（2026-10-28） |
| @SUB-S8 | 同上（SUB-08） | PASS（2026-10-28） |
| @SUB-S9 | 同上（SUB-09/10） | PASS（2026-10-28） |
| @SUB-S10 | 同上（SUB-12/13） | PASS（2026-10-28） |
| @SUB-S11 | 同上（SUB-11） | PASS（2026-10-28） |
| @SUB-S12 | 同上（SUB-14） | PASS（2026-10-28） |
| @SUB-S13 | 同上（SUB-15） | PASS（2026-10-28） |
| @SUB-S14 | 同上（SUB-16/19） | PASS（2026-10-28） |
| @SUB-S15 | 同上（SUB-17） | PASS（2026-10-28） |
| @SUB-S16 | 同上（SUB-18） | PASS（2026-10-28） |
| @SUB-S17 | 同上（SUB-20） | PASS（2026-10-28） |
| @SUB-S18 | 同上（SUB-21/22） | PASS（2026-10-28） |
| @SUB-S19 | 同上（SUB-24） | PASS（2026-10-28） |
| @SUB-S20 | 同上（SUB-25/27/28） | PASS（2026-10-28） |
| @SUB-S21 | 同上（SUB-26） | PASS（2026-10-28） |
| @SUB-S22 | 同上（SUB-29；单测 Handle_MissingTeam_Throws400 佐证） | PASS（2026-10-28） |
| @SUB-S23 | 同上（SUB-30） | PASS（2026-10-28） |
| @SUB-S24 ~ @SUB-S26 | 同上（SUB-33/34/35） | PASS（2026-10-28） |
| @SUB-S27 ~ @SUB-S31 | [AdminSubsystems.test.tsx](../../ui/src/pages/admin/__tests__/AdminSubsystems.test.tsx) | PASS 6/6（2026-10-28） |
| @SUB-S32 ~ @SUB-S42 | [local-dev/subsystem-e2e.mjs](../../local-dev/subsystem-e2e.mjs)（SUB-36 ~ SUB-50，需后端运行中） | PASS 50/50（2026-10-28） |
| @SUB-S45 | 同上（SUB-51 ~ SUB-53：机器 token 换取含 refresh、伪造 refresh 401、refresh 轮换后新 access 可用） | PASS 53/53（2026-10-29） |
| @SUB-S43、@SUB-S44 | [SsoAuthorize.test.tsx](../../ui/src/pages/sso/__tests__/SsoAuthorize.test.tsx)（含 buildCallbackRedirect 拼装单测） | PASS 6/6（2026-10-28） |

## 后端单测补充

| 用例 | 验证物 | 结果（日期） |
|---|---|---|
| 创建/密钥哈希/重复 409/团队 400/校验器 | [MoAI.Subsystem.Tests](../../tests/MoAI.Subsystem.Tests/)（Create 5 例） | PASS 14/14（2026-10-28） |
| token 换取/停用 403/重置失效旧密钥 | 同上（Token 5 例） | PASS（2026-10-28） |
| @SAA-S1~S16 | local-dev/system-access-app-e2e.mjs（SAA-S1 ~ SAA-S16） | PASS（2026-10-29，随 61/61） |
| @SAA-S11~S31 | local-dev/system-access-app-e2e.mjs（SAA-S17 ~ SAA-S42：appid+密钥/token 双轨/refresh 轮换/停用双轨 403/重置密钥/范围分档/用户 upsert 幂等与校验/root 保护/禁用启用） | PASS 61/61（2026-10-29） |
| @SAA-S24~S26 | 同上（SAA-S43 ~ SAA-S49：组织角色创建/层级 path/同父重名 409/角色分配回显/删除保护） | PASS 61/61（2026-10-29） |
| @SAA-S27~S31 | 同上（SAA-S50 ~ SAA-S57：团队创建/重名/更新/成员 upsert/移除/列表/删除后全链失效） | PASS 61/61（2026-10-29） |
| @SAA-S9 / S10 | ui/src/pages/admin/__tests__/AdminIntegrations.test.tsx | PASS 7/7（2026-10-28） |
| @SAA-S58 | 同上（三组资源编辑器/重置密钥弹窗/appid+密钥双段展示） | PASS 7/7（2026-10-29） |
| @SAA-S59 | ui/src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx（创建展示密钥/重置密钥） | PASS 6/6（2026-10-29） |
| 单测（scope 解析含 team_read/write/CRUD/软删/密钥哈希） | tests/MoAI.Subsystem.Tests/SystemAccessAppTests.cs | PASS 8/8（2026-10-29，合计 31/31） |
| 统一表回归 | team-apikey-scope-e2e（TA-40~42 密钥兑换语义）+ wiki-external/wiki-mcp/kg 无关域 + external-app/app-acp/app-a2a 兑换带密钥 | 全绿（2026-10-29，TA 24/24、WX 52、WM 33、EA 75、ACP 21、A2A 20） |

## 回归命令

```bash
node local-dev/subsystem-e2e.mjs              # 需后端运行中（默认 :5000），零外部依赖
node local-dev/system-access-app-e2e.mjs      # 系统应用接入 + 系统资源接口（默认 :5210，传参覆盖为 :5000）
dotnet test tests/MoAI.Subsystem.Tests        # 后端单测
cd ui && npx vitest run src/pages/admin/__tests__/AdminSubsystems.test.tsx src/pages/admin/__tests__/AdminIntegrations.test.tsx src/pages/teams/apps/__tests__/TeamAccessApps.test.tsx
```

## 覆盖率说明

- 自动化覆盖全部核心写路径与机器面鉴权；@SUB-S2（未登录 401）由全局 JWT 拦截保证，走查确认。
- 在线 TTL 过期转离线（60s 无心跳）未做等待型自动化（等 60s 成本高），由在场键 TTL 语义与 SUB-11/13（初始离线/心跳在线）双向佐证。
