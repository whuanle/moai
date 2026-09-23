-- 2026-09-20 插件头像与分类表情字段（随 feat(app) 多模块迭代引入，DB-first 手工执行）
-- plugin.avatar_path：插件头像路径（EF 配置见 PluginConfiguration.cs）
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS avatar_path varchar(255) NOT NULL DEFAULT ''::character varying;
COMMENT ON COLUMN plugin.avatar_path IS '头像';

-- classify.emoji：分类表情（EF 配置见 ClassifyConfiguration.cs）
ALTER TABLE classify ADD COLUMN IF NOT EXISTS emoji varchar(10) NOT NULL DEFAULT ''::character varying;
COMMENT ON COLUMN classify.emoji IS '表情';
