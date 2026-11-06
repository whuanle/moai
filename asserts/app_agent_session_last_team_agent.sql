-- Agent 应用会话记录最近一轮生效团队智能体 schema 变更（对话端智能体切换回显）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_session_last_team_agent.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 最近一轮对话生效的团队智能体ID（team_agent.id），用户在对话端切换后随轮更新；空=应用未绑定团队智能体
alter table app_agent_session add column if not exists last_team_agent_id uuid;
comment on column app_agent_session.last_team_agent_id is '最近一轮对话生效的团队智能体ID（team_agent.id），用户在对话端切换后随轮更新；空=应用未绑定团队智能体';
