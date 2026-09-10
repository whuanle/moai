-- 知识库向量化精简 DDL：去掉 wiki 表上"按文档动态配置"的元数据/切片字段；维度上限收紧到 2000。
-- 背景：本仓库无 EF Migration，启动 EnsureCreated 只对空库生效；已有数据库需手动执行本文件，
--       同时保持与 src/database 下 WikiEntity 及其 Configuration 一致。

-- 1. 删除 wiki 上不再由知识库管理的字段
ALTER TABLE public.wiki
    DROP COLUMN IF EXISTS metadata_model_id,
    DROP COLUMN IF EXISTS chunk_size,
    DROP COLUMN IF EXISTS chunk_overlap;

-- 2. 收紧 embedding_dimensions 默认/约束（最大 2000，建 hnsw 索引）
ALTER TABLE public.wiki
    DROP CONSTRAINT IF EXISTS idx_wiki_embedding_dimensions_max;
ALTER TABLE public.wiki
    ADD CONSTRAINT idx_wiki_embedding_dimensions_max
        CHECK (embedding_dimensions >= 1 AND embedding_dimensions <= 2000);

-- 3. 同步注释
COMMENT ON COLUMN public.wiki.embedding_dimensions IS '知识库向量维度（1-2000，建 hnsw 索引的硬上限）';
