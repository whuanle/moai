-- 应用长期记忆（Agent 记忆分片登记表）
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 AppMemoryShardEntity 及其 Configuration 一致
-- 向量本体在 __agent_mem_{appId} 动态表（由 VectorData 提供者按需自动创建，__ 前缀表不进 EF 模型）

create table if not exists app_memory_shard (
    id uuid not null default uuid_generate_v4(),
    app_id uuid not null,
    team_id integer not null,
    user_id bigint not null,
    memory_count integer not null default 0,
    last_active_time timestamptz not null default timezone('utc'::text, now()),
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint app_memory_shard_pkey primary key (id)
);
comment on table app_memory_shard is '应用记忆分片登记：用户在某应用下的长期记忆行数，支撑跨应用发现与清空';
comment on column app_memory_shard.id is 'id';
comment on column app_memory_shard.app_id is '应用 id，逻辑关联 app.id';
comment on column app_memory_shard.team_id is '所属团队 id，逻辑关联 app.team_id';
comment on column app_memory_shard.user_id is '用户 id，逻辑关联 user.id';
comment on column app_memory_shard.memory_count is '该用户在该应用向量集合中的记忆条数';
comment on column app_memory_shard.last_active_time is '最近一次记忆写入时间';
comment on column app_memory_shard.is_deleted is '软删除，0=未删除';

create index if not exists idx_app_memory_shard_app_id on app_memory_shard (app_id);
create index if not exists idx_app_memory_shard_user_id on app_memory_shard (user_id);
create unique index if not exists ux_app_memory_shard_app_user_live_uindex on app_memory_shard (app_id, user_id, is_deleted) where (is_deleted = 0);
