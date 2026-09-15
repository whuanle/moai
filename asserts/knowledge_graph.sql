-- 知识图谱头像（v2.2）：knowledge_graph 表新增 avatar_path（存量库补列，幂等）
ALTER TABLE knowledge_graph
    ADD COLUMN IF NOT EXISTS avatar_path varchar(255) DEFAULT ''::character varying;

COMMENT ON COLUMN knowledge_graph.avatar_path IS '头像地址';

-- 模型属性定义（v2.3）：knowledge_graph_entity_type 表新增 properties（存量库补列，幂等）
ALTER TABLE knowledge_graph_entity_type
    ADD COLUMN IF NOT EXISTS properties jsonb NOT NULL DEFAULT '[]'::jsonb;

COMMENT ON COLUMN knowledge_graph_entity_type.properties IS '属性定义 JSON';
