-- 应用接入新增知识图谱范围（access_app.scopes 增加 64=kg_read、128=kg_write 位），新库由 EnsureCreated 依据实体/配置直接建成
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai < asserts/access_app_kg_scopes.sql
-- 存量接入一律授予知识图谱读写（scopes |= 192），与「此前接入 token 可无差别访问知识图谱外部接口」行为保持一致；
-- 列 DEFAULT 同步 38 → 230（model 之外的旧默认=读写+对话，此处再加 KG 读写=230）。团队接入 key（team_api_key）为纯增量能力，不回填。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

alter table "access_app" alter column "scopes" set default 230;
update "access_app" set "scopes" = "scopes" | 192 where ("scopes" & 192) != 192;
comment on column "access_app".scopes is '功能范围位标记：1=model 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write（位或组合），限制可用模型渠道与签发 token 可访问的知识库/应用对话/知识图谱范围';
