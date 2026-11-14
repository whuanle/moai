-- Agent 应用会话记录最近一轮实际对话模型 schema 变更（对话端多模型切换回显）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_session_last_model.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 最近一轮对话实际使用的模型ID（ai_model.id），用户在对话端切换模型后随轮更新；空=从未对话
alter table app_agent_session add column if not exists last_model_id uuid not null default '00000000-0000-0000-0000-000000000000';
comment on column app_agent_session.last_model_id is '最近一轮对话实际使用的模型ID（ai_model.id），用户在对话端切换模型后随轮更新；空=从未对话';
