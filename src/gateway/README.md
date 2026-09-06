# MoAI.Gateway — 团队模型网关（模型 API 转发）

挂在团队下的模型 API 转发模块：团队管理员创建/维护 API Key，团队成员使用密钥以 OpenAI Chat Completions、OpenAI Responses 或 Anthropic Messages 三种协议调用团队可用的模型；网关负责协议转换（上游渠道支持 OpenAI Chat/OpenAI Responses/Anthropic Messages/Gemini 四种协议族）、额度校验与用量记账。

## 开放端点（API Key 认证，不走 `/api` 前缀）

| 端点 | 入口协议 |
|---|---|
| `POST /v1/chat/completions` | OpenAI Chat Completions |
| `POST /v1/responses` | OpenAI Responses |
| `POST /v1/messages` | Anthropic Messages |
| `GET /v1/models` | 团队可用模型列表（OpenAI list 格式） |

- 密钥携带：`Authorization: Bearer moai-xxx` 或 `x-api-key: moai-xxx`。
- 流式：请求体 `stream=true` 时返回 SSE；入口/上游格式不同也会逐事件转换。
- 错误：按入口协议返回对应错误信封（OpenAI `error{}` / Anthropic `type:error`）。

## 密钥模型（team_api_key）

- 原文只在创建响应中出现一次，服务端仅存 sha256（`key_sha256` 为 bytea，`KeySha256` 属性名触发 hex→bytea 转换器；脚手架 T4 模板会把它生成回 string 属性）。
- `expire_time` / `last_used_time` 为 NOT NULL timestamptz，用哨兵值表达"无"：`9999-12-31 23:59:59+00`=永不过期（`ApiKeySentinels.NeverExpire`），`0001-01-01 00:00:00+00`=从未使用（`ApiKeySentinels.NeverUsed`），查询侧转换为 null 返回前端。
- 密钥绑定创建者：创建者被禁用或移出团队即失效；支持禁用/过期时间。

## 模型可见性与额度（复用 aichannel 管理端）

- 公开模型（`ai_model.is_public`）对所有团队可用；私有模型通过 `PUT /api/ai/model/{id}/authorization` 授权团队。
- 额度规则 `ai_model_limit`：公开模型全局规则 `team_id=0`（全平台共享），私有模型按团队；`limit_value=0` 视为不限额；周期重置惰性执行（命中时发现过期即重置）。
- 记账：调用后原子累加 `ai_model_quota.used_tokens`、写 `ai_model_usage_log`、按（模型,团队,用户,use_type=4,key_id）累加 `ai_model_token_audit`。

## 管理接口（JWT，团队管理员）

- `GET/POST /api/team/{teamId}/gateway/keys`、`PUT/DELETE /api/team/{teamId}/gateway/keys/{keyId}`
- `GET /api/team/{teamId}/gateway/models`（团队成员可访问）

## 上线清单（均已完成）

1. ✅ PostgreSQL 应用 [`asserts/team_api_key.sql`](../../asserts/team_api_key.sql)。
2. ✅ e2e 冒烟：`node local-dev/gateway-e2e.mjs`（22/22：密钥管理、两种鉴权头、禁用/删除即时失效、错误信封、成员权限矩阵）。
3. ✅ `npm run syncapi` 已重新生成客户端，`ui/src/api/gateway.ts` 已迁移到 Kiota fluent 客户端。

## 认证设计注意点

`/v1/*` 端点使用 `AllowAnonymous` + 端点内显式 `AuthenticateAsync("GatewayApiKey")`，**不使用** `RequireAuthorization`。原因：`CustomAuthorizaMiddleware` 依赖 JWT 上下文解析用户（ApiKey principal 在其视角下为匿名 UserId=0，会误报"账号已被禁用"403）。密钥创建者的用户状态检查已移入 `GatewayApiKeyAuthenticationHandler`，治理语义不变。

## 已知边界

- 同名 `model_id` 授权自多个渠道时取确定性第一条；工具调用、文本、图片内容参与转换，音频等模态不转换。
- 上游未返回 usage 时按约 4 字符/token 估算，保证额度可计量。
