-- 外部应用接入 · 外部用户（external_user）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/external_app.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 外部用户：一次外部访问所对应的外部身份记录，id 承载会话与用量归属
create table if not exists "external_user" (
    id bigserial primary key,
    team_id int not null,
    app_id uuid null,
    access_app_id uuid null,
    external_user_id varchar(128) not null,
    nickname varchar(100) null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
comment on table "external_user" is '外部用户（外部应用接入的身份记录）';
comment on column "external_user".id is '外部用户id，自增主键，承载会话 create_user_id 与用量 user_id';
comment on column "external_user".team_id is '归属团队id';
comment on column "external_user".app_id is '授权访问的应用id，is_auth=false 匿名访问时为来源应用，用户 token 指定授权的单个应用';
comment on column "external_user".access_app_id is '来源应用接入id（access_app.key 换取 token 时写入），匿名访问为 null';
comment on column "external_user".external_user_id is '外部身份标识，第三方系统的用户唯一标识或临时随机值';
comment on column "external_user".nickname is '外部用户显示名，可选';

-- 同一接入下同一外部身份只保留一条记录（复用 id → 继承聊天与消费记录）；匿名身份不绑定接入，不受此约束
create unique index if not exists idx_external_user_accessapp_user_uindex
    on "external_user" (access_app_id, external_user_id)
    where access_app_id is not null and is_deleted = 0;
