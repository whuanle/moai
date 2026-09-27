-- 修复存量数据：admin 导入的系统自定义插件（MCP/OpenAPI）落库时漏标 is_system
-- 背景：plugin 表中「系统插件」的查询口径是 is_system = true AND team_id = 0，
--       而 ImportMcpServerPluginCommandHandler / ImportOpenApiPluginCommandHandler 此前创建记录时
--       未赋值 is_system（默认 false），导致这些系统插件永远不会出现在团队可用插件列表中，
--       即使已通过 plugin_team_authorization 授权给团队也无法在应用中使用。
-- 修复：team_id = 0 且 is_system = 0 的记录都是 admin 导入的系统自定义插件（团队插件的 team_id 恒大于 0），统一补标。
-- 用法：psql -h 127.0.0.1 -U postgres -d moai_v2 -f asserts/plugin_system_flag.sql

update plugin
set is_system = 1
where team_id = 0
  and is_system = 0
  and is_deleted = 0;
