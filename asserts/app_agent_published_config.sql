-- Agent 应用配置发布快照 schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_published_config.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 发布配置快照 JSON（camelCase），发布应用时写入，正式会话按此快照执行；null=从未发布（存量已发布应用回退实时配置，重新发布后生效）
alter table app_agent_config add column if not exists published_config text;
comment on column app_agent_config.published_config is '发布配置快照 JSON（camelCase：prompt/modelId/wikiIds/plugins/skills/executionSettings/openingStatement/openingStatementEnabled/quickInputs/workflowApps），发布应用时写入，正式会话按此快照执行；null=从未发布';

-- 配置状态，0=草稿有未发布变更 1=当前草稿与已发布一致
alter table app_agent_config add column if not exists status smallint not null default 0;
comment on column app_agent_config.status is '配置状态，0=草稿有未发布变更 1=当前草稿与已发布一致';
