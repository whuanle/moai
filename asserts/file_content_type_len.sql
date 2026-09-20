-- file 表 content_type 列宽扩展（varchar(50) → varchar(255)）
-- 背景：sandbox_save_artifact 等保存 Office 文档时 MIME 形如
--   application/vnd.openxmlformats-officedocument.wordprocessingml.document（70+ 字符），
-- 超过旧列宽导致整批 SaveChanges（含流程实例检查点）失败（Npgsql 22001）。
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/file_content_type_len.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成（varchar(255)），无需执行本脚本。

alter table file alter column content_type type varchar(255);
comment on column file.content_type is '文件类型（MIME，如 application/vnd.openxmlformats-officedocument.wordprocessingml.document 可达 70+ 字符）';
