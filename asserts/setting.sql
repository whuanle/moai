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

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'ORG_DEFAULT_ROLE', '组织默认角色', '新注册用户自动获得的角色（org_role 主键），留空表示新用户无默认角色；在组织角色页维护.', '', 0, 0
where not exists (select 1 from setting where key = 'ORG_DEFAULT_ROLE');

-- 2026-10-28 下线 AGENT_COMPACTION_DEFAULTS 设置项：压缩默认参数改为内置常量（应用级配置保留），存量行清理
delete from setting where key = 'AGENT_COMPACTION_DEFAULTS';

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_ENABLED', 'SMTP 邮件服务', '启用后，平台通知将通过 SMTP 服务发送邮件；不启用时无需填写服务器信息.', 'false', 0, 0
where not exists (select 1 from setting where key = 'SMTP_ENABLED');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_HOST', 'SMTP 服务器地址', 'SMTP 服务器域名或 IP，例如 smtp.exmail.qq.com.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_HOST');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_PORT', 'SMTP 端口', 'SMTP 服务器端口，1~65535；隐式 TLS 通常 465，STARTTLS 通常 587.', '465', 0, 0
where not exists (select 1 from setting where key = 'SMTP_PORT');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_SECURITY', 'SMTP 加密方式', 'ssl（隐式 TLS，通常 465）/ starttls（通常 587）/ none（不加密，通常 25）.', 'ssl', 0, 0
where not exists (select 1 from setting where key = 'SMTP_SECURITY');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_USERNAME', 'SMTP 登录账号', 'SMTP 登录账号，内网中继服务器免认证时可留空.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_USERNAME');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_PASSWORD', 'SMTP 登录密码', 'SMTP 登录密码或邮箱授权码.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_PASSWORD');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_FROM_ADDRESS', '发件人地址', '通知邮件的发件人邮箱地址，留空时使用 SMTP 登录账号.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_FROM_ADDRESS');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_FROM_NAME', '发件人显示名', '通知邮件的发件人显示名，例如 MoAI 通知.', '', 0, 0
where not exists (select 1 from setting where key = 'SMTP_FROM_NAME');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'REGISTER_EMAIL_CODE_ENABLED', '注册需要邮箱验证码', '开启后，新用户注册必须先获取邮箱验证码并校验通过才能完成注册；需 SMTP 邮件服务已启用才生效.', 'false', 0, 0
where not exists (select 1 from setting where key = 'REGISTER_EMAIL_CODE_ENABLED');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_REGISTER_CODE_SUBJECT', '注册验证码邮件主题', '注册验证码邮件主题模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.', '{siteName} 注册验证码', 0, 0
where not exists (select 1 from setting where key = 'SMTP_REGISTER_CODE_SUBJECT');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_REGISTER_CODE_BODY', '注册验证码邮件正文', '注册验证码邮件正文模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.', '你的注册验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.', 0, 0
where not exists (select 1 from setting where key = 'SMTP_REGISTER_CODE_BODY');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_RESET_CODE_SUBJECT', '找回密码验证码邮件主题', '找回密码验证码邮件主题模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.', '{siteName} 找回密码验证码', 0, 0
where not exists (select 1 from setting where key = 'SMTP_RESET_CODE_SUBJECT');

insert into setting (key, name, description, value, create_user_id, update_user_id)
select 'SMTP_RESET_CODE_BODY', '找回密码验证码邮件正文', '找回密码验证码邮件正文模板，支持 {code} {siteName} {expireMinutes} 变量，留空使用默认模板.', '你的找回密码验证码是 {code}，{expireMinutes} 分钟内有效。如非本人操作请忽略本邮件.', 0, 0
where not exists (select 1 from setting where key = 'SMTP_RESET_CODE_BODY');

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
