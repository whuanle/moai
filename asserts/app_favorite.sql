-- 应用收藏（app_favorite）schema，新库由 EnsureCreated 依据实体/配置直接建成，存量库补表执行本脚本
-- 用法：docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_favorite.sql

create table if not exists app_favorite (
    id uuid primary key default uuid_generate_v4(),
    app_id uuid not null,
    user_id bigint not null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
comment on table app_favorite is '应用收藏，(app_id, user_id) 唯一（活跃行），应用广场收藏置顶排序的数据源';
comment on column app_favorite.id is 'id';
comment on column app_favorite.app_id is '应用 id，逻辑关联 app.id';
comment on column app_favorite.user_id is '用户 id，逻辑关联 user.id';

create unique index if not exists idx_app_favorite_app_user_live_uindex on app_favorite (app_id, user_id) where (is_deleted = 0);
