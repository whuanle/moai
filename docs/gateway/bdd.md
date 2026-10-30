# 团队网关模块行为（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/bdd.md](../team/bdd.md) ｜ 证据：[local-dev/team-apikey-scope-e2e.mjs](../../local-dev/team-apikey-scope-e2e.mjs)
>
> 团队接入 key（`moai-`，`team_api_key`）已于 2026-09-27 下线删除：密钥管理接口、认证路径与数据表均已移除，接入统一使用应用接入 key（`moai-ac-`）。@GW-S1/S2/S4/S5 与 TA-01~21/23/24/32e/f/35/37 等团队 key 场景编号永久退役。
> **团队模型网关（`/api/aigateway/*`、`model` 范围）已于 2026-10-29 整体下线删除**：模型仅供建模应用内部调用，后续由独立子系统承接；@GW-S3/S7 断言改为下线行为，`gateway-e2e.mjs` 脚本删除，团队可用模型查询迁至 `/api/team/{teamId}/ai/model/available`（见 aichannel 模块文档）。

## Feature: 外部接口范围体系（模型网关部分已下线）

```gherkin
Background:
  Given 团队管理员已在「应用接入」页创建接入并勾选功能范围

@GW-S3 @auto:e2e
Scenario: 模型网关端点已下线
  When 以任意凭证调用 /api/aigateway/{teamId}/v1/* 任意端点
  Then 端点已移除，返回 404（或路径落到其他路由模板时 405）
  And 创建或更新接入勾选 model 范围返回参数错误（代码不再合法）
```

```gherkin
@GW-S8 @auto:e2e
Scenario: 应用对话范围门禁
  When 以勾选 app_chat 的接入换取外部用户 token
  Then 签发放行
  But 未勾选 app_chat 时换取禁止，来源范围撤销后刷新亦禁止
```

```gherkin
@GW-S9 @auto:e2e
Scenario: 知识图谱范围门禁
  When 以勾选 kg_read 的 token 或直连 key 查询图谱模式、节点、边
  Then 放行
  But 写接口禁止；补勾 kg_write 刷新后放行
```

```gherkin
@GW-S10 @auto:e2e
Scenario: 范围与吊销的立即性
  When 管理端修改接入范围或删除接入
  Then key 直连访问在下一请求即按新状态判定（直连每请求直查数据库，无缓存等待）
```

## Feature: 功能范围与外部 token

```gherkin
Background:
  Given 团队管理员已创建应用接入

@GW-S6 @auto:e2e
Scenario: 应用接入 key 的功能范围
  When 创建应用接入并勾选功能范围（wiki_read/wiki_write/wiki_mcp/kg_read/kg_write/kg_mcp/app_chat/app_acp/app_a2a）
  Then 列表回显范围，签发 token 的知识库读写按勾选放行
  But 勾选非法或越维代码（含已下线的 model）时参数错误；不传范围默认读写全量+对话
  When 修改范围后刷新 token 或直连访问
  Then 以接入当前勾选为准
```
