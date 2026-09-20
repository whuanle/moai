-- Agent 应用绑定流程应用（作为工具使用）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_workflow_apps.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 绑定的流程应用ID列表（作为工具使用），JSON 数组文本，元素为 app.id（uuid 字符串，须为本团队已发布流程应用），空为'[]'
alter table app_agent_config add column if not exists workflow_apps text not null default '[]';
comment on column app_agent_config.workflow_apps is '绑定的流程应用ID列表（作为工具使用），JSON 数组文本，元素为 app.id（uuid 字符串，须为本团队已发布流程应用），空为''[]''';
