-- 团队智能体（team_agent）schema，新库由 EnsureCreated 依据实体/配置直接建成，存量库补表/补列执行本脚本
-- 用法：docker exec -i moai-postgres psql -U postgres -d moai < asserts/team-agent.sql

create table if not exists "team_agent" (
    id uuid primary key default uuid_generate_v4(),
    team_id bigint not null default 0,
    key varchar(30) not null default '',
    name varchar(50) not null,
    description varchar(500) not null default '',
    avatar_object_key varchar(255) not null default '',
    enabled boolean not null default true,
    is_system boolean not null default false,
    main_prompt text not null default '',
    main_plugin_ids text not null default '[]',
    main_wiki_ids text not null default '[]',
    main_graph_ids text not null default '[]',
    subs text not null default '[]',
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
comment on table "team_agent" is '团队智能体，主智能体 + 多个子智能体的编排定义资产';
comment on column "team_agent".id is '智能体ID';
comment on column "team_agent".team_id is '所属团队ID，逻辑关联team.id（仓库约定不建物理外键）';
comment on column "team_agent".key is '智能体标识 key，团队内唯一（小写字母开头的字母/数字/下划线），创建后不可变更；Phase 2 将作为工具名暴露';
comment on column "team_agent".name is '智能体名称（展示名，可中文），团队内唯一';
comment on column "team_agent".description is '智能体描述，空串=未填写';
comment on column "team_agent".avatar_object_key is '头像文件 objectKey，空串=未设置';
comment on column "team_agent".enabled is '是否启用，停用后不可被应用绑定与运行';
comment on column "team_agent".is_system is '是否系统内置智能体：团队创建时自动种入（如规划/构建），不可删除、可编辑定制';
comment on column "team_agent".main_prompt is '主智能体系统提示词';
comment on column "team_agent".main_plugin_ids is '主智能体插件依赖，JSON uuid 字符串数组';
comment on column "team_agent".main_wiki_ids is '主智能体知识库依赖，JSON 数字数组';
comment on column "team_agent".main_graph_ids is '主智能体知识图谱依赖，JSON 数字数组';
comment on column "team_agent".subs is '子智能体列表，JSON 数组（key/name/description/prompt/pluginIds/wikiIds/graphIds），最多 20 个';

-- 存量库补列（早期草稿表无这些列时补齐）
alter table "team_agent" add column if not exists is_system boolean not null default false;
comment on column "team_agent".is_system is '是否系统内置智能体：团队创建时自动种入（如规划/构建），不可删除、可编辑定制';
alter table "team_agent" add column if not exists key varchar(30) not null default '';
alter table "team_agent" add column if not exists main_graph_ids text not null default '[]';

-- 偏好模型已收敛到 agents 应用对话模型配置，智能体不再自带（回收列）
alter table "team_agent" drop column if exists main_model_id;

create unique index if not exists idx_team_agent_team_name_live_uindex on "team_agent" (team_id, name) where (is_deleted = 0);
-- key 唯一排除空串（存量行 key 为空串不受约束），新行 key 必填即受唯一约束
create unique index if not exists idx_team_agent_team_key_live_uindex on "team_agent" (team_id, key) where (is_deleted = 0) and (key <> '');
create index if not exists idx_team_agent_team_id on "team_agent" (team_id);
