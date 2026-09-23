-- 2026-09-23 技能头像字段（技能头像 + 压缩包上传增量引入，DB-first 手工执行）
-- skill.avatar_path：技能头像路径（EF 配置见 SkillConfiguration.cs）
ALTER TABLE skill ADD COLUMN IF NOT EXISTS avatar_path varchar(255) NOT NULL DEFAULT ''::character varying;
COMMENT ON COLUMN skill.avatar_path IS '技能头像 objectKey，空串=未设置';
