create table moai.ai_model
(
    id                    int auto_increment comment 'id'
        primary key,
    title                 varchar(50)                        not null comment '自定义名模型名称，便于用户选择',
    ai_model_type         varchar(20)                        not null comment '模型功能类型',
    ai_provider           varchar(50)                        not null comment '模型供应商',
    name                  varchar(100)                       not null comment '模型名称,gpt-4o',
    deployment_name       varchar(100)                       not null comment '部署名称,Azure需要',
    endpoint              varchar(100)                       not null comment '端点',
    `key`                 varchar(100)                       not null comment '密钥',
    function_call         tinyint(1)                         not null comment '支持函数',
    context_window_tokens int        default 8192            not null comment '上下文最大token数量',
    text_output           int        default 8192            not null comment '最大文本输出token',
    max_dimension         int                                not null comment '向量的维度',
    files                 tinyint(1)                         not null comment '支持文件上传',
    image_output          tinyint(1)                         not null comment '支持图片输出',
    is_vision             tinyint(1) default 0               not null comment '支持计算机视觉',
    is_public             tinyint(1) default 0               not null comment '是否开放给大家使用',
    counter               int        default 0               not null comment '计数器',
    create_user_id        int                                not null comment '创建人',
    create_time           datetime   default utc_timestamp() not null comment '创建时间',
    update_user_id        int                                not null comment '最后修改人',
    update_time           datetime   default utc_timestamp() not null comment '最后更新时间',
    is_deleted            bigint                             not null comment '软删除'
)
    comment 'ai模型';

create index ai_model_ai_model_type_index
    on moai.ai_model (ai_model_type);

create index ai_model_ai_provider_index
    on moai.ai_model (ai_provider);

create table moai.ai_model_authorization
(
    id             int auto_increment comment 'id'
        primary key,
    ai_model_id    int                                  not null comment 'ai模型的id',
    team_id        int                                  not null comment '授权团队id',
    create_user_id int      default 0                   not null comment '创建人',
    create_time    datetime default current_timestamp() not null comment '创建时间',
    update_user_id int      default 0                   not null comment '更新人',
    update_time    datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint   default 0                   not null comment '软删除'
)
    comment '授权模型给哪些团队使用';

create table moai.ai_model_limit
(
    id              int auto_increment comment 'id'
        primary key,
    model_id        int                                  not null comment '模型id',
    user_id         int                                  not null comment '用户id',
    rule_type       int      default 0                   not null comment '限制的规则类型,每天/总额/有效期',
    limit_value     int                                  not null comment '限制值',
    expiration_time datetime default current_timestamp() not null comment '过期时间',
    create_user_id  int      default 0                   not null comment '创建人',
    create_time     datetime default current_timestamp() not null comment '创建时间',
    update_user_id  int      default 0                   not null comment '更新人',
    update_time     datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted      bigint   default 0                   not null comment '软删除'
)
    comment 'ai模型使用量限制，只能用于系统模型';

create table moai.ai_model_token_audit
(
    id                int auto_increment comment 'id'
        primary key,
    model_id          int                                  not null comment '模型id',
    useri_id          int                                  not null comment '用户id',
    completion_tokens int      default 0                   not null comment '完成数量',
    prompt_tokens     int      default 0                   not null comment '输入数量',
    total_tokens      int      default 0                   not null comment '总数量',
    count             int      default 0                   not null comment '调用次数',
    create_user_id    int      default 0                   not null comment '创建人',
    create_time       datetime default current_timestamp() not null comment '创建时间',
    update_user_id    int      default 0                   not null comment '更新人',
    update_time       datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted        bigint   default 0                   not null comment '软删除'
)
    comment '统计不同模型的token使用量，该表不是实时刷新的';

create table moai.ai_model_useage_log
(
    id                int auto_increment comment 'id'
        primary key,
    model_id          int                                     not null comment '模型id',
    useri_id          int                                     not null comment '用户id',
    completion_tokens int         default 0                   not null comment '完成数量',
    prompt_tokens     int         default 0                   not null comment '输入数量',
    total_tokens      int         default 0                   not null comment '总数量',
    channel           varchar(30) default '-'                 not null comment '渠道',
    create_user_id    int         default 0                   not null comment '创建人',
    create_time       datetime    default current_timestamp() not null comment '创建时间',
    update_user_id    int         default 0                   not null comment '更新人',
    update_time       datetime    default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted        bigint      default 0                   not null comment '软删除'
)
    comment '模型使用日志,记录每次请求使用记录';

create index ai_model_useage_log_channel_index
    on moai.ai_model_useage_log (channel);

create table moai.app
(
    id             binary(16)   default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    name           varchar(20)                                          not null comment '应用名称',
    description    varchar(255)                                         not null comment '描述',
    team_id        int                                                  not null comment '团队id',
    is_public      tinyint(1)   default 0                               not null comment '公开到团队外使用',
    is_disable     tinyint(1)   default 0                               not null comment '禁用',
    classify_id    int          default 0                               not null comment '分类id',
    is_foreign     tinyint(1)   default 0                               not null comment '是否外部应用',
    is_auth        tinyint(1)   default 0                               not null comment '是否开启授权才能使用，只有外部应用可以设置',
    app_type       int          default 0                               not null comment '应用类型，普通应用=0,流程编排=1',
    avatar         varchar(255) default ''                              not null comment '头像 objectKey',
    create_user_id int          default 0                               not null comment '创建人',
    create_time    datetime     default current_timestamp()             not null comment '创建时间',
    update_user_id int          default 0                               not null comment '更新人',
    update_time    datetime     default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint       default 0                               not null comment '软删除'
)
    comment '应用';

create index app_name_index
    on moai.app (name);

create index app_team_id_index
    on moai.app (team_id);

create table moai.app_assistant_chat
(
    id                 binary(16)                   default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    title              varchar(100)                 default '未命名标题'                    not null comment '对话标题',
    avatar             varchar(10)                  default '?'                             not null comment '头像',
    prompt             varchar(4000)                default ''                              not null comment '提示词',
    model_id           int                                                                  not null comment '对话使用的模型 id',
    wiki_ids           longtext collate utf8mb4_bin default 0                               not null comment '要使用的知识库id',
    plugins            longtext collate utf8mb4_bin default '[]'                            not null comment '要使用的插件',
    execution_settings longtext collate utf8mb4_bin default '[]'                            not null comment '对话影响参数',
    input_tokens       int                          default 0                               not null comment '输入token统计',
    out_tokens         int                          default 0                               not null comment '输出token统计',
    total_tokens       int                          default 0                               not null comment '使用的 token 总数',
    create_user_id     int                          default 0                               not null comment '创建人',
    create_time        datetime                     default current_timestamp()             not null comment '创建时间',
    update_user_id     int                          default 0                               not null comment '更新人',
    update_time        datetime                     default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted         bigint                       default 0                               not null comment '软删除'
)
    comment 'ai助手表';

create table moai.app_assistant_chat_history
(
    id             bigint auto_increment comment 'id'
        primary key,
    chat_id        binary(16)                           not null comment '对话id',
    completions_id varchar(50)                          not null comment '对话id',
    role           varchar(20)                          not null comment '角色',
    content        text                                 not null comment '内容',
    create_user_id int      default 0                   not null comment '创建人',
    create_time    datetime default current_timestamp() not null comment '创建时间',
    update_user_id int      default 0                   not null comment '更新人',
    update_time    datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint   default 0                   not null comment '软删除'
)
    comment '对话历史，不保存实际历史记录';

create index chat_history_pk_2
    on moai.app_assistant_chat_history (chat_id);

create table moai.app_chatapp
(
    id                 binary(16)                   default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    team_id            int                                                                  not null comment '团队id',
    app_id             binary(16)                                                           not null comment '所属应用id',
    prompt             varchar(4000)                default ''                              not null comment '提示词',
    model_id           int                                                                  not null comment '对话使用的模型 id',
    wiki_ids           longtext collate utf8mb4_bin default 0                               not null comment '要使用的知识库id'
        check (json_valid(`wiki_ids`)),
    plugins            longtext collate utf8mb4_bin default '[]'                            not null comment '要使用的插件',
    execution_settings longtext collate utf8mb4_bin default '[]'                            not null comment '对话影响参数'
        check (json_valid(`execution_settings`)),
    create_user_id     int                          default 0                               not null comment '创建人',
    create_time        datetime                     default current_timestamp()             not null comment '创建时间',
    update_user_id     int                          default 0                               not null comment '更新人',
    update_time        datetime                     default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted         bigint                       default 0                               not null comment '软删除'
)
    comment '普通应用';

create table moai.app_chatapp_chat
(
    id             binary(16)   default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    app_id         binary(16)                                           not null comment 'appid',
    title          varchar(100) default '未命名标题'                    not null comment '对话标题',
    input_tokens   int          default 0                               not null comment '输入token统计',
    out_tokens     int          default 0                               not null comment '输出token统计',
    total_tokens   int          default 0                               not null comment '使用的 token 总数',
    user_type      int          default 0                               not null comment '用户类型',
    create_user_id int          default 0                               not null comment '创建人',
    create_time    datetime     default current_timestamp()             not null comment '创建时间',
    update_user_id int          default 0                               not null comment '更新人',
    update_time    datetime     default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint       default 0                               not null comment '软删除'
)
    comment '普通应用对话表';

create table moai.app_chatapp_chat_history
(
    id             bigint auto_increment comment 'id'
        primary key,
    chat_id        binary(16)                           not null comment '对话id',
    completions_id varchar(50)                          not null comment '对话id',
    role           varchar(20)                          not null comment '角色',
    content        text                                 not null comment '内容',
    create_user_id int      default 0                   not null comment '创建人',
    create_time    datetime default current_timestamp() not null comment '创建时间',
    update_user_id int      default 0                   not null comment '更新人',
    update_time    datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint   default 0                   not null comment '软删除'
)
    comment '对话历史，不保存实际历史记录';

create index chat_history_pk_2
    on moai.app_chatapp_chat_history (chat_id);

create table moai.app_workflow_design
(
    id                    varbinary(16) default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    team_id               int                                                   not null comment '团队id',
    app_id                varbinary(16)                                         not null comment '应用id',
    ui_design             longtext      default '{}'                            not null comment 'ui设计，存储的是发布版本',
    function_desgin       longtext      default '{}'                            not null comment '功能设计，存储的是发布版本',
    ui_design_draft       longtext      default '{}'                            not null comment 'ui设计草稿',
    function_design_draft longtext      default '{}'                            not null comment '功能设计草稿',
    is_publish            tinyint(1)    default 0                               not null comment '是否发布',
    create_user_id        int           default 0                               not null comment '创建人',
    create_time           datetime      default current_timestamp()             not null comment '创建时间',
    update_user_id        int           default 0                               not null comment '更新人',
    update_time           datetime      default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted            bigint        default 0                               not null comment '软删除'
)
    comment '流程设计实例表';

create table moai.app_workflow_history
(
    id                 varbinary(16)                default unhex(replace(uuid(), '-', '')) not null comment 'varbinary(16)'
        primary key,
    team_id            int                                                                  not null comment '团队id',
    app_id             varbinary(16)                                                        not null comment '应用id',
    workflow_design_id varbinary(16)                                                        not null comment '流程设计id',
    state              int                          default 0                               not null comment '工作状态',
    system_paramters   longtext collate utf8mb4_bin                                         not null comment '系统参数'
        check (json_valid(`system_paramters`)),
    run_paramters      longtext collate utf8mb4_bin                                         not null comment '运行参数'
        check (json_valid(`run_paramters`)),
    data               longtext collate utf8mb4_bin default '{}'                            not null comment '数据内容'
        check (json_valid(`data`)),
    create_user_id     int                          default 0                               not null comment '创建人',
    create_time        datetime                     default current_timestamp()             not null comment '创建时间',
    update_user_id     int                          default 0                               not null comment '更新人',
    update_time        datetime                     default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted         bigint                       default 0                               not null comment '软删除'
)
    comment '流程执行记录';

create table moai.classify
(
    id             int auto_increment comment 'id'
        primary key,
    type           varchar(10)                              not null comment '分类类型',
    name           varchar(20)                              not null comment '分类名称',
    description    varchar(255) default ''                  not null comment '分类描述',
    create_user_id int          default 0                   not null comment '创建人',
    create_time    datetime     default current_timestamp() not null comment '创建时间',
    update_user_id int          default 0                   not null comment '更新人',
    update_time    datetime     default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint       default 0                   not null comment '软删除'
)
    comment '分类';

create index classify_name_index
    on moai.classify (name);

create index classify_type_index
    on moai.classify (type);

create table moai.external_app
(
    id             binary(16)   default unhex(replace(uuid(), '-', '')) not null comment 'app_id'
        primary key,
    team_id        int                                                  not null comment '团队id',
    name           varchar(20)                                          not null comment '应用名称',
    description    varchar(255)                                         not null comment '描述',
    avatar         varchar(255) default ''                              not null comment '头像objectKey',
    `key`          varchar(255)                                         not null comment '应用密钥',
    is_dsiable     tinyint(1)   default 0                               not null comment '禁用',
    create_user_id int          default 0                               not null comment '创建人',
    create_time    datetime     default current_timestamp()             not null comment '创建时间',
    update_user_id int          default 0                               not null comment '更新人',
    update_time    datetime     default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint       default 0                               not null comment '软删除'
)
    comment '系统接入';

create table moai.external_user
(
    id              int auto_increment comment '用户ID'
        primary key,
    external_app_id int                              not null comment '所属的外部应用id',
    user_uid        varchar(50)                      not null comment '外部用户标识',
    is_deleted      bigint                           not null comment '软删除',
    create_user_id  int                              not null comment '创建人',
    create_time     datetime default utc_timestamp() not null comment '创建时间',
    update_user_id  int                              not null comment '最后修改人',
    update_time     datetime default utc_timestamp() not null comment '最后更新时间'
)
    comment '外部系统的用户';

create table moai.file
(
    id             int auto_increment comment 'id'
        primary key,
    object_key     varchar(1024)                       not null comment '文件路径',
    file_extension varchar(10) default ''              not null comment '文件扩展名',
    file_md5       varchar(50)                         not null comment 'md5',
    file_size      int                                 not null comment '文件大小',
    content_type   varchar(50)                         not null comment '文件类型',
    is_uploaded    tinyint(1)                          not null comment '是否已经上传完毕',
    create_user_id int                                 not null comment '创建人',
    create_time    datetime    default utc_timestamp() not null comment '创建时间',
    update_user_id int                                 not null comment '最后修改人',
    update_time    datetime    default utc_timestamp() not null comment '最后更新时间',
    is_deleted     bigint                              not null comment '软删除'
)
    comment '文件列表';

create index file_file_md5_index
    on moai.file (file_md5);

create index file_object_key_index
    on moai.file (object_key(768));

create table moai.oauth_connection
(
    id             binary(16) default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    name           varchar(50)                                        not null comment '认证名称',
    provider       varchar(20)                                        not null comment '提供商',
    `key`          varchar(100)                                       not null comment '应用key',
    secret         varchar(100)                                       not null comment '密钥',
    icon_url       varchar(1000)                                      not null comment '图标地址',
    authorize_url  varchar(1000)                                      not null comment '登录跳转地址',
    well_known     varchar(1000)                                      not null comment '发现端口',
    is_deleted     bigint     default 0                               not null comment '软删除',
    create_time    datetime   default current_timestamp()             not null comment '创建时间',
    update_time    datetime   default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    create_user_id int        default 0                               not null comment '创建人',
    update_user_id int        default 0                               not null comment '更新人'
)
    comment 'oauth2.0系统';

create table moai.plugin
(
    id             int auto_increment comment 'id'
        primary key,
    team_id        int          default 0               not null comment '某个团队创建的自定义插件',
    plugin_id      int                                  not null comment '对应的实际插件的id，不同类型的插件表不一样',
    plugin_name    varchar(50)                          not null comment '插件名称',
    title          varchar(50)                          not null comment '插件标题',
    description    varchar(255) default ''              not null comment '注释',
    type           int                                  not null comment 'mcp|openapi|native|tool',
    classify_id    int          default 0               not null comment '分类id',
    is_public      tinyint(1)   default 0               not null comment '公开访问',
    counter        int          default 0               not null comment '计数器',
    create_user_id int                                  not null comment '创建人',
    create_time    datetime     default utc_timestamp() not null comment '创建时间',
    update_user_id int                                  not null comment '最后修改人',
    update_time    datetime     default utc_timestamp() not null comment '最后更新时间',
    is_deleted     bigint                               not null comment '软删除'
)
    comment '插件';

create index plugin_plugin_name_index
    on moai.plugin (plugin_name);

create index plugin_title_index
    on moai.plugin (title);

create table moai.plugin_authorization
(
    id             int auto_increment comment 'id'
        primary key,
    plugin_id      int                                  not null comment '私有插件的id',
    team_id        int                                  not null comment '授权团队id',
    create_user_id int      default 0                   not null comment '创建人',
    create_time    datetime default current_timestamp() not null comment '创建时间',
    update_user_id int      default 0                   not null comment '更新人',
    update_time    datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint   default 0                   not null comment '软删除'
)
    comment '授权私有插件给哪些团队使用';

create table moai.plugin_custom
(
    id                int auto_increment comment 'id'
        primary key,
    server            varchar(255)                     not null comment '服务器地址',
    headers           text                             not null comment '头部',
    queries           text                             not null comment 'query参数',
    type              int                              not null comment 'mcp|openapi',
    openapi_file_id   int                              not null comment '文件id',
    openapi_file_name varchar(255)                     not null comment '文件名称',
    create_user_id    int                              not null comment '创建人',
    create_time       datetime default utc_timestamp() not null comment '创建时间',
    update_user_id    int                              not null comment '最后修改人',
    update_time       datetime default utc_timestamp() not null comment '最后更新时间',
    is_deleted        bigint                           not null comment '软删除'
)
    comment '自定义插件';

create table moai.plugin_function
(
    id               int auto_increment comment 'id'
        primary key,
    plugin_custom_id int                                   not null comment 'plugin_custom_id',
    name             varchar(255)                          not null comment '函数名称',
    summary          varchar(1000) default ''              not null comment '描述',
    path             varchar(255)  default ''              not null comment 'api路径',
    create_user_id   int                                   not null comment '创建人',
    create_time      datetime      default utc_timestamp() not null comment '创建时间',
    update_user_id   int                                   not null comment '最后修改人',
    update_time      datetime      default utc_timestamp() not null comment '最后更新时间',
    is_deleted       bigint                                not null comment '软删除'
)
    comment '插件函数';

create table moai.plugin_limit
(
    id              int auto_increment comment 'id'
        primary key,
    plugin_id       int                                  not null comment '插件id',
    user_id         int                                  not null comment '用户id',
    rule_type       int      default 0                   not null comment '限制的规则类型,每天/总额/有效期',
    limit_value     int                                  not null comment '限制值',
    expiration_time datetime default current_timestamp() not null comment '过期时间',
    create_user_id  int      default 0                   not null comment '创建人',
    create_time     datetime default current_timestamp() not null comment '创建时间',
    update_user_id  int      default 0                   not null comment '更新人',
    update_time     datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted      bigint   default 0                   not null comment '软删除'
)
    comment '插件使用量限制';

create table moai.plugin_log
(
    id             int auto_increment comment 'id'
        primary key,
    plugin_id      int                                     not null comment '插件id',
    useri_id       int                                     not null comment '用户id',
    channel        varchar(10) default '0'                 not null comment '渠道',
    create_user_id int         default 0                   not null comment '创建人',
    create_time    datetime    default current_timestamp() not null comment '创建时间',
    update_user_id int         default 0                   not null comment '更新人',
    update_time    datetime    default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint      default 0                   not null comment '软删除'
)
    comment '插件使用日志';

create index plugin_log_channel_index
    on moai.plugin_log (channel);

create table moai.plugin_native
(
    id                       int auto_increment comment 'id'
        primary key,
    template_plugin_classify varchar(20)                                          not null comment '模板分类',
    template_plugin_key      varchar(50)                                          not null comment '对应的内置插件key',
    config                   longtext collate utf8mb4_bin default '{}'            not null comment '配置参数'
        check (json_valid(`config`)),
    create_user_id           int                                                  not null comment '创建人',
    create_time              datetime                     default utc_timestamp() not null comment '创建时间',
    update_user_id           int                                                  not null comment '最后修改人',
    update_time              datetime                     default utc_timestamp() not null comment '最后更新时间',
    is_deleted               bigint                                               not null comment '软删除'
)
    comment '内置插件';

create table moai.plugin_tool
(
    id                       int auto_increment comment 'id'
        primary key,
    template_plugin_classify varchar(20)                      not null comment '模板分类',
    template_plugin_key      varchar(50)                      not null comment '对应的内置插件key',
    create_user_id           int                              not null comment '创建人',
    create_time              datetime default utc_timestamp() not null comment '创建时间',
    update_user_id           int                              not null comment '最后修改人',
    update_time              datetime default utc_timestamp() not null comment '最后更新时间',
    is_deleted               bigint                           not null comment '软删除'
)
    comment '内置插件';

create table moai.prompt
(
    id              int auto_increment comment 'id'
        primary key,
    name            varchar(20)                            not null comment '名称',
    description     varchar(255)                           not null comment '描述',
    content         text                                   not null comment '提示词内容',
    prompt_class_id int                                    not null comment '分类id',
    is_public       tinyint(1) default 0                   not null comment '是否公开',
    counter         int        default 0                   not null comment '计数器',
    create_user_id  int        default 0                   not null comment '创建人',
    create_time     datetime   default current_timestamp() not null comment '创建时间',
    update_user_id  int        default 0                   not null comment '更新人',
    update_time     datetime   default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted      bigint     default 0                   not null comment '软删除'
)
    comment '提示词';

create table moai.setting
(
    id             int auto_increment comment 'id'
        primary key,
    `key`          varchar(50)                          not null comment '配置名称',
    value          varchar(255)                         not null comment '配置值,json',
    description    varchar(255) default ''              not null comment '描述',
    create_user_id int                                  not null comment '创建人',
    create_time    datetime     default utc_timestamp() not null comment '创建时间',
    update_user_id int                                  not null comment '最后修改人',
    update_time    datetime     default utc_timestamp() not null comment '最后更新时间',
    is_deleted     bigint                               not null comment '软删除',
    constraint setting_key_is_deleted_uindex
        unique (`key`, is_deleted) comment 'key唯一'
)
    comment '系统设置';

create table moai.team
(
    id             int auto_increment comment 'id'
        primary key,
    name           varchar(20)                              not null comment '团队名称',
    description    varchar(255) default ''                  not null comment '团队描述',
    avatar         varchar(255) default ''                  not null comment '团队头像,objectkey',
    create_user_id int          default 0                   not null comment '创建人',
    create_time    datetime     default current_timestamp() not null comment '创建时间',
    update_user_id int          default 0                   not null comment '更新人',
    update_time    datetime     default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint       default 0                   not null comment '软删除'
)
    comment '团队';

create table moai.team_user
(
    id             int auto_increment comment 'id'
        primary key,
    team_id        int                                  not null comment '团队id',
    user_id        int                                  not null comment '用户id',
    role           int      default 0                   not null comment '角色,普通用户=0,协作者=1,管理员=2,所有者=3',
    create_user_id int      default 0                   not null comment '创建人',
    create_time    datetime default current_timestamp() not null comment '创建时间',
    update_user_id int      default 0                   not null comment '更新人',
    update_time    datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint   default 0                   not null comment '软删除'
)
    comment '团队成员';

create table moai.user
(
    id             int auto_increment comment '用户ID'
        primary key,
    user_name      varchar(50)                          not null comment '用户名',
    email          varchar(255)                         not null comment '邮箱',
    password       varchar(255)                         not null comment '密码',
    nick_name      varchar(50)                          not null comment '昵称',
    avatar_path    varchar(255) default ''              not null comment '头像路径',
    phone          varchar(20)                          not null comment '手机号',
    is_disable     tinyint(1)                           not null comment '禁用',
    is_admin       tinyint(1)                           not null comment '是否管理员',
    password_salt  varchar(255)                         not null comment '计算密码值的salt',
    is_deleted     bigint                               not null comment '软删除',
    create_user_id int                                  not null comment '创建人',
    create_time    datetime     default utc_timestamp() not null comment '创建时间',
    update_user_id int                                  not null comment '最后修改人',
    update_time    datetime     default utc_timestamp() not null comment '最后更新时间',
    constraint users_email_is_deleted_uindex
        unique (email, is_deleted),
    constraint users_phone_is_deleted_uindex
        unique (phone, is_deleted),
    constraint users_user_name_is_deleted_uindex
        unique (user_name, is_deleted)
)
    comment '用户';

create index idx_users_email
    on moai.user (email);

create index idx_users_phone
    on moai.user (phone);

create index idx_users_user_name
    on moai.user (user_name);

create table moai.user_oauth
(
    id             int auto_increment comment 'id'
        primary key,
    user_id        int                              not null comment '用户id',
    provider_id    binary(16)                       not null comment '供应商id,对应oauth_connection表',
    sub            varchar(50)                      not null comment '用户oauth对应的唯一id',
    create_user_id int                              not null comment '创建人',
    create_time    datetime default utc_timestamp() not null comment '创建时间',
    update_user_id int                              not null comment '最后修改人',
    update_time    datetime default utc_timestamp() not null comment '最后更新时间',
    is_deleted     bigint                           not null comment '软删除',
    constraint user_oauth_provider_id_sub_is_deleted_uindex
        unique (provider_id, sub, is_deleted) comment '一个sub不能关联不同的用户'
)
    comment 'oauth2.0对接';

create table moai.wiki
(
    id                   int auto_increment comment 'id'
        primary key,
    name                 varchar(20)                          not null comment '知识库名称',
    description          varchar(255)                         not null comment '知识库描述',
    is_public            tinyint(1)                           not null comment '是否公开，公开后所有人都可以使用，但是不能进去操作',
    counter              int          default 0               not null comment '计数器',
    is_lock              tinyint(1)                           not null comment '是否已被锁定配置',
    embedding_model_id   int                                  not null comment '向量化模型的id',
    embedding_dimensions int          default 1024            not null comment '知识库向量维度',
    avatar               varchar(255) default ''              not null comment '团队头像',
    team_id              int          default 0               not null comment '团队id，不填则是个人知识库',
    create_user_id       int                                  not null comment '创建人',
    create_time          datetime     default utc_timestamp() not null comment '创建时间',
    update_user_id       int                                  not null comment '最后修改人',
    update_time          datetime     default utc_timestamp() not null comment '最后更新时间',
    is_deleted           bigint                               not null comment '软删除'
)
    comment '知识库';

create table moai.wiki_document
(
    id             int auto_increment comment 'id'
        primary key,
    wiki_id        int                                                  not null comment '知识库id',
    file_id        int                                                  not null comment '文件id',
    object_key     varchar(100)                                         not null comment '文件路径',
    file_name      varchar(1024)                                        not null comment '文档名称',
    file_type      varchar(10)                                          not null comment '文件扩展名称，如.md',
    is_embedding   tinyint(1)                   default 0               not null comment '是否已经向量化',
    version_no     bigint                       default 0               not null comment '版本号，可与向量元数据对比，确认最新文档版本号是否一致',
    slice_config   longtext collate utf8mb4_bin default '{}'            not null comment '切割配置'
        check (json_valid(`slice_config`)),
    is_update      tinyint(1)                   default 0               not null comment '是否有更新，需要重新进行向量化',
    create_user_id int                                                  not null comment '创建人',
    create_time    datetime                     default utc_timestamp() not null comment '创建时间',
    update_user_id int                                                  not null comment '最后修改人',
    update_time    datetime                     default utc_timestamp() not null comment '最后更新时间',
    is_deleted     bigint                                               not null comment '软删除'
)
    comment '知识库文档';

create index wiki_document_object_key_index
    on moai.wiki_document (object_key);

create table moai.wiki_document_chunk_content_preview
(
    id             bigint auto_increment comment '切片唯一ID（slice_id）'
        primary key,
    wiki_id        int                                  not null comment '关联知识库标识（冗余字段）',
    document_id    int                                  not null comment '关联文档唯一标识',
    slice_content  text                                 not null comment '原始切片内容',
    slice_order    int                                  not null comment '切片在文档中的顺序',
    slice_length   int                                  not null comment '切片字符长度',
    create_user_id int      default 0                   not null comment '创建人',
    create_time    datetime default current_timestamp() not null comment '创建时间',
    update_user_id int      default 0                   not null comment '更新人',
    update_time    datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint   default 0                   not null comment '软删除'
)
    comment '文档切片预览';

create index idx_doc_slice
    on moai.wiki_document_chunk_content_preview (document_id, id)
    comment '文档+切片ID关联索引';

create index idx_wiki_slice
    on moai.wiki_document_chunk_content_preview (wiki_id)
    comment '按知识库筛选切片索引';

create table moai.wiki_document_chunk_embedding
(
    id               binary(16) default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    wiki_id          int                                                not null comment '知识库id',
    document_id      int                                                not null comment '关联文档唯一标识（冗余字段）',
    chunk_id         binary(16)                                         not null comment '源id，关联自身',
    metadata_type    int                                                not null comment '元数据类型：0=原文，1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段',
    metadata_content text                                               not null comment '提问/提纲/摘要内容',
    is_embedding     tinyint(1) default 0                               not null comment '是否被向量化',
    create_user_id   int        default 0                               not null comment '创建人',
    create_time      datetime   default current_timestamp()             not null comment '创建时间',
    update_user_id   int        default 0                               not null comment '更新人',
    update_time      datetime   default current_timestamp()             not null on update current_timestamp() comment '更新时间',
    is_deleted       bigint     default 0                               not null comment '软删除'
)
    comment '切片向量化内容';

create index idx_deriv_type
    on moai.wiki_document_chunk_embedding (metadata_type)
    comment '按衍生类型筛选索引';

create index idx_doc_deriv
    on moai.wiki_document_chunk_embedding (document_id, metadata_type)
    comment '文档+衍生类型筛选索引';

create index idx_slice_deriv
    on moai.wiki_document_chunk_embedding (id)
    comment '切片+衍生内容关联索引';

create table moai.wiki_document_chunk_metadata_preview
(
    id               bigint auto_increment comment 'id'
        primary key,
    wiki_id          int                                  not null comment '知识库id',
    document_id      int                                  not null comment '关联文档唯一标识（冗余字段）',
    chunk_id         bigint                               not null comment '关联切片ID（表A主键）',
    metadata_type    int                                  not null comment '元数据类型：1=大纲，2=问题，3=关键词，4=摘要，5=聚合的段',
    metadata_content text                                 not null comment '提问/提纲/摘要内容',
    create_user_id   int      default 0                   not null comment '创建人',
    create_time      datetime default current_timestamp() not null comment '创建时间',
    update_user_id   int      default 0                   not null comment '更新人',
    update_time      datetime default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted       bigint   default 0                   not null comment '软删除'
)
    comment '切片元数据内容表（提问/提纲/摘要）';

create index idx_deriv_type
    on moai.wiki_document_chunk_metadata_preview (metadata_type)
    comment '按衍生类型筛选索引';

create index idx_doc_deriv
    on moai.wiki_document_chunk_metadata_preview (document_id, metadata_type)
    comment '文档+衍生类型筛选索引';

create index idx_slice_deriv
    on moai.wiki_document_chunk_metadata_preview (chunk_id, id)
    comment '切片+衍生内容关联索引';

create table moai.wiki_plugin_config
(
    id             int auto_increment comment 'id'
        primary key,
    wiki_id        int                                                      not null comment '知识库id',
    title          varchar(20)                                              not null comment '插件标题',
    config         longtext collate utf8mb4_bin default '{}'                not null comment '配置'
        check (json_valid(`config`)),
    plugin_type    varchar(10)                                              not null comment '插件类型',
    work_message   varchar(1000)                default ''                  not null comment '运行信息',
    work_state     int                          default 0                   not null comment '状态',
    create_user_id int                          default 0                   not null comment '创建人',
    create_time    datetime                     default current_timestamp() not null comment '创建时间',
    update_user_id int                          default 0                   not null comment '更新人',
    update_time    datetime                     default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted     bigint                       default 0                   not null comment '软删除'
)
    comment '知识库插件配置';

create table moai.wiki_plugin_config_document
(
    id               int auto_increment comment 'id'
        primary key,
    wiki_id          int                                       not null comment '知识库id',
    config_id        int                                       not null comment '爬虫id',
    wiki_document_id int           default 0                   not null comment '文档id',
    relevance_key    varchar(1000)                             not null comment '关联对象',
    relevance_value  varchar(1000) default ''                  not null comment '关联值',
    create_user_id   int           default 0                   not null comment '创建人',
    create_time      datetime      default current_timestamp() not null comment '创建时间',
    update_user_id   int           default 0                   not null comment '更新人',
    update_time      datetime      default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted       bigint        default 0                   not null comment '软删除'
)
    comment '知识库文档关联任务，这里的任务都是成功的';

create table moai.wiki_plugin_config_document_state
(
    id              int auto_increment comment 'id'
        primary key,
    wiki_id         int                                       not null comment '知识库id',
    config_id       int                                       not null comment '爬虫id',
    relevance_key   varchar(1000)                             not null comment '关联对象',
    relevance_value varchar(1000) default ''                  not null comment '关联值',
    message         varchar(1000) default ''                  not null comment '信息',
    state           int           default 0                   not null comment '状态',
    create_user_id  int           default 0                   not null comment '创建人',
    create_time     datetime      default current_timestamp() not null comment '创建时间',
    update_user_id  int           default 0                   not null comment '更新人',
    update_time     datetime      default current_timestamp() not null on update current_timestamp() comment '更新时间',
    is_deleted      bigint        default 0                   not null comment '软删除'
)
    comment '知识库文档关联任务';

create table moai.worker_task
(
    id             binary(16)                   default unhex(replace(uuid(), '-', '')) not null comment 'id'
        primary key,
    bind_type      varchar(20)                                                          not null comment '关联类型',
    bind_id        int                                                                  not null comment '关联对象id',
    state          int                                                                  not null comment '任务状态，不同的任务类型状态值规则不一样',
    message        text                                                                 not null comment '消息、错误信息',
    data           longtext collate utf8mb4_bin default '{}'                            not null comment '自定义数据,json格式'
        check (json_valid(`data`)),
    create_user_id int                                                                  not null comment '创建人',
    create_time    datetime                     default utc_timestamp()                 not null comment '创建时间',
    update_user_id int                                                                  not null comment '最后修改人',
    update_time    datetime                     default utc_timestamp()                 not null comment '最后更新时间',
    is_deleted     bigint                                                               not null comment '软删除'
)
    comment '工作任务';

