# 团队网关模块行为（BDD）

> 关联：[SDD](./sdd.md) ｜ [BDD](./bdd.md) ｜ [TDD](./tdd.md) ｜ [SOP](./sop.md) ｜ 上游：[../team/bdd.md](../team/bdd.md) ｜ 证据：[local-dev/team-apikey-scope-e2e.mjs](../../local-dev/team-apikey-scope-e2e.mjs)、[local-dev/gateway-e2e.mjs](../../local-dev/gateway-e2e.mjs)
>
> 团队接入 key（`moai-`，`team_api_key`）已于 2026-09-27 下线删除：密钥管理接口、认证路径与数据表均已移除，接入统一使用应用接入 key（`moai-ac-`）。@GW-S1/S2/S4/S5 与 TA-01~21/23/24/32e/f/35/37 等团队 key 场景编号永久退役。

## Feature: 模型网关端点与应用接入 key

```gherkin
Background:
  Given 团队管理员已在「应用接入」页创建接入并勾选功能范围

@GW-S3 @auto:e2e
Scenario: 模型网关端点鉴权与 model 范围
  When 以 Bearer 或 x-api-key 携带应用接入 key 调用网关端点
  Then 路由团队与 key 归属团队一致且勾选 model 时放行
  But 未勾选 model 时返回禁止；伪造或已删除的 key 返回未认证
  And 密钥管理端点（/team/{teamId}/gateway/keys）已移除，访问返回 404
```

```gherkin
@GW-S7 @auto:e2e
Scenario: 应用接入 key 勾选 model 后直连模型网关
  When 应用接入勾选 model 范围并以其 key 调用网关端点
  Then 直接放行，用量与最近使用时间照常记账（access_app.last_used_time）
  But 未勾选 model 时返回禁止；key 归属团队与路由团队不一致时返回禁止
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
Scenario: 范围与吊销的缓存立即性
  When 管理端修改接入范围或删除接入
  Then key 直连访问在下一请求即按新状态判定，无需等待缓存过期
```

## Feature: 功能范围与外部 token

```gherkin
Background:
  Given 团队管理员已创建应用接入

@GW-S6 @auto:e2e
Scenario: 应用接入 key 的功能范围
  When 创建应用接入并勾选功能范围（model/wiki_read/wiki_write/wiki_mcp/kg_*/app_chat/app_acp）
  Then 列表回显范围，签发 token 的知识库读写按勾选放行，model 授权 key 直连网关
  But 勾选非法或越维代码时参数错误；不传范围默认读写全量+对话
  When 修改范围后刷新 token 或直连网关
  Then 以接入当前勾选为准
```
