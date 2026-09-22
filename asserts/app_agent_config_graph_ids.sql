-- Agent 应用绑定知识图谱（SP-A）：app_agent_config 表新增 graph_ids（存量库补列，幂等）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_config_graph_ids.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 绑定的知识图谱ID列表，JSON 数组文本，元素为 knowledge_graph.id（整数），如'[1,2]'
alter table app_agent_config add column if not exists graph_ids text not null default '[]';
comment on column app_agent_config.graph_ids is '绑定的知识图谱ID列表，JSON 数组文本，元素为 knowledge_graph.id（整数），如''[1,2]''';
