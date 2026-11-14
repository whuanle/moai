-- 初始化 PostgreSQL 扩展
CREATE EXTENSION IF NOT EXISTS vector;
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
-- 知识库混合检索关键词路径（切片原文/元数据 ILIKE 加速）
CREATE EXTENSION IF NOT EXISTS pg_trgm;
