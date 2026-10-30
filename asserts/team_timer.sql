-- 团队定时器（绑定应用定时执行）与执行记录
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 TeamTimerEntity/TeamTimerExecutionEntity 及其 Configuration 一致

create table if not exists team_timer (
    id uuid not null default uuid_generate_v4(),
    team_id integer not null,
    name varchar(100) not null,
    description varchar(255) not null default '',
    app_id uuid not null,
    app_type integer not null default 0,
    cron_expression varchar(100) not null,
    input_content text not null,
    is_enabled boolean not null default false,
    max_retry_count integer not null default 0,
    retry_interval_seconds integer not null default 60,
    last_run_time timestamptz null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint team_timer_pkey primary key (id)
);
comment on table team_timer is '团队定时器：绑定应用按 cron 定时无头执行';
comment on column team_timer.id is 'id';
comment on column team_timer.team_id is '所属团队 id，逻辑关联 team.id';
comment on column team_timer.name is '定时器名称，团队内唯一';
comment on column team_timer.description is '描述';
comment on column team_timer.app_id is '绑定的应用 id，逻辑关联 app.id';
comment on column team_timer.app_type is '绑定时的应用类型快照，0=Agent 1=流程';
comment on column team_timer.cron_expression is '调度 cron 表达式（UTC，5 段标准格式）';
comment on column team_timer.input_content is '每次执行时作为提问发送给应用的内容';
comment on column team_timer.is_enabled is '是否启用调度';
comment on column team_timer.max_retry_count is '失败重试次数（不含首次执行）';
comment on column team_timer.retry_interval_seconds is '重试间隔秒数';
comment on column team_timer.last_run_time is '最近一次触发时间，未执行过为 null';
comment on column team_timer.is_deleted is '软删除，0=未删除';

create index if not exists idx_team_timer_team_id on team_timer (team_id);
create unique index if not exists ux_team_timer_team_name_live_uindex on team_timer (team_id, name, is_deleted) where (is_deleted = 0);

create table if not exists team_timer_execution (
    id uuid not null default uuid_generate_v4(),
    timer_id uuid not null,
    team_id integer not null,
    trigger_type integer not null default 0,
    trigger_user_id bigint not null default 0,
    status integer not null default 0,
    attempt_count integer not null default 0,
    start_time timestamptz null,
    end_time timestamptz null,
    duration_ms bigint null,
    next_retry_time timestamptz null,
    app_instance_id varchar(64) null,
    result text null,
    error_message text null,
    create_user_id bigint not null default 0,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null default 0,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0,
    constraint team_timer_execution_pkey primary key (id)
);
comment on table team_timer_execution is '团队定时器执行记录：一次触发一行，失败重试在同一行内累计尝试次数';
comment on column team_timer_execution.id is 'id';
comment on column team_timer_execution.timer_id is '定时器 id，逻辑关联 team_timer.id';
comment on column team_timer_execution.team_id is '所属团队 id，逻辑关联 team_timer.team_id';
comment on column team_timer_execution.trigger_type is '触发方式，0=调度 1=手动';
comment on column team_timer_execution.trigger_user_id is '触发用户 id（手动=操作人，调度=定时器创建人）';
comment on column team_timer_execution.status is '执行状态，0=等待 1=执行中 2=成功 3=失败 4=待重试';
comment on column team_timer_execution.attempt_count is '已尝试次数（首次执行记 1）';
comment on column team_timer_execution.start_time is '首次开始执行时间，未开始为 null';
comment on column team_timer_execution.end_time is '结束时间，未结束为 null';
comment on column team_timer_execution.duration_ms is '总耗时毫秒，未结束为 null';
comment on column team_timer_execution.next_retry_time is '下次重试时间，仅待重试状态有值';
comment on column team_timer_execution.app_instance_id is '流程应用实例 id，Agent 应用为 null';
comment on column team_timer_execution.result is '应用回答文本';
comment on column team_timer_execution.error_message is '失败原因';
comment on column team_timer_execution.is_deleted is '软删除，0=未删除';

create index if not exists idx_team_timer_execution_timer_create on team_timer_execution (timer_id, create_time);
create index if not exists idx_team_timer_execution_team_id on team_timer_execution (team_id);

-- 2026-10-30 增量：定时执行落会话（Agent 应用），执行记录关联会话 id
alter table team_timer_execution add column if not exists session_id uuid null;
comment on column team_timer_execution.session_id is '定时执行产生的 Agent 会话 id（app_agent_session，user_type=4 timer），流程应用为 null';
