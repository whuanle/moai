-- 应用 ACP 范围（app_acp，位 512）：应用接入 key 通过 ACP 协议（agent-to-agent）访问团队已发布应用
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai_v2 < asserts/app_acp.sql
-- 存量接入一律回填 512，与「不传=存量全量」口径一致（同 kg_mcp 的 |=256 先例）；
-- 列 DEFAULT 同步对齐 handler 默认（TeamApiKeyScopeCodes.AccessAppDefault = 1014，含 wiki_mcp 16）。

alter table "access_app" alter column "scopes" set default 1014;
update "access_app" set "scopes" = "scopes" | 512;
comment on column "access_app".scopes is '功能范围位标记：1=model 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp（位或组合），限制可用模型渠道与签发 token 可访问的知识库/应用对话/知识图谱/应用 ACP 范围';
