-- 外部应用访问点显示形式 schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_access_point_display_mode.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 访问点显示形式：floating 悬浮组件（默认）/ inline 内嵌小窗
alter table app_access_point add column if not exists display_mode varchar(20) not null default 'floating';
comment on column app_access_point.display_mode is '显示形式：floating 悬浮组件 / inline 内嵌小窗';
