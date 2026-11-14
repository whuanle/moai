-- 第三方登录连接器「登录页显示」开关 schema 变更（隐藏连接器：纯身份映射用）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/oauth-connection-visible.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 是否显示到登录页与个人中心绑定页：false=隐藏连接器（仅供系统接口绑定第三方 id 与换发访问用户 token）
alter table oauth_connection add column if not exists is_visible boolean not null default true;
comment on column oauth_connection.is_visible is '是否显示到登录页与个人中心绑定页（false=隐藏连接器，纯身份映射用）';
