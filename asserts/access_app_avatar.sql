-- 2026-11-04 接入头像字段（子系统/应用接入同表 access_app，随管理端接入管理头像上传引入，DB-first 手工执行）
-- access_app.avatar_path：接入头像 objectKey（EF 配置见 AccessAppConfiguration.cs）
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS avatar_path varchar(255) NOT NULL DEFAULT ''::character varying;
COMMENT ON COLUMN access_app.avatar_path IS '接入头像 objectKey，空串=未设置';
