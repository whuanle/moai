-- 应用接入统一表：团队级（moai-ac-）与系统级（moai-sa-）合用 access_app，
-- 原 system_access_app 表迁移进 access_app（level=1）后删除。
-- scope 位空间调整：系统资源范围从低位 1/2/4/8 迁至高位 1<<16..1<<19，并新增 team_read/team_write（1<<20/1<<21）。
-- 新库由 EnsureCreated 依据实体/配置直接建成，本脚本供存量库幂等迁移。
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai < asserts/access_app_unify.sql
-- 或本机直连：psql "Database=moai_v2;Host=127.0.0.1;Username=postgres;Password=..." -f asserts/access_app_unify.sql

-- 1. access_app 补列（幂等）
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "level" smallint NOT NULL DEFAULT 0;
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "key_prefix" varchar(16) NOT NULL DEFAULT ''::text;
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "secret" varchar(255);
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "secret_salt" varchar(255);
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "status" smallint NOT NULL DEFAULT 0;

-- 存量团队接入补展示前缀（moai-ac- + 4 位随机字符）
UPDATE "access_app" SET "key_prefix" = left("key", 12) WHERE "key_prefix" = '';

CREATE INDEX IF NOT EXISTS "idx_access_app_key_prefix" ON "access_app" ("key_prefix");
CREATE UNIQUE INDEX IF NOT EXISTS "idx_access_app_key_uindex" ON "access_app" ("key", "is_deleted");

COMMENT ON COLUMN "access_app"."level" IS '接入层级：0=team 团队级 1=system 系统级';
COMMENT ON COLUMN "access_app"."key_prefix" IS 'key 展示前缀';
COMMENT ON COLUMN "access_app"."secret" IS '接入密钥 PBKDF2 哈希（null=未设密钥）';
COMMENT ON COLUMN "access_app"."secret_salt" IS '接入密钥盐（Base64）';
COMMENT ON COLUMN "access_app"."status" IS '状态：0=enabled 1=disabled';
COMMENT ON COLUMN "access_app"."scopes" IS '功能范围位标记（位或组合）：团队级低位 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp 1024=app_a2a；系统级高位 65536=org_read 131072=org_write 262144=user_read 524288=user_write 1048576=team_read 2097152=team_write';

-- 2. system_access_app 迁入 access_app（幂等：按 key 去重；scope 位 1/2/4/8 左移 16 位到高位）
INSERT INTO "access_app" ("id", "name", "description", "team_id", "level", "key", "key_prefix", "secret", "secret_salt", "scopes", "status", "create_user_id", "create_time", "update_user_id", "update_time", "is_deleted")
SELECT s."id",
       s."name",
       s."description",
       0,
       1,
       s."key",
       s."key_prefix",
       NULL,
       NULL,
       ((s."scopes" & 1) << 16) | ((s."scopes" & 2) << 16) | ((s."scopes" & 4) << 16) | ((s."scopes" & 8) << 16),
       s."status",
       s."create_user_id",
       s."create_time",
       s."update_user_id",
       s."update_time",
       s."is_deleted"
FROM "system_access_app" s
WHERE NOT EXISTS (SELECT 1 FROM "access_app" a WHERE a."key" = s."key");

-- 3. 删除旧表
DROP TABLE IF EXISTS "system_access_app";
