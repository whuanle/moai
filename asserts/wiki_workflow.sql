-- 知识库默认工作流（存量库补列，幂等）
-- wiki.default_workflow_config：切割/元数据生成/向量化三步预设 JSON，空串表示未配置
ALTER TABLE public.wiki ADD COLUMN IF NOT EXISTS default_workflow_config text DEFAULT ''::text;
COMMENT ON COLUMN public.wiki.default_workflow_config IS '默认工作流配置（JSON：切割/元数据生成/向量化三步预设），空串表示未配置';
