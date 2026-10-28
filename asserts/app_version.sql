-- 应用版本号 schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_version.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 应用版本号，编码 aaa*10000 + bb*100 + cc，显示 aaa.bb.cc；存量应用默认 10000=1.0.0
alter table app add column if not exists version int not null default 10000;
comment on column app.version is '应用版本号，编码 aaa*10000 + bb*100 + cc，显示 aaa.bb.cc（默认 10000=1.0.0）';

-- 发布时快照的应用版本号；null=未发布或存量已发布应用（重新发布后写入）
alter table app add column if not exists publish_version int;
comment on column app.publish_version is '发布时快照的应用版本号，未发布或存量已发布应用（未重新发布过）为 null';
