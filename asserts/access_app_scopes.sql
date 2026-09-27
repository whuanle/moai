-- 应用接入功能范围（access_app.scopes）与最近使用时间（access_app.last_used_time）schema，
-- 新库由 EnsureCreated 依据实体/配置直接建成
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai < asserts/access_app_scopes.sql
-- 存量接入一律回填 6（wiki_read|wiki_write），与列 DEFAULT 保持一致：此前接入 key 签发的 token 按全量知识库范围放行。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

alter table "access_app" add column if not exists "scopes" int not null default 6;
comment on column "access_app".scopes is '功能范围位标记：1=model 2=wiki_read 4=wiki_write 16=wiki_mcp（位或组合），限制可用模型渠道与签发 token 的知识库范围';

alter table "access_app" add column if not exists "last_used_time" timestamptz null;
comment on column "access_app".last_used_time is '最近使用时间（通过模型网关调用时刷新）';

