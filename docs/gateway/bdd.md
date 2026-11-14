# 团队网关模块行为（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../app/bdd.md](../app/bdd.md)（@TR-* 资源 key 主场景）｜ 证据：[local-dev/teamres-key-e2e.mjs](../../local-dev/teamres-key-e2e.mjs)
>
> 团队接入 key（`moai-`）已于 2026-09-27 下线删除：@GW-S1/S2/S4/S5 与 TA-01~21/23/24/32e/f/35/37 等团队 key 场景编号永久退役。
> **团队模型网关（`/api/aigateway/*`、`model` 范围）已于 2026-10-29 整体下线删除**：@GW-S3/S7 断言改为下线行为，`gateway-e2e.mjs` 删除。
> **scope 范围体系已于 2026-11-04 整体退役**：@GW-S6/S8/S9 的「功能范围/勾选/分档」断言语义失效，由去范围化 + 资源 key 包围圈新场景（@GW-S11 起）承接；证据脚本 `team-apikey-scope-e2e.mjs`（TA 系列）删除，编号永久不复用。

## Feature: 外部接口凭证体系（scope 体系已退役）

```gherkin
Background:
  Given 团队管理员已创建应用接入 key（moai-ac-，无范围勾选概念）

@GW-S3 @auto:e2e
Scenario: 模型网关端点已下线
  When 以任意凭证调用 /api/aigateway/{teamId}/v1/* 任意端点
  Then 端点已移除，返回 404（或路径落到其他路由模板时 405）
  And 创建或更新接入请求携带 model 等任意 scopes 字段被忽略，创建返回成功

@GW-S6 @auto:e2e
Scenario: 应用接入 key 全量访问（去范围化改写）
  When 以 moai-ac- key 直连知识库读接口与写接口
  Then 读写均放行（无范围分档）
  When 以 moai-ac- key 查询知识图谱列表或换取应用 token
  Then 均放行
  When 以 moai-ac- key 携带 externalUserId 换用户 token
  Then 返回 400（外部应用授权已退役，身份穿透改走内部应用访问点）
  And 接入列表项不回显 scopes 字段（列已退役）

@GW-S10 @auto:e2e
Scenario: 凭据状态变化的即时性
  When 管理端停用、删除或重置接入
  Then key 直连访问在下一请求即按新状态判定（直连每请求直查数据库，无缓存等待）
```

## Feature: 团队资源 key 包围圈（moai-tr-，主场景见 [@TR-S10](../app/bdd.md#tr-s10)~[@TR-S31](../app/bdd.md#tr-s31)）

```gherkin
Background:
  Given 团队管理员已为知识库/知识图谱/团队应用各生成一把资源 key（moai-tr-）

@GW-S11 @auto:e2e
Scenario: 资源 key 仅能访问绑定资源端点
  When 以 wiki 资源 key 直连本库 REST 与本库 MCP 端点
  Then 放行（无读/写分档，读写均可）
  But 访问同团队其它知识库返回 403 insufficient_scope
  And 越圈访问知识图谱或 agent 端点返回 403 insufficient_scope
  When 以 app 资源 key 调用其绑定应用的 ACP/A2A 端点
  Then 放行（一应用一把，ACP/A2A 共用）
  But 打其它应用的同端点返回 403 insufficient_scope（key 绑定应用实例）
  And 越圈访问 wiki 端点返回 403 insufficient_scope

@GW-S12 @auto:e2e
Scenario: 资源 key 生命周期即时生效
  When 管理端重置资源 key
  Then 行 id 不变、旧 key 下一请求 401、新 key 放行（直连身份延续）
  When 停用资源 key
  Then 直连下一请求 401；重新启用后恢复放行
  When 删除资源 key
  Then 直连 401；重新 get-or-create 生成新 key
  When 以资源 key 请求换取外部 token
  Then 拒绝（资源 key 不可换 token，仅认 moai-ac-）
```
