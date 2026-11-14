-- 知识图谱画布样式规则（B8）：knowledge_graph 表新增列，存量库补列（幂等）
-- 与 src/database 下 KnowledgeGraphEntity.CanvasStyleJson 及其 Configuration 保持一致
ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS canvas_style_json text NOT NULL DEFAULT '';

COMMENT ON COLUMN knowledge_graph.canvas_style_json IS '画布样式规则 JSON（按实体类型配置节点第二行 caption；空串=未配置）';
