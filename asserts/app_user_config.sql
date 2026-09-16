-- 用户级应用配置（app_user_config）schema，新库由 EnsureCreated 依据实体/配置直接建成，存量库补表执行本脚本
-- 用法：docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_user_config.sql

create table if not exists app_user_config (
    id uuid primary key default uuid_generate_v4(),
    app_id uuid not null,
    user_id bigint not null,
    team_id int not null,
    prompt_id int not null default 0,
    skills text not null default '[]',
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
comment on table app_user_config is '用户级应用配置，(app_id, user_id) 唯一，跨会话复用';
comment on column app_user_config.id is 'id';
comment on column app_user_config.app_id is '应用 id，逻辑关联 app.id';
comment on column app_user_config.user_id is '用户 id，逻辑关联 user.id';
comment on column app_user_config.team_id is '所属团队 id，逻辑关联 app.team_id，冗余用于团队维度过滤';
comment on column app_user_config.prompt_id is '用户为新会话选择的专家提示词 id，0=未设置';
comment on column app_user_config.skills is '用户自选技能 id 列表 JSON 数组，与应用绑定技能取并集生效';

create unique index if not exists idx_app_user_config_app_user_live_uindex on app_user_config (app_id, user_id) where (is_deleted = 0);
create index if not exists idx_app_user_config_team_id on app_user_config (team_id);
