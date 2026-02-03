create table ai_model
(
    id                    serial
        constraint idx_62951_primary
            primary key,
    title                 varchar(50)                                                   not null,
    ai_model_type         varchar(20)                                                   not null,
    ai_provider           varchar(50)                                                   not null,
    name                  varchar(100)                                                  not null,
    deployment_name       varchar(100)                                                  not null,
    endpoint              varchar(100)                                                  not null,
    key                   varchar(100)                                                  not null,
    function_call         boolean                                                       not null,
    context_window_tokens integer                  default 8192                         not null,
    text_output           integer                  default 8192                         not null,
    max_dimension         integer                                                       not null,
    files                 boolean                                                       not null,
    image_output          boolean                                                       not null,
    is_vision             boolean                  default false                        not null,
    is_public             boolean                  default false                        not null,
    counter               integer                  default 0                            not null,
    create_user_id        integer                                                       not null,
    create_time           timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id        integer                                                       not null,
    update_time           timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted            bigint                   default '0'::bigint                  not null
);

comment on table ai_model is 'ai模型';

comment on column ai_model.id is 'id';

comment on column ai_model.title is '自定义名模型名称，便于用户选择';

comment on column ai_model.ai_model_type is '模型功能类型';

comment on column ai_model.ai_provider is '模型供应商';

comment on column ai_model.name is '模型名称,gpt-4o';

comment on column ai_model.deployment_name is '部署名称,Azure需要';

comment on column ai_model.endpoint is '端点';

comment on column ai_model.key is '密钥';

comment on column ai_model.function_call is '支持函数';

comment on column ai_model.context_window_tokens is '上下文最大token数量';

comment on column ai_model.text_output is '最大文本输出token';

comment on column ai_model.max_dimension is '向量的维度';

comment on column ai_model.files is '支持文件上传';

comment on column ai_model.image_output is '支持图片输出';

comment on column ai_model.is_vision is '支持计算机视觉';

comment on column ai_model.is_public is '是否开放给大家使用';

comment on column ai_model.counter is '计数器';

comment on column ai_model.create_user_id is '创建人';

comment on column ai_model.create_time is '创建时间';

comment on column ai_model.update_user_id is '最后修改人';

comment on column ai_model.update_time is '更新时间';

comment on column ai_model.is_deleted is '软删除';

alter table ai_model
    owner to postgres;

create index idx_62951_ai_model_ai_model_type_index
    on ai_model (ai_model_type);

create index idx_62951_ai_model_ai_provider_index
    on ai_model (ai_provider);

create table ai_model_authorization
(
    id             serial
        constraint idx_62966_primary
            primary key,
    ai_model_id    integer                                                       not null,
    team_id        integer                                                       not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table ai_model_authorization is '授权模型给哪些团队使用';

comment on column ai_model_authorization.id is 'id';

comment on column ai_model_authorization.ai_model_id is 'ai模型的id';

comment on column ai_model_authorization.team_id is '授权团队id';

comment on column ai_model_authorization.create_user_id is '创建人';

comment on column ai_model_authorization.create_time is '创建时间';

comment on column ai_model_authorization.update_user_id is '更新人';

comment on column ai_model_authorization.update_time is '更新时间';

comment on column ai_model_authorization.is_deleted is '软删除';

alter table ai_model_authorization
    owner to postgres;

create table ai_model_limit
(
    id              serial
        constraint idx_62976_primary
            primary key,
    model_id        integer                                                       not null,
    user_id         integer                                                       not null,
    rule_type       integer                  default 0                            not null,
    limit_value     integer                                                       not null,
    expiration_time timestamp with time zone default CURRENT_TIMESTAMP            not null,
    create_user_id  integer                  default 0                            not null,
    create_time     timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id  integer                  default 0                            not null,
    update_time     timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted      bigint                   default '0'::bigint                  not null
);

comment on table ai_model_limit is 'ai模型使用量限制，只能用于系统模型';

comment on column ai_model_limit.id is 'id';

comment on column ai_model_limit.model_id is '模型id';

comment on column ai_model_limit.user_id is '用户id';

comment on column ai_model_limit.rule_type is '限制的规则类型,每天/总额/有效期';

comment on column ai_model_limit.limit_value is '限制值';

comment on column ai_model_limit.expiration_time is '过期时间';

comment on column ai_model_limit.create_user_id is '创建人';

comment on column ai_model_limit.create_time is '创建时间';

comment on column ai_model_limit.update_user_id is '更新人';

comment on column ai_model_limit.update_time is '更新时间';

comment on column ai_model_limit.is_deleted is '软删除';

alter table ai_model_limit
    owner to postgres;

create table ai_model_token_audit
(
    id                serial
        constraint idx_62988_primary
            primary key,
    model_id          integer                                                       not null,
    useri_id          integer                                                       not null,
    completion_tokens integer                  default 0                            not null,
    prompt_tokens     integer                  default 0                            not null,
    total_tokens      integer                  default 0                            not null,
    count             integer                  default 0                            not null,
    create_user_id    integer                  default 0                            not null,
    create_time       timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id    integer                  default 0                            not null,
    update_time       timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted        bigint                   default '0'::bigint                  not null
);

comment on table ai_model_token_audit is '统计不同模型的token使用量，该表不是实时刷新的';

comment on column ai_model_token_audit.id is 'id';

comment on column ai_model_token_audit.model_id is '模型id';

comment on column ai_model_token_audit.useri_id is '用户id';

comment on column ai_model_token_audit.completion_tokens is '完成数量';

comment on column ai_model_token_audit.prompt_tokens is '输入数量';

comment on column ai_model_token_audit.total_tokens is '总数量';

comment on column ai_model_token_audit.count is '调用次数';

comment on column ai_model_token_audit.create_user_id is '创建人';

comment on column ai_model_token_audit.create_time is '创建时间';

comment on column ai_model_token_audit.update_user_id is '更新人';

comment on column ai_model_token_audit.update_time is '更新时间';

comment on column ai_model_token_audit.is_deleted is '软删除';

alter table ai_model_token_audit
    owner to postgres;

create table ai_model_useage_log
(
    id                serial
        constraint idx_63002_primary
            primary key,
    model_id          integer                                                       not null,
    useri_id          integer                                                       not null,
    completion_tokens integer                  default 0                            not null,
    prompt_tokens     integer                  default 0                            not null,
    total_tokens      integer                  default 0                            not null,
    channel           varchar(30)              default '-'::character varying       not null,
    create_user_id    integer                  default 0                            not null,
    create_time       timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id    integer                  default 0                            not null,
    update_time       timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted        bigint                   default '0'::bigint                  not null
);

comment on table ai_model_useage_log is '模型使用日志,记录每次请求使用记录';

comment on column ai_model_useage_log.id is 'id';

comment on column ai_model_useage_log.model_id is '模型id';

comment on column ai_model_useage_log.useri_id is '用户id';

comment on column ai_model_useage_log.completion_tokens is '完成数量';

comment on column ai_model_useage_log.prompt_tokens is '输入数量';

comment on column ai_model_useage_log.total_tokens is '总数量';

comment on column ai_model_useage_log.channel is '渠道';

comment on column ai_model_useage_log.create_user_id is '创建人';

comment on column ai_model_useage_log.create_time is '创建时间';

comment on column ai_model_useage_log.update_user_id is '更新人';

comment on column ai_model_useage_log.update_time is '更新时间';

comment on column ai_model_useage_log.is_deleted is '软删除';

alter table ai_model_useage_log
    owner to postgres;

create index idx_63002_ai_model_useage_log_channel_index
    on ai_model_useage_log (channel);

create table app
(
    id             uuid                                                          not null
        constraint idx_63015_primary
            primary key,
    name           varchar(20)                                                   not null,
    description    varchar(255)                                                  not null,
    team_id        integer                                                       not null,
    is_public      boolean                  default false                        not null,
    is_disable     boolean                  default false                        not null,
    classify_id    integer                  default 0                            not null,
    is_foreign     boolean                  default false                        not null,
    is_auth        boolean                  default false                        not null,
    app_type       integer                  default 0                            not null,
    avatar         varchar(255)             default ''::character varying        not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table app is '应用';

comment on column app.id is 'id';

comment on column app.name is '应用名称';

comment on column app.description is '描述';

comment on column app.team_id is '团队id';

comment on column app.is_public is '公开到团队外使用';

comment on column app.is_disable is '禁用';

comment on column app.classify_id is '分类id';

comment on column app.is_foreign is '是否外部应用';

comment on column app.is_auth is '是否开启授权才能使用，只有外部应用可以设置';

comment on column app.app_type is '应用类型，普通应用=0,流程编排=1';

comment on column app.avatar is '头像 objectKey';

comment on column app.create_user_id is '创建人';

comment on column app.create_time is '创建时间';

comment on column app.update_user_id is '更新人';

comment on column app.update_time is '更新时间';

comment on column app.is_deleted is '软删除';

alter table app
    owner to postgres;

create index idx_63015_app_name_index
    on app (name);

create index idx_63015_app_team_id_index
    on app (team_id);

create table app_assistant_chat
(
    id                 uuid                                                             not null
        constraint idx_63032_primary
            primary key,
    title              varchar(100)             default '未命名标题'::character varying not null,
    avatar             varchar(10)              default '?'::character varying          not null,
    prompt             varchar(4000)            default ''::character varying           not null,
    model_id           integer                                                          not null,
    wiki_ids           text                     default '0'::text                       not null,
    plugins            text                     default '[]'::text                      not null,
    execution_settings text                     default '[]'::text                      not null,
    input_tokens       integer                  default 0                               not null,
    out_tokens         integer                  default 0                               not null,
    total_tokens       integer                  default 0                               not null,
    create_user_id     integer                  default 0                               not null,
    create_time        timestamp with time zone default CURRENT_TIMESTAMP               not null,
    update_user_id     integer                  default 0                               not null,
    update_time        timestamp with time zone default timezone('utc'::text, now())    not null,
    is_deleted         bigint                   default '0'::bigint                     not null
);

comment on table app_assistant_chat is 'ai助手表';

comment on column app_assistant_chat.id is 'id';

comment on column app_assistant_chat.title is '对话标题';

comment on column app_assistant_chat.avatar is '头像';

comment on column app_assistant_chat.prompt is '提示词';

comment on column app_assistant_chat.model_id is '对话使用的模型 id';

comment on column app_assistant_chat.wiki_ids is '要使用的知识库id';

comment on column app_assistant_chat.plugins is '要使用的插件';

comment on column app_assistant_chat.execution_settings is '对话影响参数';

comment on column app_assistant_chat.input_tokens is '输入token统计';

comment on column app_assistant_chat.out_tokens is '输出token统计';

comment on column app_assistant_chat.total_tokens is '使用的 token 总数';

comment on column app_assistant_chat.create_user_id is '创建人';

comment on column app_assistant_chat.create_time is '创建时间';

comment on column app_assistant_chat.update_user_id is '更新人';

comment on column app_assistant_chat.update_time is '更新时间';

comment on column app_assistant_chat.is_deleted is '软删除';

alter table app_assistant_chat
    owner to postgres;

create table app_assistant_chat_history
(
    id             bigserial
        constraint idx_63052_primary
            primary key,
    chat_id        uuid                                                          not null,
    completions_id varchar(50)                                                   not null,
    role           varchar(20)                                                   not null,
    content        text                                                          not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table app_assistant_chat_history is '对话历史，不保存实际历史记录';

comment on column app_assistant_chat_history.id is 'id';

comment on column app_assistant_chat_history.chat_id is '对话id';

comment on column app_assistant_chat_history.completions_id is '对话id';

comment on column app_assistant_chat_history.role is '角色';

comment on column app_assistant_chat_history.content is '内容';

comment on column app_assistant_chat_history.create_user_id is '创建人';

comment on column app_assistant_chat_history.create_time is '创建时间';

comment on column app_assistant_chat_history.update_user_id is '更新人';

comment on column app_assistant_chat_history.update_time is '更新时间';

comment on column app_assistant_chat_history.is_deleted is '软删除';

alter table app_assistant_chat_history
    owner to postgres;

create index idx_63052_chat_history_pk_2
    on app_assistant_chat_history (chat_id);

create table app_chatapp
(
    id                 uuid                                                          not null
        constraint idx_63063_primary
            primary key,
    team_id            integer                                                       not null,
    app_id             uuid                                                          not null,
    prompt             varchar(4000)            default ''::character varying        not null,
    model_id           integer                                                       not null,
    wiki_ids           text                     default '0'::text                    not null,
    plugins            text                     default '[]'::text                   not null,
    execution_settings text                     default '[]'::text                   not null,
    create_user_id     integer                  default 0                            not null,
    create_time        timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id     integer                  default 0                            not null,
    update_time        timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted         bigint                   default '0'::bigint                  not null
);

comment on table app_chatapp is '普通应用';

comment on column app_chatapp.id is 'id';

comment on column app_chatapp.team_id is '团队id';

comment on column app_chatapp.app_id is '所属应用id';

comment on column app_chatapp.prompt is '提示词';

comment on column app_chatapp.model_id is '对话使用的模型 id';

comment on column app_chatapp.wiki_ids is '要使用的知识库id';

comment on column app_chatapp.plugins is '要使用的插件';

comment on column app_chatapp.execution_settings is '对话影响参数';

comment on column app_chatapp.create_user_id is '创建人';

comment on column app_chatapp.create_time is '创建时间';

comment on column app_chatapp.update_user_id is '更新人';

comment on column app_chatapp.update_time is '更新时间';

comment on column app_chatapp.is_deleted is '软删除';

alter table app_chatapp
    owner to postgres;

create table app_chatapp_chat
(
    id             uuid                                                             not null
        constraint idx_63077_primary
            primary key,
    app_id         uuid                                                             not null,
    title          varchar(100)             default '未命名标题'::character varying not null,
    input_tokens   integer                  default 0                               not null,
    out_tokens     integer                  default 0                               not null,
    total_tokens   integer                  default 0                               not null,
    user_type      integer                  default 0                               not null,
    create_user_id integer                  default 0                               not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP               not null,
    update_user_id integer                  default 0                               not null,
    update_time    timestamp with time zone default timezone('utc'::text, now())    not null,
    is_deleted     bigint                   default '0'::bigint                     not null
);

comment on table app_chatapp_chat is '普通应用对话表';

comment on column app_chatapp_chat.id is 'id';

comment on column app_chatapp_chat.app_id is 'appid';

comment on column app_chatapp_chat.title is '对话标题';

comment on column app_chatapp_chat.input_tokens is '输入token统计';

comment on column app_chatapp_chat.out_tokens is '输出token统计';

comment on column app_chatapp_chat.total_tokens is '使用的 token 总数';

comment on column app_chatapp_chat.user_type is '用户类型';

comment on column app_chatapp_chat.create_user_id is '创建人';

comment on column app_chatapp_chat.create_time is '创建时间';

comment on column app_chatapp_chat.update_user_id is '更新人';

comment on column app_chatapp_chat.update_time is '更新时间';

comment on column app_chatapp_chat.is_deleted is '软删除';

alter table app_chatapp_chat
    owner to postgres;

create table app_chatapp_chat_history
(
    id             bigserial
        constraint idx_63091_primary
            primary key,
    chat_id        uuid                                                          not null,
    completions_id varchar(50)                                                   not null,
    role           varchar(20)                                                   not null,
    content        text                                                          not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table app_chatapp_chat_history is '对话历史，不保存实际历史记录';

comment on column app_chatapp_chat_history.id is 'id';

comment on column app_chatapp_chat_history.chat_id is '对话id';

comment on column app_chatapp_chat_history.completions_id is '对话id';

comment on column app_chatapp_chat_history.role is '角色';

comment on column app_chatapp_chat_history.content is '内容';

comment on column app_chatapp_chat_history.create_user_id is '创建人';

comment on column app_chatapp_chat_history.create_time is '创建时间';

comment on column app_chatapp_chat_history.update_user_id is '更新人';

comment on column app_chatapp_chat_history.update_time is '更新时间';

comment on column app_chatapp_chat_history.is_deleted is '软删除';

alter table app_chatapp_chat_history
    owner to postgres;

create index idx_63091_chat_history_pk_2
    on app_chatapp_chat_history (chat_id);

create table app_workflow_design
(
    id                    bytea                                                         not null
        constraint idx_63102_primary
            primary key,
    team_id               integer                                                       not null,
    app_id                bytea                                                         not null,
    ui_design             text                     default '{}'::text                   not null,
    function_desgin       text                     default '{}'::text                   not null,
    ui_design_draft       text                     default '{}'::text                   not null,
    function_design_draft text                     default '{}'::text                   not null,
    is_publish            boolean                  default false                        not null,
    create_user_id        integer                  default 0                            not null,
    create_time           timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id        integer                  default 0                            not null,
    update_time           timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted            bigint                   default '0'::bigint                  not null
);

comment on table app_workflow_design is '流程设计实例表';

comment on column app_workflow_design.id is 'id';

comment on column app_workflow_design.team_id is '团队id';

comment on column app_workflow_design.app_id is '应用id';

comment on column app_workflow_design.ui_design is 'ui设计，存储的是发布版本';

comment on column app_workflow_design.function_desgin is '功能设计，存储的是发布版本';

comment on column app_workflow_design.ui_design_draft is 'ui设计草稿';

comment on column app_workflow_design.function_design_draft is '功能设计草稿';

comment on column app_workflow_design.is_publish is '是否发布';

comment on column app_workflow_design.create_user_id is '创建人';

comment on column app_workflow_design.create_time is '创建时间';

comment on column app_workflow_design.update_user_id is '更新人';

comment on column app_workflow_design.update_time is '更新时间';

comment on column app_workflow_design.is_deleted is '软删除';

alter table app_workflow_design
    owner to postgres;

create table app_workflow_history
(
    id                 bytea                                                         not null
        constraint idx_63117_primary
            primary key,
    team_id            integer                                                       not null,
    app_id             bytea                                                         not null,
    workflow_design_id bytea                                                         not null,
    state              integer                  default 0                            not null,
    system_paramters   text                                                          not null,
    run_paramters      text                                                          not null,
    data               text                     default '{}'::text                   not null,
    create_user_id     integer                  default 0                            not null,
    create_time        timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id     integer                  default 0                            not null,
    update_time        timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted         bigint                   default '0'::bigint                  not null
);

comment on table app_workflow_history is '流程执行记录';

comment on column app_workflow_history.id is 'varbinary(16)';

comment on column app_workflow_history.team_id is '团队id';

comment on column app_workflow_history.app_id is '应用id';

comment on column app_workflow_history.workflow_design_id is '流程设计id';

comment on column app_workflow_history.state is '工作状态';

comment on column app_workflow_history.system_paramters is '系统参数';

comment on column app_workflow_history.run_paramters is '运行参数';

comment on column app_workflow_history.data is '数据内容';

comment on column app_workflow_history.create_user_id is '创建人';

comment on column app_workflow_history.create_time is '创建时间';

comment on column app_workflow_history.update_user_id is '更新人';

comment on column app_workflow_history.update_time is '更新时间';

comment on column app_workflow_history.is_deleted is '软删除';

alter table app_workflow_history
    owner to postgres;

create table classify
(
    id             serial
        constraint idx_63130_primary
            primary key,
    type           varchar(10)                                                   not null,
    name           varchar(20)                                                   not null,
    description    varchar(255)             default ''::character varying        not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table classify is '分类';

comment on column classify.id is 'id';

comment on column classify.type is '分类类型';

comment on column classify.name is '分类名称';

comment on column classify.description is '分类描述';

comment on column classify.create_user_id is '创建人';

comment on column classify.create_time is '创建时间';

comment on column classify.update_user_id is '更新人';

comment on column classify.update_time is '更新时间';

comment on column classify.is_deleted is '软删除';

alter table classify
    owner to postgres;

create index idx_63130_classify_name_index
    on classify (name);

create index idx_63130_classify_type_index
    on classify (type);

create table external_app
(
    id             uuid                                                          not null
        constraint idx_63140_primary
            primary key,
    team_id        integer                                                       not null,
    name           varchar(20)                                                   not null,
    description    varchar(255)                                                  not null,
    avatar         varchar(255)             default ''::character varying        not null,
    key            varchar(255)                                                  not null,
    is_dsiable     boolean                  default false                        not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table external_app is '系统接入';

comment on column external_app.id is 'app_id';

comment on column external_app.team_id is '团队id';

comment on column external_app.name is '应用名称';

comment on column external_app.description is '描述';

comment on column external_app.avatar is '头像objectKey';

comment on column external_app.key is '应用密钥';

comment on column external_app.is_dsiable is '禁用';

comment on column external_app.create_user_id is '创建人';

comment on column external_app.create_time is '创建时间';

comment on column external_app.update_user_id is '更新人';

comment on column external_app.update_time is '更新时间';

comment on column external_app.is_deleted is '软删除';

alter table external_app
    owner to postgres;

create table external_user
(
    id              serial
        constraint idx_63153_primary
            primary key,
    external_app_id integer                                                       not null,
    user_uid        varchar(50)                                                   not null,
    is_deleted      bigint                   default '0'::bigint                  not null,
    create_user_id  integer                                                       not null,
    create_time     timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id  integer                                                       not null,
    update_time     timestamp with time zone default timezone('utc'::text, now()) not null
);

comment on table external_user is '外部系统的用户';

comment on column external_user.id is '用户ID';

comment on column external_user.external_app_id is '所属的外部应用id';

comment on column external_user.user_uid is '外部用户标识';

comment on column external_user.is_deleted is '软删除';

comment on column external_user.create_user_id is '创建人';

comment on column external_user.create_time is '创建时间';

comment on column external_user.update_user_id is '最后修改人';

comment on column external_user.update_time is '更新时间';

alter table external_user
    owner to postgres;

create table file
(
    id             serial
        constraint idx_63161_primary
            primary key,
    object_key     varchar(1024)                                                 not null,
    file_extension varchar(10)              default ''::character varying        not null,
    file_md5       varchar(50)                                                   not null,
    file_size      integer                                                       not null,
    content_type   varchar(50)                                                   not null,
    is_uploaded    boolean                                                       not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table file is '文件列表';

comment on column file.id is 'id';

comment on column file.object_key is '文件路径';

comment on column file.file_extension is '文件扩展名';

comment on column file.file_md5 is 'md5';

comment on column file.file_size is '文件大小';

comment on column file.content_type is '文件类型';

comment on column file.is_uploaded is '是否已经上传完毕';

comment on column file.create_user_id is '创建人';

comment on column file.create_time is '创建时间';

comment on column file.update_user_id is '最后修改人';

comment on column file.update_time is '更新时间';

comment on column file.is_deleted is '软删除';

alter table file
    owner to postgres;

create index idx_63161_file_file_md5_index
    on file (file_md5);

create index idx_63161_file_object_key_index
    on file (object_key);

create table oauth_connection
(
    id             uuid                                                          not null
        constraint idx_63171_primary
            primary key,
    name           varchar(50)                                                   not null,
    provider       varchar(20)                                                   not null,
    key            varchar(100)                                                  not null,
    secret         varchar(100)                                                  not null,
    icon_url       varchar(1000)                                                 not null,
    authorize_url  varchar(1000)                                                 not null,
    well_known     varchar(1000)                                                 not null,
    is_deleted     bigint                   default '0'::bigint                  not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    create_user_id integer                  default 0                            not null,
    update_user_id integer                  default 0                            not null
);

comment on table oauth_connection is 'oauth2.0系统';

comment on column oauth_connection.id is 'id';

comment on column oauth_connection.name is '认证名称';

comment on column oauth_connection.provider is '提供商';

comment on column oauth_connection.key is '应用key';

comment on column oauth_connection.secret is '密钥';

comment on column oauth_connection.icon_url is '图标地址';

comment on column oauth_connection.authorize_url is '登录跳转地址';

comment on column oauth_connection.well_known is '发现端口';

comment on column oauth_connection.is_deleted is '软删除';

comment on column oauth_connection.create_time is '创建时间';

comment on column oauth_connection.update_time is '更新时间';

comment on column oauth_connection.create_user_id is '创建人';

comment on column oauth_connection.update_user_id is '更新人';

alter table oauth_connection
    owner to postgres;

create table plugin
(
    id             serial
        constraint idx_63182_primary
            primary key,
    team_id        integer                  default 0                            not null,
    plugin_id      integer                                                       not null,
    plugin_name    varchar(50)                                                   not null,
    title          varchar(50)                                                   not null,
    description    varchar(255)             default ''::character varying        not null,
    type           integer                                                       not null,
    classify_id    integer                  default 0                            not null,
    is_public      boolean                  default false                        not null,
    counter        integer                  default 0                            not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table plugin is '插件';

comment on column plugin.id is 'id';

comment on column plugin.team_id is '某个团队创建的自定义插件';

comment on column plugin.plugin_id is '对应的实际插件的id，不同类型的插件表不一样';

comment on column plugin.plugin_name is '插件名称';

comment on column plugin.title is '插件标题';

comment on column plugin.description is '注释';

comment on column plugin.type is 'mcp|openapi|native|tool';

comment on column plugin.classify_id is '分类id';

comment on column plugin.is_public is '公开访问';

comment on column plugin.counter is '计数器';

comment on column plugin.create_user_id is '创建人';

comment on column plugin.create_time is '创建时间';

comment on column plugin.update_user_id is '最后修改人';

comment on column plugin.update_time is '更新时间';

comment on column plugin.is_deleted is '软删除';

alter table plugin
    owner to postgres;

create index idx_63182_plugin_plugin_name_index
    on plugin (plugin_name);

create index idx_63182_plugin_title_index
    on plugin (title);

create table plugin_authorization
(
    id             serial
        constraint idx_63195_primary
            primary key,
    plugin_id      integer                                                       not null,
    team_id        integer                                                       not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table plugin_authorization is '授权私有插件给哪些团队使用';

comment on column plugin_authorization.id is 'id';

comment on column plugin_authorization.plugin_id is '私有插件的id';

comment on column plugin_authorization.team_id is '授权团队id';

comment on column plugin_authorization.create_user_id is '创建人';

comment on column plugin_authorization.create_time is '创建时间';

comment on column plugin_authorization.update_user_id is '更新人';

comment on column plugin_authorization.update_time is '更新时间';

comment on column plugin_authorization.is_deleted is '软删除';

alter table plugin_authorization
    owner to postgres;

create table plugin_custom
(
    id                serial
        constraint idx_63205_primary
            primary key,
    server            varchar(255)                                                  not null,
    headers           text                                                          not null,
    queries           text                                                          not null,
    type              integer                                                       not null,
    openapi_file_id   integer                                                       not null,
    openapi_file_name varchar(255)                                                  not null,
    create_user_id    integer                                                       not null,
    create_time       timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id    integer                                                       not null,
    update_time       timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted        bigint                   default '0'::bigint                  not null
);

comment on table plugin_custom is '自定义插件';

comment on column plugin_custom.id is 'id';

comment on column plugin_custom.server is '服务器地址';

comment on column plugin_custom.headers is '头部';

comment on column plugin_custom.queries is 'query参数';

comment on column plugin_custom.type is 'mcp|openapi';

comment on column plugin_custom.openapi_file_id is '文件id';

comment on column plugin_custom.openapi_file_name is '文件名称';

comment on column plugin_custom.create_user_id is '创建人';

comment on column plugin_custom.create_time is '创建时间';

comment on column plugin_custom.update_user_id is '最后修改人';

comment on column plugin_custom.update_time is '更新时间';

comment on column plugin_custom.is_deleted is '软删除';

alter table plugin_custom
    owner to postgres;

create table plugin_function
(
    id               serial
        constraint idx_63215_primary
            primary key,
    plugin_custom_id integer                                                       not null,
    name             varchar(255)                                                  not null,
    summary          varchar(1000)            default ''::character varying        not null,
    path             varchar(255)             default ''::character varying        not null,
    create_user_id   integer                                                       not null,
    create_time      timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id   integer                                                       not null,
    update_time      timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted       bigint                   default '0'::bigint                  not null
);

comment on table plugin_function is '插件函数';

comment on column plugin_function.id is 'id';

comment on column plugin_function.plugin_custom_id is 'plugin_custom_id';

comment on column plugin_function.name is '函数名称';

comment on column plugin_function.summary is '描述';

comment on column plugin_function.path is 'api路径';

comment on column plugin_function.create_user_id is '创建人';

comment on column plugin_function.create_time is '创建时间';

comment on column plugin_function.update_user_id is '最后修改人';

comment on column plugin_function.update_time is '更新时间';

comment on column plugin_function.is_deleted is '软删除';

alter table plugin_function
    owner to postgres;

create table plugin_limit
(
    id              serial
        constraint idx_63227_primary
            primary key,
    plugin_id       integer                                                       not null,
    user_id         integer                                                       not null,
    rule_type       integer                  default 0                            not null,
    limit_value     integer                                                       not null,
    expiration_time timestamp with time zone default CURRENT_TIMESTAMP            not null,
    create_user_id  integer                  default 0                            not null,
    create_time     timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id  integer                  default 0                            not null,
    update_time     timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted      bigint                   default '0'::bigint                  not null
);

comment on table plugin_limit is '插件使用量限制';

comment on column plugin_limit.id is 'id';

comment on column plugin_limit.plugin_id is '插件id';

comment on column plugin_limit.user_id is '用户id';

comment on column plugin_limit.rule_type is '限制的规则类型,每天/总额/有效期';

comment on column plugin_limit.limit_value is '限制值';

comment on column plugin_limit.expiration_time is '过期时间';

comment on column plugin_limit.create_user_id is '创建人';

comment on column plugin_limit.create_time is '创建时间';

comment on column plugin_limit.update_user_id is '更新人';

comment on column plugin_limit.update_time is '更新时间';

comment on column plugin_limit.is_deleted is '软删除';

alter table plugin_limit
    owner to postgres;

create table plugin_log
(
    id             serial
        constraint idx_63239_primary
            primary key,
    plugin_id      integer                                                       not null,
    useri_id       integer                                                       not null,
    channel        varchar(10)              default '0'::character varying       not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table plugin_log is '插件使用日志';

comment on column plugin_log.id is 'id';

comment on column plugin_log.plugin_id is '插件id';

comment on column plugin_log.useri_id is '用户id';

comment on column plugin_log.channel is '渠道';

comment on column plugin_log.create_user_id is '创建人';

comment on column plugin_log.create_time is '创建时间';

comment on column plugin_log.update_user_id is '更新人';

comment on column plugin_log.update_time is '更新时间';

comment on column plugin_log.is_deleted is '软删除';

alter table plugin_log
    owner to postgres;

create index idx_63239_plugin_log_channel_index
    on plugin_log (channel);

create table plugin_native
(
    id                       serial
        constraint idx_63250_primary
            primary key,
    template_plugin_classify varchar(20)                                                   not null,
    template_plugin_key      varchar(50)                                                   not null,
    config                   text                     default '{}'::text                   not null,
    create_user_id           integer                                                       not null,
    create_time              timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id           integer                                                       not null,
    update_time              timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted               bigint                   default '0'::bigint                  not null
);

comment on table plugin_native is '内置插件';

comment on column plugin_native.id is 'id';

comment on column plugin_native.template_plugin_classify is '模板分类';

comment on column plugin_native.template_plugin_key is '对应的内置插件key';

comment on column plugin_native.config is '配置参数';

comment on column plugin_native.create_user_id is '创建人';

comment on column plugin_native.create_time is '创建时间';

comment on column plugin_native.update_user_id is '最后修改人';

comment on column plugin_native.update_time is '更新时间';

comment on column plugin_native.is_deleted is '软删除';

alter table plugin_native
    owner to postgres;

create table plugin_tool
(
    id                       serial
        constraint idx_63261_primary
            primary key,
    template_plugin_classify varchar(20)                                                   not null,
    template_plugin_key      varchar(50)                                                   not null,
    create_user_id           integer                                                       not null,
    create_time              timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id           integer                                                       not null,
    update_time              timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted               bigint                   default '0'::bigint                  not null
);

comment on table plugin_tool is '内置插件';

comment on column plugin_tool.id is 'id';

comment on column plugin_tool.template_plugin_classify is '模板分类';

comment on column plugin_tool.template_plugin_key is '对应的内置插件key';

comment on column plugin_tool.create_user_id is '创建人';

comment on column plugin_tool.create_time is '创建时间';

comment on column plugin_tool.update_user_id is '最后修改人';

comment on column plugin_tool.update_time is '更新时间';

comment on column plugin_tool.is_deleted is '软删除';

alter table plugin_tool
    owner to postgres;

create table prompt
(
    id              serial
        constraint idx_63269_primary
            primary key,
    name            varchar(20)                                                   not null,
    description     varchar(255)                                                  not null,
    content         text                                                          not null,
    prompt_class_id integer                                                       not null,
    is_public       boolean                  default false                        not null,
    counter         integer                  default 0                            not null,
    create_user_id  integer                  default 0                            not null,
    create_time     timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id  integer                  default 0                            not null,
    update_time     timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted      bigint                   default '0'::bigint                  not null
);

comment on table prompt is '提示词';

comment on column prompt.id is 'id';

comment on column prompt.name is '名称';

comment on column prompt.description is '描述';

comment on column prompt.content is '提示词内容';

comment on column prompt.prompt_class_id is '分类id';

comment on column prompt.is_public is '是否公开';

comment on column prompt.counter is '计数器';

comment on column prompt.create_user_id is '创建人';

comment on column prompt.create_time is '创建时间';

comment on column prompt.update_user_id is '更新人';

comment on column prompt.update_time is '更新时间';

comment on column prompt.is_deleted is '软删除';

alter table prompt
    owner to postgres;

create table setting
(
    id             serial
        constraint idx_63283_primary
            primary key,
    key            varchar(50)                                                   not null,
    value          varchar(255)                                                  not null,
    description    varchar(255)             default ''::character varying        not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table setting is '系统设置';

comment on column setting.id is 'id';

comment on column setting.key is '配置名称';

comment on column setting.value is '配置值,json';

comment on column setting.description is '描述';

comment on column setting.create_user_id is '创建人';

comment on column setting.create_time is '创建时间';

comment on column setting.update_user_id is '最后修改人';

comment on column setting.update_time is '更新时间';

comment on column setting.is_deleted is '软删除';

alter table setting
    owner to postgres;

create unique index idx_63283_setting_key_is_deleted_uindex
    on setting (key, is_deleted);

create table team
(
    id             serial
        constraint idx_63294_primary
            primary key,
    name           varchar(20)                                                   not null,
    description    varchar(255)             default ''::character varying        not null,
    avatar         varchar(255)             default ''::character varying        not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table team is '团队';

comment on column team.id is 'id';

comment on column team.name is '团队名称';

comment on column team.description is '团队描述';

comment on column team.avatar is '团队头像,objectkey';

comment on column team.create_user_id is '创建人';

comment on column team.create_time is '创建时间';

comment on column team.update_user_id is '更新人';

comment on column team.update_time is '更新时间';

comment on column team.is_deleted is '软删除';

alter table team
    owner to postgres;

create table team_user
(
    id             serial
        constraint idx_63308_primary
            primary key,
    team_id        integer                                                       not null,
    user_id        integer                                                       not null,
    role           integer                  default 0                            not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table team_user is '团队成员';

comment on column team_user.id is 'id';

comment on column team_user.team_id is '团队id';

comment on column team_user.user_id is '用户id';

comment on column team_user.role is '角色,普通用户=0,协作者=1,管理员=2,所有者=3';

comment on column team_user.create_user_id is '创建人';

comment on column team_user.create_time is '创建时间';

comment on column team_user.update_user_id is '更新人';

comment on column team_user.update_time is '更新时间';

comment on column team_user.is_deleted is '软删除';

alter table team_user
    owner to postgres;

create table "user"
(
    id             serial
        constraint idx_63319_primary
            primary key,
    user_name      varchar(50)                                                   not null,
    email          varchar(255)                                                  not null,
    password       varchar(255)                                                  not null,
    nick_name      varchar(50)                                                   not null,
    avatar_path    varchar(255)             default ''::character varying        not null,
    phone          varchar(20)                                                   not null,
    is_disable     boolean                                                       not null,
    is_admin       boolean                                                       not null,
    password_salt  varchar(255)                                                  not null,
    is_deleted     bigint                   default '0'::bigint                  not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null
);

comment on table "user" is '用户';

comment on column "user".id is '用户ID';

comment on column "user".user_name is '用户名';

comment on column "user".email is '邮箱';

comment on column "user".password is '密码';

comment on column "user".nick_name is '昵称';

comment on column "user".avatar_path is '头像路径';

comment on column "user".phone is '手机号';

comment on column "user".is_disable is '禁用';

comment on column "user".is_admin is '是否管理员';

comment on column "user".password_salt is '计算密码值的salt';

comment on column "user".is_deleted is '软删除';

comment on column "user".create_user_id is '创建人';

comment on column "user".create_time is '创建时间';

comment on column "user".update_user_id is '最后修改人';

comment on column "user".update_time is '更新时间';

alter table "user"
    owner to postgres;

create unique index idx_63319_users_email_is_deleted_uindex
    on "user" (email, is_deleted);

create unique index idx_63319_users_phone_is_deleted_uindex
    on "user" (phone, is_deleted);

create unique index idx_63319_users_user_name_is_deleted_uindex
    on "user" (user_name, is_deleted);

create index idx_63319_idx_users_email
    on "user" (email);

create index idx_63319_idx_users_phone
    on "user" (phone);

create index idx_63319_idx_users_user_name
    on "user" (user_name);

create table user_oauth
(
    id             serial
        constraint idx_63330_primary
            primary key,
    user_id        integer                                                       not null,
    provider_id    uuid                                                          not null,
    sub            varchar(50)                                                   not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table user_oauth is 'oauth2.0对接';

comment on column user_oauth.id is 'id';

comment on column user_oauth.user_id is '用户id';

comment on column user_oauth.provider_id is '供应商id,对应oauth_connection表';

comment on column user_oauth.sub is '用户oauth对应的唯一id';

comment on column user_oauth.create_user_id is '创建人';

comment on column user_oauth.create_time is '创建时间';

comment on column user_oauth.update_user_id is '最后修改人';

comment on column user_oauth.update_time is '更新时间';

comment on column user_oauth.is_deleted is '软删除';

alter table user_oauth
    owner to postgres;

create unique index idx_63330_user_oauth_provider_id_sub_is_deleted_uindex
    on user_oauth (provider_id, sub, is_deleted);

create table wiki
(
    id                   serial
        constraint idx_63338_primary
            primary key,
    name                 varchar(20)                                                   not null,
    description          varchar(255)                                                  not null,
    is_public            boolean                                                       not null,
    counter              integer                  default 0                            not null,
    is_lock              boolean                                                       not null,
    embedding_model_id   integer                                                       not null,
    embedding_dimensions integer                  default 1024                         not null,
    avatar               varchar(255)             default ''::character varying        not null,
    team_id              integer                  default 0                            not null,
    create_user_id       integer                                                       not null,
    create_time          timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id       integer                                                       not null,
    update_time          timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted           bigint                   default '0'::bigint                  not null
);

comment on table wiki is '知识库';

comment on column wiki.id is 'id';

comment on column wiki.name is '知识库名称';

comment on column wiki.description is '知识库描述';

comment on column wiki.is_public is '是否公开，公开后所有人都可以使用，但是不能进去操作';

comment on column wiki.counter is '计数器';

comment on column wiki.is_lock is '是否已被锁定配置';

comment on column wiki.embedding_model_id is '向量化模型的id';

comment on column wiki.embedding_dimensions is '知识库向量维度';

comment on column wiki.avatar is '团队头像';

comment on column wiki.team_id is '团队id，不填则是个人知识库';

comment on column wiki.create_user_id is '创建人';

comment on column wiki.create_time is '创建时间';

comment on column wiki.update_user_id is '最后修改人';

comment on column wiki.update_time is '更新时间';

comment on column wiki.is_deleted is '软删除';

alter table wiki
    owner to postgres;

create table wiki_document
(
    id             serial
        constraint idx_63352_primary
            primary key,
    wiki_id        integer                                                       not null,
    file_id        integer                                                       not null,
    object_key     varchar(100)                                                  not null,
    file_name      varchar(1024)                                                 not null,
    file_type      varchar(10)                                                   not null,
    is_embedding   boolean                  default false                        not null,
    version_no     bigint                   default '0'::bigint                  not null,
    slice_config   text                     default '{}'::text                   not null,
    is_update      boolean                  default false                        not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table wiki_document is '知识库文档';

comment on column wiki_document.id is 'id';

comment on column wiki_document.wiki_id is '知识库id';

comment on column wiki_document.file_id is '文件id';

comment on column wiki_document.object_key is '文件路径';

comment on column wiki_document.file_name is '文档名称';

comment on column wiki_document.file_type is '文件扩展名称，如.md';

comment on column wiki_document.is_embedding is '是否已经向量化';

comment on column wiki_document.version_no is '版本号，可与向量元数据对比，确认最新文档版本号是否一致';

comment on column wiki_document.slice_config is '切割配置';

comment on column wiki_document.is_update is '是否有更新，需要重新进行向量化';

comment on column wiki_document.create_user_id is '创建人';

comment on column wiki_document.create_time is '创建时间';

comment on column wiki_document.update_user_id is '最后修改人';

comment on column wiki_document.update_time is '更新时间';

comment on column wiki_document.is_deleted is '软删除';

alter table wiki_document
    owner to postgres;

create index idx_63352_wiki_document_object_key_index
    on wiki_document (object_key);

create table wiki_document_chunk_content_preview
(
    id             bigserial
        constraint idx_63366_primary
            primary key,
    wiki_id        integer                                                       not null,
    document_id    integer                                                       not null,
    slice_content  text                                                          not null,
    slice_order    integer                                                       not null,
    slice_length   integer                                                       not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table wiki_document_chunk_content_preview is '文档切片预览';

comment on column wiki_document_chunk_content_preview.id is '切片唯一ID（slice_id）';

comment on column wiki_document_chunk_content_preview.wiki_id is '关联知识库标识（冗余字段）';

comment on column wiki_document_chunk_content_preview.document_id is '关联文档唯一标识';

comment on column wiki_document_chunk_content_preview.slice_content is '原始切片内容';

comment on column wiki_document_chunk_content_preview.slice_order is '切片在文档中的顺序';

comment on column wiki_document_chunk_content_preview.slice_length is '切片字符长度';

comment on column wiki_document_chunk_content_preview.create_user_id is '创建人';

comment on column wiki_document_chunk_content_preview.create_time is '创建时间';

comment on column wiki_document_chunk_content_preview.update_user_id is '更新人';

comment on column wiki_document_chunk_content_preview.update_time is '更新时间';

comment on column wiki_document_chunk_content_preview.is_deleted is '软删除';

alter table wiki_document_chunk_content_preview
    owner to postgres;

create index idx_63366_idx_wiki_slice
    on wiki_document_chunk_content_preview (wiki_id);

create index idx_63366_idx_doc_slice
    on wiki_document_chunk_content_preview (document_id, id);

create table wiki_document_chunk_embedding
(
    id               uuid                                                          not null
        constraint idx_63377_primary
            primary key,
    wiki_id          integer                                                       not null,
    document_id      integer                                                       not null,
    chunk_id         uuid                                                          not null,
    metadata_type    integer                                                       not null,
    metadata_content text                                                          not null,
    is_embedding     boolean                  default false                        not null,
    create_user_id   integer                  default 0                            not null,
    create_time      timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id   integer                  default 0                            not null,
    update_time      timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted       bigint                   default '0'::bigint                  not null
);

comment on table wiki_document_chunk_embedding is '切片向量化内容';

comment on column wiki_document_chunk_embedding.id is 'id';

comment on column wiki_document_chunk_embedding.wiki_id is '知识库id';

comment on column wiki_document_chunk_embedding.document_id is '关联文档唯一标识（冗余字段）';

comment on column wiki_document_chunk_embedding.chunk_id is '源id，关联自身';

comment on column wiki_document_chunk_embedding.metadata_type is '元数据类型：0=原文，1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段';

comment on column wiki_document_chunk_embedding.metadata_content is '提问/提纲/摘要内容';

comment on column wiki_document_chunk_embedding.is_embedding is '是否被向量化';

comment on column wiki_document_chunk_embedding.create_user_id is '创建人';

comment on column wiki_document_chunk_embedding.create_time is '创建时间';

comment on column wiki_document_chunk_embedding.update_user_id is '更新人';

comment on column wiki_document_chunk_embedding.update_time is '更新时间';

comment on column wiki_document_chunk_embedding.is_deleted is '软删除';

alter table wiki_document_chunk_embedding
    owner to postgres;

create index idx_63377_idx_deriv_type
    on wiki_document_chunk_embedding (metadata_type);

create index idx_63377_idx_doc_deriv
    on wiki_document_chunk_embedding (document_id, metadata_type);

create index idx_63377_idx_slice_deriv
    on wiki_document_chunk_embedding (id);

create table wiki_document_chunk_metadata_preview
(
    id               bigserial
        constraint idx_63389_primary
            primary key,
    wiki_id          integer                                                       not null,
    document_id      integer                                                       not null,
    chunk_id         bigint                                                        not null,
    metadata_type    integer                                                       not null,
    metadata_content text                                                          not null,
    create_user_id   integer                  default 0                            not null,
    create_time      timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id   integer                  default 0                            not null,
    update_time      timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted       bigint                   default '0'::bigint                  not null
);

comment on table wiki_document_chunk_metadata_preview is '切片元数据内容表（提问/提纲/摘要）';

comment on column wiki_document_chunk_metadata_preview.id is 'id';

comment on column wiki_document_chunk_metadata_preview.wiki_id is '知识库id';

comment on column wiki_document_chunk_metadata_preview.document_id is '关联文档唯一标识（冗余字段）';

comment on column wiki_document_chunk_metadata_preview.chunk_id is '关联切片ID（表A主键）';

comment on column wiki_document_chunk_metadata_preview.metadata_type is '元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段';

comment on column wiki_document_chunk_metadata_preview.metadata_content is '提问/提纲/摘要内容';

comment on column wiki_document_chunk_metadata_preview.create_user_id is '创建人';

comment on column wiki_document_chunk_metadata_preview.create_time is '创建时间';

comment on column wiki_document_chunk_metadata_preview.update_user_id is '更新人';

comment on column wiki_document_chunk_metadata_preview.update_time is '更新时间';

comment on column wiki_document_chunk_metadata_preview.is_deleted is '软删除';

alter table wiki_document_chunk_metadata_preview
    owner to postgres;

create index idx_63389_idx_slice_deriv
    on wiki_document_chunk_metadata_preview (chunk_id, id);

create index idx_63389_idx_deriv_type
    on wiki_document_chunk_metadata_preview (metadata_type);

create index idx_63389_idx_doc_deriv
    on wiki_document_chunk_metadata_preview (document_id, metadata_type);

create table wiki_plugin_config
(
    id             serial
        constraint idx_63401_primary
            primary key,
    wiki_id        integer                                                       not null,
    title          varchar(20)                                                   not null,
    config         text                     default '{}'::text                   not null,
    plugin_type    varchar(10)                                                   not null,
    work_message   varchar(1000)            default ''::character varying        not null,
    work_state     integer                  default 0                            not null,
    create_user_id integer                  default 0                            not null,
    create_time    timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id integer                  default 0                            not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table wiki_plugin_config is '知识库插件配置';

comment on column wiki_plugin_config.id is 'id';

comment on column wiki_plugin_config.wiki_id is '知识库id';

comment on column wiki_plugin_config.title is '插件标题';

comment on column wiki_plugin_config.config is '配置';

comment on column wiki_plugin_config.plugin_type is '插件类型';

comment on column wiki_plugin_config.work_message is '运行信息';

comment on column wiki_plugin_config.work_state is '状态';

comment on column wiki_plugin_config.create_user_id is '创建人';

comment on column wiki_plugin_config.create_time is '创建时间';

comment on column wiki_plugin_config.update_user_id is '更新人';

comment on column wiki_plugin_config.update_time is '更新时间';

comment on column wiki_plugin_config.is_deleted is '软删除';

alter table wiki_plugin_config
    owner to postgres;

create table wiki_plugin_config_document
(
    id               serial
        constraint idx_63416_primary
            primary key,
    wiki_id          integer                                                       not null,
    config_id        integer                                                       not null,
    wiki_document_id integer                  default 0                            not null,
    relevance_key    varchar(1000)                                                 not null,
    relevance_value  varchar(1000)            default ''::character varying        not null,
    create_user_id   integer                  default 0                            not null,
    create_time      timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id   integer                  default 0                            not null,
    update_time      timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted       bigint                   default '0'::bigint                  not null
);

comment on table wiki_plugin_config_document is '知识库文档关联任务，这里的任务都是成功的';

comment on column wiki_plugin_config_document.id is 'id';

comment on column wiki_plugin_config_document.wiki_id is '知识库id';

comment on column wiki_plugin_config_document.config_id is '爬虫id';

comment on column wiki_plugin_config_document.wiki_document_id is '文档id';

comment on column wiki_plugin_config_document.relevance_key is '关联对象';

comment on column wiki_plugin_config_document.relevance_value is '关联值';

comment on column wiki_plugin_config_document.create_user_id is '创建人';

comment on column wiki_plugin_config_document.create_time is '创建时间';

comment on column wiki_plugin_config_document.update_user_id is '更新人';

comment on column wiki_plugin_config_document.update_time is '更新时间';

comment on column wiki_plugin_config_document.is_deleted is '软删除';

alter table wiki_plugin_config_document
    owner to postgres;

create table wiki_plugin_config_document_state
(
    id              serial
        constraint idx_63430_primary
            primary key,
    wiki_id         integer                                                       not null,
    config_id       integer                                                       not null,
    relevance_key   varchar(1000)                                                 not null,
    relevance_value varchar(1000)            default ''::character varying        not null,
    message         varchar(1000)            default ''::character varying        not null,
    state           integer                  default 0                            not null,
    create_user_id  integer                  default 0                            not null,
    create_time     timestamp with time zone default CURRENT_TIMESTAMP            not null,
    update_user_id  integer                  default 0                            not null,
    update_time     timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted      bigint                   default '0'::bigint                  not null
);

comment on table wiki_plugin_config_document_state is '知识库文档关联任务';

comment on column wiki_plugin_config_document_state.id is 'id';

comment on column wiki_plugin_config_document_state.wiki_id is '知识库id';

comment on column wiki_plugin_config_document_state.config_id is '爬虫id';

comment on column wiki_plugin_config_document_state.relevance_key is '关联对象';

comment on column wiki_plugin_config_document_state.relevance_value is '关联值';

comment on column wiki_plugin_config_document_state.message is '信息';

comment on column wiki_plugin_config_document_state.state is '状态';

comment on column wiki_plugin_config_document_state.create_user_id is '创建人';

comment on column wiki_plugin_config_document_state.create_time is '创建时间';

comment on column wiki_plugin_config_document_state.update_user_id is '更新人';

comment on column wiki_plugin_config_document_state.update_time is '更新时间';

comment on column wiki_plugin_config_document_state.is_deleted is '软删除';

alter table wiki_plugin_config_document_state
    owner to postgres;

create table worker_task
(
    id             uuid                                                          not null
        constraint idx_63444_primary
            primary key,
    bind_type      varchar(20)                                                   not null,
    bind_id        integer                                                       not null,
    state          integer                                                       not null,
    message        text                                                          not null,
    data           text                     default '{}'::text                   not null,
    create_user_id integer                                                       not null,
    create_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    update_user_id integer                                                       not null,
    update_time    timestamp with time zone default timezone('utc'::text, now()) not null,
    is_deleted     bigint                   default '0'::bigint                  not null
);

comment on table worker_task is '工作任务';

comment on column worker_task.id is 'id';

comment on column worker_task.bind_type is '关联类型';

comment on column worker_task.bind_id is '关联对象id';

comment on column worker_task.state is '任务状态，不同的任务类型状态值规则不一样';

comment on column worker_task.message is '消息、错误信息';

comment on column worker_task.data is '自定义数据,json格式';

comment on column worker_task.create_user_id is '创建人';

comment on column worker_task.create_time is '创建时间';

comment on column worker_task.update_user_id is '最后修改人';

comment on column worker_task.update_time is '更新时间';

comment on column worker_task.is_deleted is '软删除';

alter table worker_task
    owner to postgres;

