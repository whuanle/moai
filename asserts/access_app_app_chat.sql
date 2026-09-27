-- 应用接入新增「应用对话」范围（access_app.scopes 增加 32=app_chat 位），新库由 EnsureCreated 依据实体/配置直接建成
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai < asserts/access_app_app_chat.sql
-- 存量接入一律授予 app_chat（scopes |= 32），与「此前任何接入 key 都可换取用户 token 对话」行为保持一致；
-- 列 DEFAULT 同步 6 → 38（wiki_read|wiki_write|app_chat）。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

alter table "access_app" alter column "scopes" set default 38;
update "access_app" set "scopes" = "scopes" | 32 where ("scopes" & 32) = 0;
comment on column "access_app".scopes is '功能范围位标记：1=model 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat（位或组合），限制可用模型渠道与签发 token 可访问的知识库/应用对话范围';
