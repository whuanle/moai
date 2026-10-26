-- 上架审核版本号 schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/publication_review_version.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 申请上架的版本号（int 编码 aaa*10000 + bb*100 + cc，显示 aaa.bb.cc），仅应用类型使用
alter table publication_review add column if not exists publish_version int;
comment on column publication_review.publish_version is '申请上架的版本号（int 编码 aaa*10000 + bb*100 + cc，显示 aaa.bb.cc），仅应用类型使用，其他资源为 null';
