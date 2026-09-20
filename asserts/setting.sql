-- 系统设置种子数据（存量库补种：只插入缺失的 key，不覆盖已保存的值）
-- 新库由 EnsureCreated 时 SettingSeed.Apply(modelBuilder) 枚举 SettingDefinitions 自动写入，无需执行本脚本
-- 新增设置项时：先在 SettingDefinitions 注册，再在本脚本同步补一条 insert
-- 用法：psql -h 127.0.0.1 -U postgres -d moai_v2 -f asserts/setting.sql
-- create_user_id / update_user_id 为非空列，与 EnsureCreated 种子一致取 0

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'root', '', '超级管理员', '1', 0, 0
where not exists (select 1 from setting where key = 'root');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'KG_ENABLED', '知识图谱', '开启后，团队可以使用知识图谱能力；关闭时无需填写连接信息.', 'false', 0, 0
where not exists (select 1 from setting where key = 'KG_ENABLED');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'KG_URI', '图数据库连接地址', '图数据库 Bolt 连接地址，例如 neo4j://127.0.0.1:7687 或 bolt://127.0.0.1:7687.', '', 0, 0
where not exists (select 1 from setting where key = 'KG_URI');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'KG_USERNAME', '图数据库用户名', '图数据库登录用户名.', '', 0, 0
where not exists (select 1 from setting where key = 'KG_USERNAME');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'KG_PASSWORD', '图数据库密码', '图数据库登录密码.', '', 0, 0
where not exists (select 1 from setting where key = 'KG_PASSWORD');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'KG_DIALECT', '图数据库方言', 'memgraph 或 neo4j，影响内省与索引语句；外部接入 Neo4j 实例时选 neo4j.', 'memgraph', 0, 0
where not exists (select 1 from setting where key = 'KG_DIALECT');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'WIKI_MAX_FILE_SIZE_MB', '知识库最大文件大小', '知识库上传文档的大小上限（MB），默认 50，0 表示不限制（平台硬上限 1GB）.', '50', 0, 0
where not exists (select 1 from setting where key = 'WIKI_MAX_FILE_SIZE_MB');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SANDBOX_MAX_TTL_SECONDS', '沙箱存活时间上限', '每个应用沙箱最大存活时间（秒），团队保存应用配置时不得超出；默认 86400（24 小时），范围 60~604800.', '86400', 0, 0
where not exists (select 1 from setting where key = 'SANDBOX_MAX_TTL_SECONDS');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SANDBOX_MAX_CPU', '沙箱 CPU 上限', '每个应用沙箱 CPU 限制上限（K8s 数量格式，如 4 或 2000m），团队保存应用配置时不得超出；默认 4.', '4', 0, 0
where not exists (select 1 from setting where key = 'SANDBOX_MAX_CPU');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SANDBOX_MAX_MEMORY', '沙箱内存上限', '每个应用沙箱内存限制上限（K8s 数量格式，如 8Gi 或 512Mi），团队保存应用配置时不得超出；默认 8Gi.', '8Gi', 0, 0
where not exists (select 1 from setting where key = 'SANDBOX_MAX_MEMORY');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SYSTEM_LOGO', '网站 Logo', '上传后替换全局网站 Logo（侧边栏与登录/注册页），留空使用默认 Logo.', '', 0, 0
where not exists (select 1 from setting where key = 'SYSTEM_LOGO');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SYSTEM_NAME', '网站名称', '仅影响前端展示（侧边栏标题与浏览器标签页），留空使用配置文件默认名称.', '', 0, 0
where not exists (select 1 from setting where key = 'SYSTEM_NAME');
