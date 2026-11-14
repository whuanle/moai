-- 知识库检索经验聚类（等价于启动时幂等 DDL，免手工执行）
-- D44：验证过的查询→命中沉淀为可复用知识单元，(wiki_id, query_hash) 部分唯一

CREATE TABLE IF NOT EXISTS wiki_recall_cluster (
    id bigint NOT NULL,
    wiki_id int NOT NULL,
    query_hash varchar(64) NOT NULL,
    query varchar(1000) NOT NULL,
    index_epoch bigint NOT NULL DEFAULT 0,
    hits_json text NOT NULL,
    hit_count int NOT NULL DEFAULT 0,
    recall_count int NOT NULL DEFAULT 0,
    last_recall_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    update_user_id bigint NOT NULL DEFAULT 0,
    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    is_deleted bigint NOT NULL DEFAULT 0,
    CONSTRAINT idx_wiki_recall_cluster_primary PRIMARY KEY (id)
);

CREATE INDEX IF NOT EXISTS idx_wiki_recall_cluster_wiki_id ON wiki_recall_cluster (wiki_id) WHERE is_deleted = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_wiki_recall_cluster_wiki_query_uindex ON wiki_recall_cluster (wiki_id, query_hash) WHERE is_deleted = 0;

-- wiki.index_epoch（索引纪元，聚类新鲜度依据：文档/切片/向量集合变更时 +1）
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS index_epoch bigint NOT NULL DEFAULT 0;

-- 聚类表后加列（存量库 CREATE TABLE IF NOT EXISTS 不会补列，幂等 ALTER 补齐）
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS index_epoch bigint NOT NULL DEFAULT 0;
