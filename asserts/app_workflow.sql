-- 流程应用（MoAI.App.Workflow）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_workflow.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 流程应用编排配置，与 app 一一对应（app_type=1）
create table if not exists app_workflow_config (
    id uuid default uuid_generate_v4() not null
        constraint app_workflow_config_pkey primary key,
    team_id integer not null,
    app_id uuid not null,
    draft_definition text not null,
    draft_editor_data text not null default '{}'::text,
    published_definition text null,
    version integer not null default 0,
    status smallint not null default 0,
    publish_time timestamptz null,
    create_user_id bigint not null,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
create unique index if not exists idx_app_workflow_config_app_id_live_uindex
    on app_workflow_config (app_id) where (is_deleted = 0);
create index if not exists idx_app_workflow_config_team_id on app_workflow_config (team_id);
comment on table app_workflow_config is '流程应用编排配置，与 app 一一对应（app_type=1）';
comment on column app_workflow_config.draft_definition is '草稿流程定义 JSON（引擎 WorkflowDefinition 契约：nodes + connections + ui）';
comment on column app_workflow_config.draft_editor_data is '草稿编辑器原始 JSON（FlowGram 画布 toJSON 产物，用于无损还原画布），空为 ''{}''';
comment on column app_workflow_config.published_definition is '已发布定义快照 JSON，发布后不可变；从未发布为 null';
comment on column app_workflow_config.version is '当前已发布版本号，发布一次递增 1，0=从未发布';
comment on column app_workflow_config.status is '状态，0=草稿有未发布变更（或从未发布） 1=当前草稿已发布（草稿与已发布版本一致）';
comment on column app_workflow_config.publish_time is '最近发布时间，从未发布为 null';

-- 流程应用运行实例，一次工作流执行的完整快照（含节点级状态，支撑断点恢复）
create table if not exists app_workflow_instance (
    id uuid default uuid_generate_v4() not null
        constraint app_workflow_instance_pkey primary key,
    team_id integer not null,
    app_id uuid not null,
    workflow_config_id uuid not null,
    version integer not null default 0,
    is_debug boolean not null default false,
    status smallint not null default 0,
    input text not null default '{}'::text,
    output text null,
    error_message text null,
    instance_data text not null,
    start_time timestamptz null,
    end_time timestamptz null,
    create_user_id bigint not null,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
create index if not exists idx_app_workflow_instance_app_id on app_workflow_instance (app_id);
create index if not exists idx_app_workflow_instance_team_id on app_workflow_instance (team_id);
create index if not exists idx_app_workflow_instance_config_id on app_workflow_instance (workflow_config_id);
comment on table app_workflow_instance is '流程应用运行实例，一次工作流执行的完整快照（含节点级状态，支撑断点恢复）';
comment on column app_workflow_instance.version is '执行引用的定义版本号，0=调试执行（运行草稿定义）';
comment on column app_workflow_instance.is_debug is '是否调试运行（设计器内发起），0=正式执行 1=调试';
comment on column app_workflow_instance.status is '实例状态，0=已创建 1=执行中 2=已挂起 3=已完成 4=已取消';
comment on column app_workflow_instance.instance_data is '引擎实例全量 JSON（WorkflowInstance 序列化，含每个节点的状态/输入/输出/耗时），实例自含定义快照';
