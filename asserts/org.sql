-- 角色组织（org_role / org_user_role）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/org.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 组织角色：全局层级角色树（与团队无关），path 物化路径前缀匹配即得子树
create table if not exists "org_role" (
    id serial not null,
    name varchar(50) not null,
    parent_id int null,
    path varchar(255) not null default ''::text,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default timezone('utc'::text, now()),
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint idx_org_role_primary primary key (id)
);
comment on table "org_role" is '组织角色：全局层级角色树（与团队无关），path 物化路径前缀匹配即得子树';
comment on column "org_role".id is '自增主键';
comment on column "org_role".name is '角色名称，同一父角色下唯一';
comment on column "org_role".parent_id is '上级角色id，null=根角色';
comment on column "org_role".path is '物化路径：根为 /{id}/，子级逐段追加（如 /1/3/8/）';

-- 物化路径在未删除记录中唯一（id 链天然唯一，索引兜底）
create unique index if not exists idx_org_role_path_uindex
    on "org_role" (path)
    where is_deleted = 0;

create index if not exists idx_org_role_parent_id
    on "org_role" (parent_id);

-- 组织角色成员绑定：用户持有哪些组织角色，多对多
create table if not exists "org_user_role" (
    id serial not null,
    user_id bigint not null,
    role_id int not null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default timezone('utc'::text, now()),
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint idx_org_user_role_primary primary key (id)
);
comment on table "org_user_role" is '组织角色成员绑定：用户持有哪些组织角色，多对多';
comment on column "org_user_role".user_id is '用户id，逻辑关联user.id（仓库约定不建物理外键）';
comment on column "org_user_role".role_id is '组织角色id，逻辑关联org_role.id';

-- 同一用户同一角色只保留一条有效绑定
create unique index if not exists idx_org_user_role_user_role_live_uindex
    on "org_user_role" (user_id, role_id)
    where is_deleted = 0;

create index if not exists idx_org_user_role_role_id
    on "org_user_role" (role_id);
