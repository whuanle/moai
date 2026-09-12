-- Agent 应用对话（发布 + 会话运行）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_chat.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 应用发布状态：0=草稿（未发布）1=已发布
alter table app add column if not exists publish_status smallint not null default 0;
comment on column app.publish_status is '发布状态，0=草稿 1=已发布';

-- 发布时间，未发布为 null
alter table app add column if not exists publish_time timestamptz null;
comment on column app.publish_time is '发布时间，未发布为 null';

-- 会话冷快照：AgentSession 序列化状态（含压缩索引），用于 Redis 热态失效后恢复
alter table app_agent_session add column if not exists state jsonb null;
comment on column app_agent_session.state is 'Agent 会话冷快照（AgentSession 序列化，含上下文压缩索引），Redis 热态失效后恢复';
