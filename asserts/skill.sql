-- 技能（skill）schema，新库由 EnsureCreated 依据实体/配置直接建成，存量库补表/补列执行本脚本
-- 用法：docker exec -i moai-postgres psql -U postgres -d moai < asserts/skill.sql

create table if not exists "skill" (
    id uuid primary key default uuid_generate_v4(),
    key varchar(30) not null,
    name varchar(50) not null,
    description varchar(255) not null default '',
    instructions text not null default '',
    files text not null default '[]',
    is_system boolean not null,
    team_id int not null,
    is_public boolean not null default false,
    is_disable boolean not null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
comment on table "skill" is '技能';
comment on column "skill".id is 'id';
comment on column "skill".key is '技能标识，全局唯一，蛇形命名，创建后不可变更';
comment on column "skill".name is '技能名称';
comment on column "skill".description is '技能描述，作为 Agent 工具列表中的能力说明';
comment on column "skill".instructions is '使用说明（markdown），技能加载时注入给 Agent';
comment on column "skill".files is '技能包文件清单 JSON';
comment on column "skill".is_system is '是否系统内置技能：脚本以程序集内嵌资源分发，不可删除';
comment on column "skill".team_id is '所属团队 id，>0=团队技能，0=系统级技能（is_system）或个人技能（归属 create_user_id）';
comment on column "skill".is_disable is '是否禁用';

-- 存量库补列：市场上架（publication_review resource_type=skill）审批通过后 is_public 置为 true
alter table "skill" add column if not exists is_public boolean not null default false;
comment on column "skill".is_public is '是否公开（市场上架审批通过后置为 true），公开技能全员可见可用';

create unique index if not exists idx_skill_key_live_uindex on "skill" (key) where (is_deleted = 0);
create index if not exists idx_skill_team_id on "skill" (team_id);
