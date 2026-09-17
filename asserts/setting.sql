-- 系统设置种子数据（存量库补种：只插入缺失的 key，不覆盖已保存的值）
-- 新库由 EnsureCreated 时 SettingSeed.Apply(modelBuilder) 枚举 SettingDefinitions 自动写入，无需执行本脚本
-- 新增设置项时：先在 SettingDefinitions 注册，再在本脚本同步补一条 insert
-- 用法：psql -h 127.0.0.1 -U postgres -d moai_v2 -f asserts/setting.sql

insert into setting (key, name, description, value)
select 'root', '', '超级管理员', '1'
where not exists (select 1 from setting where key = 'root');

insert into setting (key, name, description, value)
select 'KG_ENABLED', '知识图谱', '开启后，团队可以使用知识图谱能力；关闭时无需填写连接信息.', 'false'
where not exists (select 1 from setting where key = 'KG_ENABLED');

insert into setting (key, name, description, value)
select 'KG_URI', '图数据库连接地址', '图数据库 Bolt 连接地址，例如 neo4j://127.0.0.1:7687 或 bolt://127.0.0.1:7687.', ''
where not exists (select 1 from setting where key = 'KG_URI');

insert into setting (key, name, description, value)
select 'KG_USERNAME', '图数据库用户名', '图数据库登录用户名.', ''
where not exists (select 1 from setting where key = 'KG_USERNAME');

insert into setting (key, name, description, value)
select 'KG_PASSWORD', '图数据库密码', '图数据库登录密码.', ''
where not exists (select 1 from setting where key = 'KG_PASSWORD');

insert into setting (key, name, description, value)
select 'KG_DIALECT', '图数据库方言', 'memgraph 或 neo4j，影响内省与索引语句；外部接入 Neo4j 实例时选 neo4j.', 'memgraph'
where not exists (select 1 from setting where key = 'KG_DIALECT');

insert into setting (key, name, description, value)
select 'WIKI_MAX_FILE_SIZE_MB', '知识库最大文件大小', '知识库上传文档的大小上限（MB），默认 50，0 表示不限制（平台硬上限 1GB）.', '50'
where not exists (select 1 from setting where key = 'WIKI_MAX_FILE_SIZE_MB');
