-- 外部应用访问点配置（app_access_point）schema，新库由 EnsureCreated 依据实体/配置直接建成
-- 用法（存量库）：docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_access_point.sql
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

create table if not exists "app_access_point" (
    id uuid primary key default uuid_generate_v4(),
    team_id int not null,
    app_id uuid not null,
    title varchar(100) null,
    subtitle varchar(255) null,
    placeholder varchar(100) null,
    primary_color varchar(20) null,
    position varchar(20) not null default 'bottomRight',
    launcher_text varchar(50) null,
    avatar varchar(255) null,
    panel_width int not null default 380,
    panel_height int not null default 560,
    default_open boolean not null default false,
    enabled boolean not null default true,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
comment on table "app_access_point" is '外部应用访问点配置，与外部应用 1:1';
comment on column "app_access_point".id is 'id';
comment on column "app_access_point".team_id is '归属团队id，冗余团队维度过滤';
comment on column "app_access_point".app_id is '外部应用id，1:1（partial 唯一）';
comment on column "app_access_point".title is '面板标题，空则用应用名';
comment on column "app_access_point".subtitle is '欢迎语/副标题';
comment on column "app_access_point".placeholder is '输入框占位文案';
comment on column "app_access_point".primary_color is '主题色，#RRGGBB';
comment on column "app_access_point".position is '悬浮位置：bottomRight / bottomLeft';
comment on column "app_access_point".launcher_text is '悬浮按钮文案，空则用图标';
comment on column "app_access_point".avatar is '头像 objectKey（走存储）';
comment on column "app_access_point".panel_width is '面板宽度 px';
comment on column "app_access_point".panel_height is '面板高度 px';
comment on column "app_access_point".default_open is '是否默认展开';
comment on column "app_access_point".enabled is '是否启用访问点';

-- 应用 1:1
create unique index if not exists idx_app_access_point_app_uindex
    on "app_access_point" (app_id)
    where (is_deleted = 0);
