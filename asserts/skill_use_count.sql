-- 技能使用计数（应用设置面板技能卡片热门排序）
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 SkillEntity 及其 Configuration 一致
ALTER TABLE skill
    ADD COLUMN IF NOT EXISTS use_count integer NOT NULL DEFAULT 0;

COMMENT ON COLUMN skill.use_count IS '使用次数：应用对话中模型调用 skill_{key} 装载工具时自增，技能卡片热门排序依据';
