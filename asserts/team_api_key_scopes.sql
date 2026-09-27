-- 团队 API Key 功能范围（team_api_key.scopes）schema，新库由 EnsureCreated 依据实体/配置直接建成
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai < asserts/team_api_key_scopes.sql
-- 存量 key 一律回填 1（model），与列 DEFAULT 保持一致：此前 team_api_key 仅授权模型网关。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

alter table "team_api_key" add column if not exists "scopes" int not null default 1;
comment on column "team_api_key".scopes is '功能范围位标记：1=model 2=wiki_read 4=wiki_write 8=external_token 16=wiki_mcp（位或组合）';
