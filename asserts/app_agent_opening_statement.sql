-- Agent 应用对话开场白 schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_opening_statement.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 对话开场白内容，最长4000字符，空为''
alter table app_agent_config add column if not exists opening_statement varchar(4000) not null default '';
comment on column app_agent_config.opening_statement is '对话开场白，最长4000字符，空为''''';

-- 是否启用对话开场白
alter table app_agent_config add column if not exists opening_statement_enabled boolean not null default false;
comment on column app_agent_config.opening_statement_enabled is '是否启用对话开场白';
