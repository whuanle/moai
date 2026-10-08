-- 应用安全配置（内容脱敏，MoAI.App）schema 变更
-- 用法（存量库）：
--   psql -h <host> -U postgres -d moai -f asserts/app_security.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 应用安全配置，与 app 一一对应（Agent/流程应用通用），未配置行的应用视为未启用脱敏
create table if not exists app_security_config (
    id uuid default uuid_generate_v4() not null
        constraint app_security_config_pkey primary key,
    team_id integer not null,
    app_id uuid not null,
    enabled boolean not null default false,
    mask_tool_result boolean not null default true,
    mask_tool_args boolean not null default false,
    mask_model_output boolean not null default false,
    rules text not null default '[]'::text,
    model_output_rules text not null default '[]'::text,
    create_user_id bigint not null,
    create_time timestamptz not null default CURRENT_TIMESTAMP,
    update_user_id bigint not null,
    update_time timestamptz not null default timezone('utc'::text, now()),
    is_deleted bigint not null default 0
);
create unique index if not exists idx_app_security_config_app_id_live_uindex
    on app_security_config (app_id) where (is_deleted = 0);
create index if not exists idx_app_security_config_team_id on app_security_config (team_id);
comment on table app_security_config is '应用安全配置（内容脱敏），与 app 一一对应（Agent/流程应用通用）';
comment on column app_security_config.enabled is '是否启用内容脱敏；启用且存在至少一条规则时生效';
comment on column app_security_config.mask_tool_result is '是否对工具调用结果（含错误信息、流程节点输出）脱敏';
comment on column app_security_config.mask_tool_args is '是否对工具调用参数（调用记录展示、流程节点输入）脱敏';
comment on column app_security_config.mask_model_output is '是否对模型回复文本（对话正文）脱敏';
comment on column app_security_config.rules is '内容脱敏规则列表（工具调用结果/工具调用参数范围共用），JSON 数组文本，元素为 {name,type,pattern,replacement}，type 见 AppSecurityRuleTypes（phone/idCard/email/bankCard/custom），空为 ''[]''';

-- 2026-10-08：模型回复专属脱敏规则（与 rules 相互独立维护）
alter table app_security_config add column if not exists model_output_rules text not null default '[]'::text;
comment on column app_security_config.model_output_rules is '模型回复专属脱敏规则列表，与 rules 相互独立维护，元素同 rules，空为 ''[]''';
