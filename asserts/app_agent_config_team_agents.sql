-- Agent 应用绑定团队智能体 schema 变更（team-agent Phase 2：应用绑定）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_config_team_agents.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 绑定的团队智能体ID列表，JSON 数组文本，元素为 team_agent.id（uuid 字符串），
-- 须为本团队已启用智能体；空为'[]'，运行时视为未绑定（行为与存量应用一致）
alter table app_agent_config add column if not exists team_agent_ids text not null default '[]';
comment on column app_agent_config.team_agent_ids is '绑定的团队智能体ID列表，JSON 数组文本，元素为 team_agent.id（uuid 字符串），须为本团队已启用智能体，空为''[]''';

-- 默认团队智能体ID（team_agent.id）；空=取列表第一个；列表为空时忽略
alter table app_agent_config add column if not exists default_team_agent_id uuid;
comment on column app_agent_config.default_team_agent_id is '默认团队智能体ID，逻辑关联 team_agent.id（uuid）；空=取列表第一个；列表为空时忽略';

-- 团队智能体内嵌定义 JSON（SnapshotTeamAgent 数组），发布应用时随快照写入 published_config 并同步本列；
-- 草稿行恒为''，运行时已发布会话按此解析智能体定义（发布后改团队智能体不影响线上）
alter table app_agent_config add column if not exists team_agent_defs text not null default '';
comment on column app_agent_config.team_agent_defs is '团队智能体内嵌定义 JSON（SnapshotTeamAgent 数组），发布应用时随快照写入，草稿行恒为''''；已发布会话运行时按此解析智能体定义';
