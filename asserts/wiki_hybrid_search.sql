-- 知识库混合检索与向量生命周期优化（等价于启动时幂等 DDL，免手工执行）
-- D39/D41：关键词先验路径 pg_trgm 索引 + 切片序号复合索引 + 遗留死表清理

-- 1. pg_trgm 扩展（中文场景 ILIKE 加速；不用 tsvector 因无 zhparser 分词扩展）
CREATE EXTENSION IF NOT EXISTS pg_trgm;

-- 2. 切片原文 GIN trgm 索引（关键词召回：切片原文 ILIKE 探针匹配）
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_content_slice_content_trgm
    ON wiki_document_chunk_content USING gin (slice_content gin_trgm_ops);

-- 3. 关键词/问题元数据 GIN trgm 索引（关键词召回：LLM 预生成关键词/问题的 ILIKE 匹配）
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_metadata_content_trgm
    ON wiki_document_chunk_metadata USING gin (metadata_content gin_trgm_ops);

-- 4. 切片序号复合索引（召回邻居扩展 / 按序号取片段 / 向量化按序读切片）
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_content_doc_slice
    ON wiki_document_chunk_content (document_id, slice_order);

-- 4b. 提取正文 GIN trgm 索引（内容级兜底检索：已提取未切割文档的 ILIKE 探针匹配，D45）
CREATE INDEX IF NOT EXISTS idx_wiki_document_content_content_trgm
    ON wiki_document_content USING gin (content gin_trgm_ops);

-- 5. 遗留死表清理（wiki_document_chunk_embedding 全仓库无读写，向量实际存于 pgvector 动态表 __wiki_{id}）
DROP TABLE IF EXISTS wiki_document_chunk_embedding;
