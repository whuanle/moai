-- 会话/审计表缓存命中 token 与上下文占用增列（模型响应 usage 上报）
-- 背景：MEAI 10.9 UsageDetails.CachedInputTokenCount（OpenAI 兼容响应 prompt_tokens_details.cached_tokens）；
--       context_tokens 为最近一次模型调用的输入 token（当前上下文占用，压缩后随下一轮回落）
-- 用法：docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_cache_tokens.sql

alter table app_agent_session add column if not exists cached_tokens integer not null default 0;
comment on column app_agent_session.cached_tokens is '缓存命中的输入token累计（模型响应 usage 上报，未上报为 0）';

alter table app_agent_session add column if not exists context_tokens integer not null default 0;
comment on column app_agent_session.context_tokens is '当前上下文占用（最近一次模型调用的输入 token；上下文压缩后随下一轮回落）';

alter table ai_model_token_audit add column if not exists cached_tokens integer not null default 0;
comment on column ai_model_token_audit.cached_tokens is '缓存命中的输入数量（模型响应 usage 上报，未上报为 0）';
