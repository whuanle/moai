-- 应用接入统一表合并子系统：subsystem 行整体并入 access_app（level=1、app_type=1，id 原样迁移），
-- 系统级接入的资源范围分档下线（scopes 归 0=全量访问 /api/subsys），随后删除 subsystem 表。
-- 新增列：
--   app_type     系统级接入类型：0=不适用（团队级行）1=subsystem 子系统 2=app 应用接入
--   callback_url 委托 SSO 回调地址（仅子系统行使用，空串=未开放登录接入）
-- 新库由 EnsureCreated 依据实体/配置直接建成，本脚本供存量库幂等迁移。
-- 用法（存量库）：psql "Database=moai_v2;Host=...;Username=postgres;Password=..." -f asserts/access_app_merge_subsystem.sql

-- 1. access_app 补列（幂等）；name 由 varchar(20) 加宽到 varchar(50)（原 subsystem.name 口径，加宽安全可重复执行）
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "app_type" smallint NOT NULL DEFAULT 0;
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "team_scope" smallint NOT NULL DEFAULT 0;
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "callback_url" varchar(255) NOT NULL DEFAULT ''::text;
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "callback_urls" text NOT NULL DEFAULT '[]'::text;
ALTER TABLE "access_app" ADD COLUMN IF NOT EXISTS "secret_mask" varchar(32) NOT NULL DEFAULT ''::text;
ALTER TABLE "access_app" ALTER COLUMN "name" TYPE varchar(50);

-- 存量单回调地址迁入 JSON 数组（幂等：仅空数组行回填）
UPDATE "access_app" SET "callback_urls" = to_jsonb(jsonb_build_array("callback_url"))::text
WHERE "level" = 1 AND "callback_url" <> '' AND "callback_urls" = '[]';

-- 2. 存量系统级应用接入（moai-sa-）标记 app_type=2；子系统行由第 3 步 INSERT 写 1；团队级行保持 0
UPDATE "access_app" SET "app_type" = 2 WHERE "level" = 1 AND "app_type" = 0;

-- 3. subsystem 并入 access_app（幂等：按 key 去重；id 原样迁移，团队授权/组织授权/委托引用不断；scopes 归 0=全量）
--    以 to_regclass 守护，subsystem 表不存在（已迁移过）时跳过，脚本可重复执行
DO $$
BEGIN
  IF to_regclass('public.subsystem') IS NOT NULL THEN
    INSERT INTO "access_app" ("id", "name", "description", "team_id", "level", "app_type", "team_scope", "key", "key_prefix", "secret", "secret_salt", "scopes", "status", "callback_url", "create_user_id", "create_time", "update_user_id", "update_time", "is_deleted")
    SELECT s."id",
           s."name",
           s."description",
           0,
           1,
           1,
           s."team_scope",
           s."key",
           left(s."key", 12),
           s."secret",
           s."secret_salt",
           0,
           s."status",
           s."callback_url",
           s."create_user_id",
           s."create_time",
           s."update_user_id",
           s."update_time",
           s."is_deleted"
    FROM "subsystem" s
    WHERE NOT EXISTS (SELECT 1 FROM "access_app" a WHERE a."key" = s."key");
  END IF;
END
$$;

-- 4. 系统级资源范围分档下线：scopes 归 0（团队级低位不受影响）
UPDATE "access_app" SET "scopes" = 0 WHERE "level" = 1;

-- 4b. 迁移修复：有团队授权行的子系统行标记为指定团队范围（若早期迁移先于本列补建执行，此处幂等兜底）
UPDATE "access_app" a SET "team_scope" = 1
WHERE a."level" = 1 AND a."app_type" = 1
  AND EXISTS (SELECT 1 FROM "subsystem_team_auth" t WHERE t."subsystem_id" = a."id");

-- 5. 删除旧表
DROP TABLE IF EXISTS "subsystem";

-- 6. 注释同步
COMMENT ON COLUMN "access_app"."app_type" IS '系统级接入类型：0=不适用（团队级行）1=subsystem 子系统 2=app 应用接入';
COMMENT ON COLUMN "access_app"."team_scope" IS '团队授权范围（仅子系统行，委托 SSO 门禁用）：0=全员可用 1=指定团队';
COMMENT ON COLUMN "access_app"."callback_url" IS '（已废弃，仅存量保留）委托 SSO 单回调地址，迁移后不再读取';
COMMENT ON COLUMN "access_app"."callback_urls" IS '委托 SSO 登录回调地址列表（JSON 字符串数组，仅子系统行使用，空数组=未开放登录接入）';
COMMENT ON COLUMN "access_app"."secret_mask" IS '密钥展示掩码（前 4 + **** + 后 4，仅系统级可逆格式行填充）';
COMMENT ON COLUMN "access_app"."secret" IS '接入密钥存储值（系统级：v2: 前缀+AES 密文可回显，无前缀=存量 PBKDF2 哈希；团队级：PBKDF2 哈希；null=未设密钥）';
COMMENT ON COLUMN "access_app"."scopes" IS '功能范围位标记（位或组合）：团队级低位 2=wiki_read 4=wiki_write 16=wiki_mcp 32=app_chat 64=kg_read 128=kg_write 256=kg_mcp 512=app_acp 1024=app_a2a；系统级不再使用（全量访问 /api/subsys）';
