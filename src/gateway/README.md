# MoAI.Gateway — 团队模型网关（模型 API 转发）

挂在团队下的模型 API 转发模块：第三方使用应用接入 key（`moai-ac-`，「应用接入」页维护）以 OpenAI Chat Completions、OpenAI Responses 或 Anthropic Messages 三种协议调用团队可用的模型；网关负责协议转换（上游渠道支持 OpenAI Chat/OpenAI Responses/Anthropic Messages/Gemini 四种协议族）、额度校验与用量记账。团队接入 key（`moai-`，`team_api_key`）已下线删除（2026-09-27）：密钥管理接口、认证分支与数据表均已移除。

## 开放端点（API Key 认证）

团队 id 直接入路由，每个团队拥有独立的接入地址 `{host}/api/aigateway/{teamId}/v1/...`。路由中的 teamId 必须与 key 绑定的团队完全一致，否则返回 403。

| 端点 | 入口协议 |
|---|---|
| `POST /api/aigateway/{teamId}/v1/chat/completions` | OpenAI Chat Completions |
| `POST /api/aigateway/{teamId}/v1/responses` | OpenAI Responses |
| `POST /api/aigateway/{teamId}/v1/messages` | Anthropic Messages |
| `GET /api/aigateway/{teamId}/v1/models` | 团队可用模型列表（OpenAI list 格式） |

- 密钥携带：`Authorization: Bearer moai-ac-xxx` 或 `x-api-key: moai-ac-xxx`。
- 流式：请求体 `stream=true` 时返回 SSE；入口/上游格式不同也会逐事件转换。
- 错误：按入口协议返回对应错误信封（OpenAI `error{}` / Anthropic `type:error`）。

## 接入 key 模型（access_app）

- key 明文落 `access_app.key`，认证时明文比对（存在即有效，软删除自动过滤）；「应用接入」页创建/删除/改范围。
- **功能范围 `scopes`**（int 位或）：`1=model`（网关端点必查）、`2=wiki_read`、`4=wiki_write`、`16=wiki_mcp`、`32=app_chat`、`64=kg_read`、`128=kg_write`、`256=kg_mcp`；不传默认 `AccessAppDefault`（外部资源全量+对话）。枚举与代码互转见 `MoAI.Database.Shared/Enums/TeamApiKeyScopes.cs`；范围模型详见 [docs/gateway/sdd.md](../../docs/gateway/sdd.md) 与 [docs/gateway/external-api-scope-system.md](../../docs/gateway/external-api-scope-system.md)。
- 直连调用网关后 `access_app.last_used_time` 自动刷新。

## 模型可见性与额度（复用 aichannel 管理端）

- 公开模型（`ai_model.is_public`）对所有团队可用；私有模型通过 `PUT /api/ai/model/{id}/authorization` 授权团队。
- 额度规则 `ai_model_limit`：公开模型全局规则 `team_id=0`（全平台共享），私有模型按团队；`limit_value=0` 视为不限额；周期重置惰性执行（命中时发现过期即重置）。
- 记账：调用后原子累加 `ai_model_quota.used_tokens`、写 `ai_model_usage_log`、按（模型,团队,用户,use_type=4,key_id）累加 `ai_model_token_audit`。

## 管理接口（JWT）

- `GET /api/team/{teamId}/gateway/models`（团队成员可访问）
- 密钥管理接口（`/api/team/{teamId}/gateway/keys`）已随团队接入 key 下线移除。

## 上线清单

1. ✅ e2e 冒烟：`node local-dev/gateway-e2e.mjs`（15/15：应用接入 key 两种鉴权头、路由团队校验、已下线 moai- 前缀 401、keys 端点 404、错误信封）；`node local-dev/team-apikey-scope-e2e.mjs`（21/21：功能范围回显/非法代码 400、网关 model 门禁、知识库读/写范围、直连、缓存立即性）。四件套见 [docs/gateway/](../../docs/gateway/)。
2. ✅ `npm run syncapi` 已重新生成客户端，`ui/src/api/gateway.ts` 仅保留 models 查询封装。

## 认证设计注意点

`/api/aigateway/{teamId}/v1/*` 端点使用 `AllowAnonymous` + 端点内显式 `AuthenticateAsync("GatewayApiKey")`，**不使用** `RequireAuthorization`。原因：`CustomAuthorizaMiddleware` 依赖 JWT 上下文解析用户（ApiKey principal 在其视角下为匿名 UserId=0，会误报"账号已被禁用"403）。团队 id 入路由后，端点内额外校验路由 teamId 与 key 绑定的团队一致，再按 `model` scope 位门禁。

## 已知边界

- 同名 `model_id` 授权自多个渠道时取确定性第一条；工具调用、文本、图片内容参与转换，音频等模态不转换。
- 上游未返回 usage 时按约 4 字符/token 估算，保证额度可计量。
