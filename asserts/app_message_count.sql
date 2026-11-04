-- 应用消息条数累计列（使用分析消息总数走统计模块增量维护）
-- 背景：消息总数原由 /usage 查询实时 COUNT app_agent_message（Contains(sessionIds)），
--       会话量大后成本高；改为会话落库时推送增量到统计模块，每分钟批量累加到本列
-- 用法：docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_message_count.sql

alter table app add column if not exists message_count bigint not null default 0;
comment on column app.message_count is '累计消息条数（会话落库物理消息行数增量累计，删除会话不减少；统计模块 app-message-usage 批量累加）';
