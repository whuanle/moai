-- 模型渠道：团队自有渠道（ai_channel.team_id）+ 额度体系下线
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/ai_channel_team.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成（额度表不再创建），无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 团队自有模型渠道：0=系统渠道（平台统一维护），大于 0=团队自有渠道（仅该团队可用）
alter table "ai_channel" add column if not exists "team_id" integer not null default 0;
create index if not exists "idx_ai_channel_team_id" on "ai_channel" ("team_id") where "is_deleted" = 0;

-- 额度体系整体下线（周期规则 + 余额），存量表与数据直接清除
drop table if exists "ai_model_quota";
drop table if exists "ai_model_limit";
