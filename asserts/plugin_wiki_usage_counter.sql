-- 插件与知识库使用次数可能长期增长，统一升级为 PostgreSQL bigint。
-- PostgreSQL 允许对已经是 bigint 的列重复执行 TYPE bigint。

ALTER TABLE plugin
    ALTER COLUMN counter TYPE bigint;

ALTER TABLE wiki
    ALTER COLUMN counter TYPE bigint;