-- 子系统注册与接入（subsystem / subsystem_team_auth）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/subsystem.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 子系统：独立部署、通过 appid+密钥接入 MoAI 的外部子系统（如智能报表），用户体系由 MoAI 承载
create table if not exists "subsystem" (
    id uuid not null default uuid_generate_v4(),
    key varchar(40) not null,
    name varchar(50) not null,
    description varchar(255) not null default ''::text,
    secret varchar(255) not null,
    secret_salt varchar(255) not null,
    status int not null default 0,
    team_scope int not null default 0,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint idx_subsystem_primary primary key (id)
);
comment on table "subsystem" is '子系统：独立部署、通过 appid+密钥接入 MoAI 的外部子系统';
comment on column "subsystem".id is 'id';
comment on column "subsystem".key is '子系统标识（appid），创建后不可修改';
comment on column "subsystem".name is '子系统名称';
comment on column "subsystem".description is '描述';
comment on column "subsystem".secret is '接入密钥哈希（PBKDF2），原文仅在创建/重置时返回一次';
comment on column "subsystem".secret_salt is '接入密钥盐（Base64）';
comment on column "subsystem".status is '状态：0=启用 1=停用，停用后拒绝换取 token 与机器面调用';
comment on column "subsystem".team_scope is '团队授权范围：0=全员可用 1=指定团队';

-- appid 在未删除记录中唯一
create unique index if not exists idx_subsystem_key_uindex
    on "subsystem" (key)
    where is_deleted = 0;

-- 子系统团队授权：指定团队范围下记录哪些团队可使用该子系统
create table if not exists "subsystem_team_auth" (
    id uuid not null default uuid_generate_v4(),
    subsystem_id uuid not null,
    team_id int not null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint idx_subsystem_team_auth_primary primary key (id)
);
comment on table "subsystem_team_auth" is '子系统团队授权：指定团队范围下记录哪些团队可使用该子系统';
comment on column "subsystem_team_auth".subsystem_id is '子系统id';
comment on column "subsystem_team_auth".team_id is '团队id';

-- 同一子系统同一团队只保留一条有效授权
create unique index if not exists idx_subsystem_team_auth_uindex
    on "subsystem_team_auth" (subsystem_id, team_id)
    where is_deleted = 0;

-- 委托 SSO：登录回调地址列（2026-10-28 追加，幂等）
alter table "subsystem" add column if not exists callback_url varchar(255) not null default ''::text;
comment on column "subsystem".callback_url is '委托 SSO 登录回调地址（子系统侧完整 URL），空串=未开放登录接入';
