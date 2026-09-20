-- Agent 应用快捷输入 schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_quick_inputs.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 快捷输入列表，JSON 数组文本，元素为字符串（管理员配置，用户在对话欢迎态点击即发送），空为'[]'
alter table app_agent_config add column if not exists quick_inputs text not null default '[]';
comment on column app_agent_config.quick_inputs is '快捷输入列表，JSON 数组文本，元素为字符串（管理员配置，用户在对话欢迎态点击即发送），空为''[]''';
