-- 知识图谱图检索（SP-A）：knowledge_graph 表新增向量化模型与维度（存量库补列，幂等）
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 KnowledgeGraphEntity 及其 Configuration 一致
ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS embedding_model_id uuid NULL;

COMMENT ON COLUMN knowledge_graph.embedding_model_id IS '向量化模型的id；为空表示未配置、图谱不参与向量检索';

ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS embedding_dimensions integer NOT NULL DEFAULT 1024;

COMMENT ON COLUMN knowledge_graph.embedding_dimensions IS '知识图谱向量维度（1-2000，建 hnsw 索引的硬上限）';
