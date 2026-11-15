-- 用户鉴权版本：禁用/启用、强制下线、改密等安全事件时递增，token 携带签发时的版本，版本不一致立即失效。
-- 存量库可重复执行；启动期 SchemaReconciler 也会按 EF 模型自动补列。
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS auth_version bigint NOT NULL DEFAULT 0;
COMMENT ON COLUMN "user".auth_version IS '鉴权版本：禁用/启用、强制下线、改密等安全事件时递增';
