-- 插件语义配置：插件级使用须知 + 语义条目 + 工具元数据（补充说明/调用策略/MCP 行为注解）
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 PluginSemanticNoteEntity / PluginToolMetaEntity 及其 Configuration 一致
-- 用法：psql -h 127.0.0.1 -U postgres -d moai_v2 -f asserts/plugin_semantic.sql

-- 1) plugin.usage_notes：插件级使用须知/领域背景（随工具清单注入模型系统提示）
alter table plugin add column if not exists usage_notes text not null default '';
comment on column plugin.usage_notes is '使用须知/领域背景（管理员维护，随工具清单注入模型系统提示）';

-- 2) plugin_semantic_note：语义条目（always=常驻注入；on_demand=经 plugin_note 元工具按需取回）
create table if not exists plugin_semantic_note (
    id uuid not null default uuid_generate_v4(),
    plugin_id uuid not null,
    tool_key character varying(200) not null default '',
    title character varying(100) not null default '',
    content text not null default '',
    mode integer not null default 0,
    sort_order integer not null default 0,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default timezone('utc'::text, now()),
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint plugin_semantic_note_pkey primary key (id)
);
comment on table plugin_semantic_note is '插件语义条目：按（插件,工具）粒度维护语义说明，按披露方式注入 AI 上下文';
comment on column plugin_semantic_note.id is 'id';
comment on column plugin_semantic_note.plugin_id is '所属插件 id，逻辑关联 plugin.id';
comment on column plugin_semantic_note.tool_key is '工具 key，空串=插件级条目；否则为工具 key（MCP/OpenAPI 为函数名，动态插件为工具 key）';
comment on column plugin_semantic_note.title is '条目标题（按需披露时作为目录展示给模型）';
comment on column plugin_semantic_note.content is '条目内容（markdown，如库表用途、字段说明、业务术语、示例问答）';
comment on column plugin_semantic_note.mode is '披露方式：0=always 常驻注入上下文；1=on_demand 经 plugin_note 元工具按需取回';
comment on column plugin_semantic_note.sort_order is '排序';
comment on column plugin_semantic_note.create_user_id is '创建人';
comment on column plugin_semantic_note.create_time is '创建时间';
comment on column plugin_semantic_note.update_user_id is '最后修改人';
comment on column plugin_semantic_note.update_time is '更新时间';
comment on column plugin_semantic_note.is_deleted is '软删除，0=未删除';

create index if not exists idx_plugin_semantic_note_plugin_id on plugin_semantic_note (plugin_id);

-- 3) plugin_tool_meta：工具元数据（补充说明 + 调用策略 + MCP 注解），按（插件,工具 key）粒度
create table if not exists plugin_tool_meta (
    id uuid not null default uuid_generate_v4(),
    plugin_id uuid not null,
    tool_key character varying(200) not null default '',
    extra_notes text not null default '',
    call_policy text not null default '',
    read_only_hint boolean not null default false,
    destructive_hint boolean not null default false,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default timezone('utc'::text, now()),
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint plugin_tool_meta_pkey primary key (id)
);
comment on table plugin_tool_meta is '插件工具元数据：按（插件,工具）粒度维护补充说明、调用策略与 MCP 行为注解';
comment on column plugin_tool_meta.id is 'id';
comment on column plugin_tool_meta.plugin_id is '所属插件 id，逻辑关联 plugin.id';
comment on column plugin_tool_meta.tool_key is '工具 key（MCP/OpenAPI 为函数名，动态插件为工具 key，静态插件为 run）';
comment on column plugin_tool_meta.extra_notes is '工具补充说明（叠加在原始描述之上进入模型工具说明）';
comment on column plugin_tool_meta.call_policy is '调用策略 JSON（denyPatterns 正则数组 + action：block|confirm|audit），空串=未配置';
comment on column plugin_tool_meta.read_only_hint is '只读提示（MCP readOnlyHint 采集或人工标注，不构成安全保证）';
comment on column plugin_tool_meta.destructive_hint is '破坏性提示（MCP destructiveHint 采集或人工标注，不构成安全保证）';
comment on column plugin_tool_meta.create_user_id is '创建人';
comment on column plugin_tool_meta.create_time is '创建时间';
comment on column plugin_tool_meta.update_user_id is '最后修改人';
comment on column plugin_tool_meta.update_time is '更新时间';
comment on column plugin_tool_meta.is_deleted is '软删除，0=未删除';

create index if not exists idx_plugin_tool_meta_plugin_id on plugin_tool_meta (plugin_id);
