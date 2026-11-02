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
select 'WIKI_ALLOWED_FILE_TYPES', '知识库允许上传的文件类型', '知识库允许上传的文档扩展名（逗号分隔，如 .pdf,.docx,.md），对网页端与开放接口上传同时生效；默认与文档/文本提取能力对齐，图片暂不支持.', '.pdf,.docx,.xlsx,.pptx,.html,.htm,.md,.markdown,.txt,.json', 0, 0
where not exists (select 1 from setting where key = 'WIKI_ALLOWED_FILE_TYPES');

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
select 'SYSTEM_NAME', '网站名称', '仅影响前端展示（侧边栏标题、登录/注册页与浏览器标签页），留空使用配置文件默认名称.', '', 0, 0
where not exists (select 1 from setting where key = 'SYSTEM_NAME');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'LOGIN_BACKGROUND', '登录页背景图', '上传后替换登录页整页背景，留空使用前端默认背景.', '', 0, 0
where not exists (select 1 from setting where key = 'LOGIN_BACKGROUND');

-- 2026-11-02 下线 ORG_DEFAULT_ROLE（默认组织功能下线，注册不再自动赋予组织），存量行清理
delete from setting where key = 'ORG_DEFAULT_ROLE';

-- 2026-10-28 下线 AGENT_COMPACTION_DEFAULTS 设置项：压缩默认参数改为内置常量（应用级配置保留），存量行清理
delete from setting where key = 'AGENT_COMPACTION_DEFAULTS';

-- 2026-11-02 SMTP 连接配置收敛到通知中心渠道：系统设置仅保留邮箱验证总开关 + 绑定渠道 id，存量 7 个连接键作废清理
delete from setting where key in ('SMTP_HOST', 'SMTP_PORT', 'SMTP_SECURITY', 'SMTP_USERNAME', 'SMTP_PASSWORD', 'SMTP_FROM_ADDRESS', 'SMTP_FROM_NAME');

-- 2026-11-02 验证码邮件模板收敛到通知中心模板（内置种子 + 系统设置绑定模板 id）：存量 6 个主题/正文键作废清理
delete from setting where key in ('SMTP_REGISTER_CODE_SUBJECT', 'SMTP_REGISTER_CODE_BODY', 'SMTP_RESET_CODE_SUBJECT', 'SMTP_RESET_CODE_BODY', 'SMTP_LOGIN_CODE_SUBJECT', 'SMTP_LOGIN_CODE_BODY');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_ENABLED', '邮箱验证', '启用后，平台邮件（注册/找回密码/登录验证码、安全告警等）通过绑定的 SMTP 服务发送；SMTP 连接配置统一在通知中心维护，此处仅绑定使用.', 'false', 0, 0
where not exists (select 1 from setting where key = 'SMTP_ENABLED');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_CHANNEL_ID', '绑定 SMTP 服务', '绑定的通知中心 SMTP 渠道 id（notification_channel.id），留空表示未绑定；渠道连接配置在通知中心维护.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_CHANNEL_ID');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'REGISTER_EMAIL_CODE_ENABLED', '注册需要邮箱验证码', '开启后，新用户注册必须先获取邮箱验证码并校验通过才能完成注册；需 SMTP 邮件服务已启用才生效.', 'false', 0, 0
where not exists (select 1 from setting where key = 'REGISTER_EMAIL_CODE_ENABLED');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_REGISTER_CODE_TEMPLATE_ID', '注册验证码通知模板', '绑定的通知中心模板 id（notification_template.id），留空使用平台内置的注册验证码模板；模板内容与渲染测试在通知中心维护.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_REGISTER_CODE_TEMPLATE_ID');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_RESET_CODE_TEMPLATE_ID', '找回密码验证码通知模板', '绑定的通知中心模板 id（notification_template.id），留空使用平台内置的找回密码验证码模板；模板内容与渲染测试在通知中心维护.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_RESET_CODE_TEMPLATE_ID');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'MEMORY_ENABLED', '长期记忆', '长期记忆总开关：开启后应用可开启记忆，用户可在对话中维护应用内记忆与个人跨应用记忆（全局记忆）；关闭时应用不可开启记忆.', 'false', 0, 0
where not exists (select 1 from setting where key = 'MEMORY_ENABLED');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'MEMORY_EMBEDDING_MODEL', '记忆向量化模型', '应用记忆与全局记忆统一使用的向量化模型 id（公开模型），写入任意记忆后锁定，更换需先清空全部记忆.', '', 0, 0
where not exists (select 1 from setting where key = 'MEMORY_EMBEDDING_MODEL');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'MEMORY_EMBEDDING_DIMENSIONS', '记忆向量维度', '记忆统一向量维度（1~2000），默认 1024；与向量化模型一起在写入任意记忆后锁定.', '1024', 0, 0
where not exists (select 1 from setting where key = 'MEMORY_EMBEDDING_DIMENSIONS');

-- 旧版键清理（历史遗留：全局记忆独立设置，已并入 MEMORY_* 统一口径）
delete from setting where key in ('MEMORY_GLOBAL_ENABLED', 'MEMORY_GLOBAL_EMBEDDING_MODEL', 'MEMORY_GLOBAL_EMBEDDING_DIMENSIONS');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'LOGIN_FORCED_2FA', '强制两步验证', '开启后，未绑定 2FA 的用户登录时强制进入个人设置绑定两步验证，绑定完成后才能使用系统.', 'false', 0, 0
where not exists (select 1 from setting where key = 'LOGIN_FORCED_2FA');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'LOGIN_EMAIL_CODE_MODE', '登录邮箱验证码策略', 'off=关闭（默认）/ always=每次登录都需要邮箱验证码 / after_fail=密码错误一次后的登录需要邮箱验证码；需 SMTP 邮件服务已启用才生效.', 'off', 0, 0
where not exists (select 1 from setting where key = 'LOGIN_EMAIL_CODE_MODE');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_LOGIN_CODE_TEMPLATE_ID', '登录验证码通知模板', '绑定的通知中心模板 id（notification_template.id），留空使用平台内置的登录验证码模板；模板内容与渲染测试在通知中心维护.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_LOGIN_CODE_TEMPLATE_ID');
