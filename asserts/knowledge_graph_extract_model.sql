-- 知识图谱默认抽取模型（B5/D30）：knowledge_graph 表新增列，存量库补列（幂等）
-- 与 src/database 下 KnowledgeGraphEntity.ExtractModelId 及其 Configuration 保持一致
ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS extract_model_id uuid NULL;

COMMENT ON COLUMN knowledge_graph.extract_model_id IS '默认抽取模型的id（AI 导入缺省对话模型）；为空表示未配置、导入时必须显式指定';
