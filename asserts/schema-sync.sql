-- 存量库 schema 补齐（由 model 生成，幂等可重复执行）.
-- 生成命令：dotnet run --project tool/SchemaSync（禁止手工编辑）.
-- 覆盖 EF Core 模型中的全部表与列：建缺失表、补缺失列、建缺失索引；不删除任何表/列.

CREATE SEQUENCE IF NOT EXISTS external_id_seq START WITH 1 INCREMENT BY 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS access_app (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    name character varying(50) NOT NULL,
    app_type integer NOT NULL DEFAULT 0,
    team_scope integer NOT NULL DEFAULT 0,
    description character varying(255) NOT NULL,
    team_id integer NOT NULL,
    level integer NOT NULL DEFAULT 0,
    key_prefix character varying(16) NOT NULL,
    secret character varying(255),
    secret_mask character varying(32) NOT NULL,
    secret_salt character varying(255),
    status integer NOT NULL DEFAULT 0,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    key character varying(255) NOT NULL,
    callback_urls text NOT NULL,
    last_used_time timestamp with time zone,
    avatar_path character varying(255) NOT NULL DEFAULT '',
    CONSTRAINT idx_access_app_primary PRIMARY KEY (id)
);
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS app_type integer NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS team_scope integer NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT '';
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS level integer NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS key_prefix character varying(16) NOT NULL DEFAULT '';
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS secret character varying(255);
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS secret_mask character varying(32) NOT NULL DEFAULT '';
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS secret_salt character varying(255);
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS status integer NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS key character varying(255) NOT NULL DEFAULT '';
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS callback_urls text NOT NULL DEFAULT '';
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS last_used_time timestamp with time zone;
ALTER TABLE access_app ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS ai_channel (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    provider_key character varying(50) NOT NULL,
    name character varying(100) NOT NULL,
    protocol_family integer NOT NULL,
    base_url character varying(1000) NOT NULL,
    api_key character varying(500) NOT NULL,
    enabled boolean NOT NULL DEFAULT TRUE,
    description character varying(1000),
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    is_deleted bigint NOT NULL,
    CONSTRAINT ai_channel_pkey PRIMARY KEY (id)
);
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS provider_key character varying(50) NOT NULL DEFAULT '';
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS protocol_family integer NOT NULL DEFAULT 0;
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS base_url character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS api_key character varying(500) NOT NULL DEFAULT '';
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT TRUE;
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS description character varying(1000);
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_channel ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS ai_model (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    channel_id uuid NOT NULL,
    model_id character varying(200) NOT NULL,
    name character varying(200) NOT NULL,
    description character varying(2000),
    family character varying(100),
    model_kind character varying(30) NOT NULL,
    supports_vision boolean NOT NULL,
    supports_attachments boolean NOT NULL,
    supports_reasoning boolean NOT NULL,
    supports_tool_call boolean NOT NULL,
    supports_structured_output boolean NOT NULL,
    supports_temperature boolean NOT NULL,
    context_window integer NOT NULL,
    max_output integer NOT NULL,
    modalities_input character varying(500),
    modalities_output character varying(500),
    knowledge_cutoff character varying(50),
    release_date character varying(50),
    last_updated character varying(50),
    open_weights boolean NOT NULL,
    cost_input numeric(18,6),
    cost_output numeric(18,6),
    cost_cache_read numeric(18,6),
    enabled boolean NOT NULL DEFAULT TRUE,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    is_deleted bigint NOT NULL,
    is_public boolean NOT NULL DEFAULT TRUE,
    CONSTRAINT ai_model_pkey PRIMARY KEY (id)
);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS channel_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS model_id character varying(200) NOT NULL DEFAULT '';
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS name character varying(200) NOT NULL DEFAULT '';
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS description character varying(2000);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS family character varying(100);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS model_kind character varying(30) NOT NULL DEFAULT '';
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS supports_vision boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS supports_attachments boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS supports_reasoning boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS supports_tool_call boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS supports_structured_output boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS supports_temperature boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS context_window integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS max_output integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS modalities_input character varying(500);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS modalities_output character varying(500);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS knowledge_cutoff character varying(50);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS release_date character varying(50);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS last_updated character varying(50);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS open_weights boolean NOT NULL DEFAULT false;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS cost_input numeric(18,6);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS cost_output numeric(18,6);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS cost_cache_read numeric(18,6);
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT TRUE;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model ADD COLUMN IF NOT EXISTS is_public boolean NOT NULL DEFAULT TRUE;

CREATE TABLE IF NOT EXISTS ai_model_authorization (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    ai_model_id uuid NOT NULL,
    team_id integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_ai_model_authorization_primary PRIMARY KEY (id)
);
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS ai_model_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_model_authorization ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS ai_model_token_audit (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    model_id uuid NOT NULL,
    team_id integer NOT NULL,
    user_id integer NOT NULL,
    use_type integer NOT NULL,
    use_resource_id uuid NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'::uuid),
    completion_tokens integer NOT NULL,
    prompt_tokens integer NOT NULL,
    cached_tokens integer NOT NULL,
    total_tokens integer NOT NULL,
    count integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_ai_model_token_audit_primary PRIMARY KEY (id)
);
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS model_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS user_id integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS use_type integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS use_resource_id uuid NOT NULL DEFAULT ('00000000-0000-0000-0000-000000000000'::uuid);
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS completion_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS prompt_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS cached_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS total_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS count integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_model_token_audit ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS ai_model_usage_log (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    model_id uuid NOT NULL,
    team_id integer NOT NULL,
    user_id bigint NOT NULL,
    completion_tokens integer NOT NULL,
    prompt_tokens integer NOT NULL,
    total_tokens integer NOT NULL,
    use_type integer NOT NULL,
    use_resource_id integer NOT NULL,
    channel character varying(30) NOT NULL DEFAULT ('-'::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_ai_model_usage_log_primary PRIMARY KEY (id)
);
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS model_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS completion_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS prompt_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS total_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS use_type integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS use_resource_id integer NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS channel character varying(30) NOT NULL DEFAULT ('-'::character varying);
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE ai_model_usage_log ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS app (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    name character varying(20) NOT NULL,
    description character varying(255) NOT NULL,
    team_id integer NOT NULL,
    is_public boolean NOT NULL,
    is_disable boolean NOT NULL,
    classify_id integer NOT NULL,
    app_type integer NOT NULL,
    avatar character varying(255) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    publish_status smallint NOT NULL,
    publish_time timestamp with time zone DEFAULT (timezone('utc'::text, now())),
    publish_version integer,
    version integer NOT NULL DEFAULT (10000),
    is_external boolean NOT NULL,
    is_auth boolean NOT NULL,
    is_acp_a2a_enabled boolean NOT NULL DEFAULT FALSE,
    use_count integer NOT NULL DEFAULT (0),
    message_count bigint NOT NULL DEFAULT (0),
    CONSTRAINT idx_65606_primary PRIMARY KEY (id)
);
ALTER TABLE app ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app ADD COLUMN IF NOT EXISTS name character varying(20) NOT NULL DEFAULT '';
ALTER TABLE app ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT '';
ALTER TABLE app ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app ADD COLUMN IF NOT EXISTS is_public boolean NOT NULL DEFAULT false;
ALTER TABLE app ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE app ADD COLUMN IF NOT EXISTS classify_id integer NOT NULL DEFAULT 0;
ALTER TABLE app ADD COLUMN IF NOT EXISTS app_type integer NOT NULL DEFAULT 0;
ALTER TABLE app ADD COLUMN IF NOT EXISTS avatar character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE app ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE app ADD COLUMN IF NOT EXISTS publish_status smallint NOT NULL DEFAULT 0;
ALTER TABLE app ADD COLUMN IF NOT EXISTS publish_time timestamp with time zone DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app ADD COLUMN IF NOT EXISTS publish_version integer;
ALTER TABLE app ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT (10000);
ALTER TABLE app ADD COLUMN IF NOT EXISTS is_external boolean NOT NULL DEFAULT false;
ALTER TABLE app ADD COLUMN IF NOT EXISTS is_auth boolean NOT NULL DEFAULT false;
ALTER TABLE app ADD COLUMN IF NOT EXISTS is_acp_a2a_enabled boolean NOT NULL DEFAULT FALSE;
ALTER TABLE app ADD COLUMN IF NOT EXISTS use_count integer NOT NULL DEFAULT (0);
ALTER TABLE app ADD COLUMN IF NOT EXISTS message_count bigint NOT NULL DEFAULT (0);

CREATE TABLE IF NOT EXISTS app_access_point (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    title character varying(100),
    subtitle character varying(255),
    placeholder character varying(100),
    primary_color character varying(20),
    position character varying(20) NOT NULL DEFAULT ('bottomRight'::character varying),
    display_mode character varying(20) NOT NULL DEFAULT ('floating'::character varying),
    launcher_text character varying(50),
    avatar character varying(255),
    panel_width integer NOT NULL DEFAULT 380,
    panel_height integer NOT NULL DEFAULT 560,
    default_open boolean NOT NULL,
    enabled boolean NOT NULL DEFAULT TRUE,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_access_point_pkey PRIMARY KEY (id)
);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS title character varying(100);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS subtitle character varying(255);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS placeholder character varying(100);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS primary_color character varying(20);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS position character varying(20) NOT NULL DEFAULT ('bottomRight'::character varying);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS display_mode character varying(20) NOT NULL DEFAULT ('floating'::character varying);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS launcher_text character varying(50);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS avatar character varying(255);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS panel_width integer NOT NULL DEFAULT 380;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS panel_height integer NOT NULL DEFAULT 560;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS default_open boolean NOT NULL DEFAULT false;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT TRUE;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_access_point ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS app_agent_config (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    prompt character varying(4000) NOT NULL DEFAULT (''::character varying),
    model_id uuid NOT NULL,
    model_ids text NOT NULL DEFAULT ('[]'::text),
    wiki_ids text NOT NULL DEFAULT ('[]'::text),
    plugins text NOT NULL DEFAULT ('[]'::text),
    execution_settings text NOT NULL DEFAULT ('{}'::text),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    skills text NOT NULL DEFAULT ('[]'::text),
    opening_statement character varying(4000) NOT NULL DEFAULT (''::character varying),
    opening_statement_enabled boolean NOT NULL,
    prompts text NOT NULL DEFAULT ('[]'::text),
    published_config text,
    status smallint NOT NULL,
    quick_inputs text NOT NULL DEFAULT ('[]'::text),
    workflow_apps text NOT NULL DEFAULT ('[]'::text),
    agent_apps text NOT NULL DEFAULT ('[]'::text),
    graph_ids text NOT NULL DEFAULT ('[]'::text),
    team_agent_ids text NOT NULL DEFAULT ('[]'::text),
    default_team_agent_id uuid,
    team_agent_defs text NOT NULL DEFAULT (''::text),
    CONSTRAINT app_agent_config_pkey PRIMARY KEY (id)
);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS prompt character varying(4000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS model_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS model_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS wiki_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS plugins text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS execution_settings text NOT NULL DEFAULT ('{}'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS skills text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS opening_statement character varying(4000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS opening_statement_enabled boolean NOT NULL DEFAULT false;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS prompts text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS published_config text;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS status smallint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS quick_inputs text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS workflow_apps text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS agent_apps text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS graph_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS team_agent_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS default_team_agent_id uuid;
ALTER TABLE app_agent_config ADD COLUMN IF NOT EXISTS team_agent_defs text NOT NULL DEFAULT (''::text);

CREATE TABLE IF NOT EXISTS app_agent_message (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    session_id uuid NOT NULL,
    seq integer NOT NULL,
    role character varying(20) NOT NULL,
    content text NOT NULL DEFAULT (''::text),
    completions_id character varying(50) NOT NULL DEFAULT (''::character varying),
    tool_calls text NOT NULL DEFAULT ('[]'::text),
    tool_call_id character varying(50) NOT NULL DEFAULT (''::character varying),
    reasoning text NOT NULL DEFAULT (''::text),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_agent_message_pkey PRIMARY KEY (id)
);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS session_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS seq integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS role character varying(20) NOT NULL DEFAULT '';
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS content text NOT NULL DEFAULT (''::text);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS completions_id character varying(50) NOT NULL DEFAULT (''::character varying);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS tool_calls text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS tool_call_id character varying(50) NOT NULL DEFAULT (''::character varying);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS reasoning text NOT NULL DEFAULT (''::text);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_agent_message ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS app_agent_session (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    title character varying(100) NOT NULL DEFAULT ('未命名标题'::character varying),
    user_type integer NOT NULL,
    input_tokens integer NOT NULL,
    cached_tokens integer NOT NULL,
    context_tokens integer NOT NULL,
    out_tokens integer NOT NULL,
    total_tokens integer NOT NULL,
    last_message_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    state jsonb,
    prompt_id integer NOT NULL,
    last_model_id uuid NOT NULL,
    last_team_agent_id uuid,
    CONSTRAINT app_agent_session_pkey PRIMARY KEY (id)
);
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS title character varying(100) NOT NULL DEFAULT ('未命名标题'::character varying);
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS user_type integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS input_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS cached_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS context_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS out_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS total_tokens integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS last_message_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS state jsonb;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS prompt_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS last_model_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_agent_session ADD COLUMN IF NOT EXISTS last_team_agent_id uuid;

CREATE TABLE IF NOT EXISTS app_favorite (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    app_id uuid NOT NULL,
    user_id bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_favorite_pkey PRIMARY KEY (id)
);
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_favorite ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS app_memory_shard (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    app_id uuid NOT NULL,
    team_id integer NOT NULL,
    user_id bigint NOT NULL,
    memory_count integer NOT NULL,
    last_active_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_memory_shard_pkey PRIMARY KEY (id)
);
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS memory_count integer NOT NULL DEFAULT 0;
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS last_active_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_memory_shard ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS app_security_config (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    enabled boolean NOT NULL,
    mask_tool_result boolean NOT NULL DEFAULT TRUE,
    mask_tool_args boolean NOT NULL,
    mask_model_output boolean NOT NULL,
    rules text NOT NULL DEFAULT ('[]'::text),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    model_output_rules text NOT NULL DEFAULT ('[]'::text),
    mask_input boolean NOT NULL,
    CONSTRAINT app_security_config_pkey PRIMARY KEY (id)
);
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT false;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS mask_tool_result boolean NOT NULL DEFAULT TRUE;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS mask_tool_args boolean NOT NULL DEFAULT false;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS mask_model_output boolean NOT NULL DEFAULT false;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS rules text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS model_output_rules text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_security_config ADD COLUMN IF NOT EXISTS mask_input boolean NOT NULL DEFAULT false;

CREATE TABLE IF NOT EXISTS app_security_event (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    session_id uuid,
    user_id bigint,
    scope text NOT NULL,
    rule_name text NOT NULL,
    rule_type text NOT NULL,
    strategy text NOT NULL,
    action text NOT NULL,
    hit_count integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_security_event_pkey PRIMARY KEY (id)
);
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS session_id uuid;
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS user_id bigint;
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS scope text NOT NULL DEFAULT '';
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS rule_name text NOT NULL DEFAULT '';
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS rule_type text NOT NULL DEFAULT '';
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS strategy text NOT NULL DEFAULT '';
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS action text NOT NULL DEFAULT '';
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS hit_count integer NOT NULL DEFAULT 0;
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_security_event ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS app_user_config (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    app_id uuid NOT NULL,
    user_id bigint NOT NULL,
    team_id integer NOT NULL,
    prompt_id integer NOT NULL,
    skills text NOT NULL DEFAULT ('[]'::text),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    tool_approval_mode character varying(16) NOT NULL DEFAULT ('auto'::character varying),
    memory_mode smallint NOT NULL,
    memory_global_mode smallint NOT NULL DEFAULT (0),
    CONSTRAINT app_user_config_pkey PRIMARY KEY (id)
);
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS prompt_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS skills text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS tool_approval_mode character varying(16) NOT NULL DEFAULT ('auto'::character varying);
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS memory_mode smallint NOT NULL DEFAULT 0;
ALTER TABLE app_user_config ADD COLUMN IF NOT EXISTS memory_global_mode smallint NOT NULL DEFAULT (0);

CREATE TABLE IF NOT EXISTS app_workflow_config (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    draft_definition text NOT NULL,
    draft_editor_data text NOT NULL DEFAULT ('{}'::text),
    published_definition text,
    version integer NOT NULL,
    status smallint NOT NULL,
    publish_time timestamp with time zone DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_workflow_config_pkey PRIMARY KEY (id)
);
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS draft_definition text NOT NULL DEFAULT '';
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS draft_editor_data text NOT NULL DEFAULT ('{}'::text);
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS published_definition text;
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS status smallint NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS publish_time timestamp with time zone DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_workflow_config ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS app_workflow_instance (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    app_id uuid NOT NULL,
    workflow_config_id uuid NOT NULL,
    version integer NOT NULL,
    is_debug boolean NOT NULL,
    status smallint NOT NULL,
    input text NOT NULL DEFAULT ('{}'::text),
    output text,
    error_message text,
    instance_data text NOT NULL,
    start_time timestamp with time zone,
    end_time timestamp with time zone,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT app_workflow_instance_pkey PRIMARY KEY (id)
);
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS workflow_config_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS version integer NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS is_debug boolean NOT NULL DEFAULT false;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS status smallint NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS input text NOT NULL DEFAULT ('{}'::text);
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS output text;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS error_message text;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS instance_data text NOT NULL DEFAULT '';
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS start_time timestamp with time zone;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS end_time timestamp with time zone;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE app_workflow_instance ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS classify (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    type character varying(10) NOT NULL,
    name character varying(20) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    emoji character varying(10) NOT NULL DEFAULT (''::character varying),
    CONSTRAINT classify_pkey PRIMARY KEY (id)
);
ALTER TABLE classify ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE classify ADD COLUMN IF NOT EXISTS type character varying(10) NOT NULL DEFAULT '';
ALTER TABLE classify ADD COLUMN IF NOT EXISTS name character varying(20) NOT NULL DEFAULT '';
ALTER TABLE classify ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE classify ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE classify ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE classify ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE classify ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE classify ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE classify ADD COLUMN IF NOT EXISTS emoji character varying(10) NOT NULL DEFAULT (''::character varying);

CREATE TABLE IF NOT EXISTS dingtalk_app (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    team_id integer NOT NULL,
    app_key character varying(64) NOT NULL,
    app_secret character varying(128) NOT NULL,
    chatbot_user_id character varying(128) NOT NULL DEFAULT (''::character varying),
    is_disable boolean NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT dingtalk_app_pkey PRIMARY KEY (id)
);
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS app_key character varying(64) NOT NULL DEFAULT '';
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS app_secret character varying(128) NOT NULL DEFAULT '';
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS chatbot_user_id character varying(128) NOT NULL DEFAULT (''::character varying);
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE dingtalk_app ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS dingtalk_app_binding (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    dingtalk_app_id uuid NOT NULL,
    channel_type integer NOT NULL,
    channel_id character varying(64) NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT dingtalk_app_binding_pkey PRIMARY KEY (id)
);
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS dingtalk_app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS channel_type integer NOT NULL DEFAULT 0;
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS channel_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE dingtalk_app_binding ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS external_user (
    id bigint NOT NULL DEFAULT (nextval('external_id_seq'::regclass)),
    team_id integer NOT NULL,
    app_id uuid,
    access_app_id uuid,
    external_user_id character varying(128) NOT NULL,
    nickname character varying(100),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT external_user_pkey PRIMARY KEY (id)
);
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS id bigint NOT NULL DEFAULT (nextval('external_id_seq'::regclass));
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS app_id uuid;
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS access_app_id uuid;
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS external_user_id character varying(128) NOT NULL DEFAULT '';
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS nickname character varying(100);
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE external_user ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS feishu_app (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    team_id integer NOT NULL,
    app_id character varying(64) NOT NULL,
    app_secret character varying(128) NOT NULL,
    domain character varying(100) NOT NULL DEFAULT ('https://open.feishu.cn'::character varying),
    is_disable boolean NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT feishu_app_pkey PRIMARY KEY (id)
);
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS app_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS app_secret character varying(128) NOT NULL DEFAULT '';
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS domain character varying(100) NOT NULL DEFAULT ('https://open.feishu.cn'::character varying);
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE feishu_app ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS feishu_app_binding (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    feishu_app_id uuid NOT NULL,
    channel_type integer NOT NULL,
    channel_id character varying(64) NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT feishu_app_binding_pkey PRIMARY KEY (id)
);
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS feishu_app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS channel_type integer NOT NULL DEFAULT 0;
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS channel_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE feishu_app_binding ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS file (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    object_key character varying(1024) NOT NULL,
    file_extension character varying(10) NOT NULL DEFAULT (''::character varying),
    file_sha256 bytea NOT NULL,
    file_size integer NOT NULL,
    content_type character varying(255) NOT NULL,
    is_uploaded boolean NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT file_pkey PRIMARY KEY (id)
);
ALTER TABLE file ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE file ADD COLUMN IF NOT EXISTS object_key character varying(1024) NOT NULL DEFAULT '';
ALTER TABLE file ADD COLUMN IF NOT EXISTS file_extension character varying(10) NOT NULL DEFAULT (''::character varying);
ALTER TABLE file ADD COLUMN IF NOT EXISTS file_sha256 bytea NOT NULL DEFAULT ''::bytea;
ALTER TABLE file ADD COLUMN IF NOT EXISTS file_size integer NOT NULL DEFAULT 0;
ALTER TABLE file ADD COLUMN IF NOT EXISTS content_type character varying(255) NOT NULL DEFAULT '';
ALTER TABLE file ADD COLUMN IF NOT EXISTS is_uploaded boolean NOT NULL DEFAULT false;
ALTER TABLE file ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE file ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE file ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE file ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE file ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS kg_import_job (
    id bigint GENERATED ALWAYS AS IDENTITY,
    knowledge_graph_id bigint NOT NULL,
    team_id integer NOT NULL,
    type character varying(20) NOT NULL,
    file_name character varying(255) NOT NULL,
    object_key character varying(500) NOT NULL,
    ai_model_id uuid,
    status character varying(20) NOT NULL,
    total_rows integer NOT NULL,
    done_rows integer NOT NULL,
    error_json text,
    result_json text,
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT kg_import_job_pkey PRIMARY KEY (id)
);
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS id bigint GENERATED ALWAYS AS IDENTITY;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS knowledge_graph_id bigint NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS type character varying(20) NOT NULL DEFAULT '';
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS file_name character varying(255) NOT NULL DEFAULT '';
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS object_key character varying(500) NOT NULL DEFAULT '';
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS ai_model_id uuid;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS status character varying(20) NOT NULL DEFAULT '';
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS total_rows integer NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS done_rows integer NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS error_json text;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS result_json text;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE kg_import_job ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS knowledge_graph (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    team_id integer NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    mode character varying(20) NOT NULL DEFAULT ('managed'::character varying),
    database character varying(100),
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    embedding_model_id uuid,
    embedding_dimensions integer NOT NULL DEFAULT 1024,
    extract_model_id uuid,
    canvas_style_json text NOT NULL DEFAULT (''::character varying),
    is_mcp_enabled boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT knowledge_graph_pkey PRIMARY KEY (id)
);
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS mode character varying(20) NOT NULL DEFAULT ('managed'::character varying);
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS database character varying(100);
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS embedding_model_id uuid;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS embedding_dimensions integer NOT NULL DEFAULT 1024;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS extract_model_id uuid;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS canvas_style_json text NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS is_mcp_enabled boolean NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS knowledge_graph_entity_type (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    knowledge_graph_id bigint NOT NULL,
    name character varying(50) NOT NULL,
    color character varying(20) NOT NULL DEFAULT (''::character varying),
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    sort integer NOT NULL,
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    properties jsonb NOT NULL DEFAULT ('[]'::jsonb),
    CONSTRAINT knowledge_graph_entity_type_pkey PRIMARY KEY (id)
);
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS knowledge_graph_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS color character varying(20) NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS sort integer NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE knowledge_graph_entity_type ADD COLUMN IF NOT EXISTS properties jsonb NOT NULL DEFAULT ('[]'::jsonb);

CREATE TABLE IF NOT EXISTS knowledge_graph_relation_type (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    knowledge_graph_id bigint NOT NULL,
    name character varying(50) NOT NULL,
    color character varying(20) NOT NULL DEFAULT (''::character varying),
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    source_type_id bigint,
    target_type_id bigint,
    sort integer NOT NULL,
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT knowledge_graph_relation_type_pkey PRIMARY KEY (id)
);
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS knowledge_graph_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS color character varying(20) NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS source_type_id bigint;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS target_type_id bigint;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS sort integer NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE knowledge_graph_relation_type ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS login_ip_ban (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    ip text NOT NULL,
    reason text NOT NULL,
    source_rule text NOT NULL,
    expire_time timestamp with time zone NOT NULL,
    is_lifted boolean NOT NULL,
    lift_user_id bigint NOT NULL,
    lift_time timestamp with time zone,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT login_ip_ban_pkey PRIMARY KEY (id)
);
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS ip text NOT NULL DEFAULT '';
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS reason text NOT NULL DEFAULT '';
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS source_rule text NOT NULL DEFAULT '';
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS expire_time timestamp with time zone NOT NULL DEFAULT now();
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS is_lifted boolean NOT NULL DEFAULT false;
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS lift_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS lift_time timestamp with time zone;
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE login_ip_ban ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);

CREATE TABLE IF NOT EXISTS login_log (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    user_id bigint NOT NULL,
    user_name text NOT NULL,
    login_type integer NOT NULL,
    is_success boolean NOT NULL,
    fail_reason text NOT NULL,
    ip text NOT NULL,
    country text NOT NULL,
    province text NOT NULL,
    city text NOT NULL,
    isp text NOT NULL,
    user_agent text NOT NULL,
    risk_level integer NOT NULL,
    risk_reasons text NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT login_log_pkey PRIMARY KEY (id)
);
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS user_name text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS login_type integer NOT NULL DEFAULT 0;
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS is_success boolean NOT NULL DEFAULT false;
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS fail_reason text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS ip text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS country text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS province text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS city text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS isp text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS user_agent text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS risk_level integer NOT NULL DEFAULT 0;
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS risk_reasons text NOT NULL DEFAULT '';
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE login_log ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);

CREATE TABLE IF NOT EXISTS login_risk_event (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    user_id bigint NOT NULL,
    user_name text NOT NULL,
    rule_code text NOT NULL,
    risk_level integer NOT NULL,
    summary text NOT NULL,
    detail text NOT NULL,
    is_read boolean NOT NULL,
    ip text NOT NULL,
    province text NOT NULL,
    city text NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    CONSTRAINT login_risk_event_pkey PRIMARY KEY (id)
);
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS user_name text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS rule_code text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS risk_level integer NOT NULL DEFAULT 0;
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS summary text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS detail text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS is_read boolean NOT NULL DEFAULT false;
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS ip text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS province text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS city text NOT NULL DEFAULT '';
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE login_risk_event ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);

CREATE TABLE IF NOT EXISTS message (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    recipient_user_id bigint NOT NULL,
    title character varying(256) NOT NULL,
    content character varying(4000) NOT NULL,
    message_type integer NOT NULL,
    source character varying(50) NOT NULL DEFAULT (''::character varying),
    sender_user_id bigint,
    params character varying(1024),
    is_read boolean NOT NULL,
    read_time timestamp with time zone,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_message_primary PRIMARY KEY (id)
);
ALTER TABLE message ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE message ADD COLUMN IF NOT EXISTS recipient_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE message ADD COLUMN IF NOT EXISTS title character varying(256) NOT NULL DEFAULT '';
ALTER TABLE message ADD COLUMN IF NOT EXISTS content character varying(4000) NOT NULL DEFAULT '';
ALTER TABLE message ADD COLUMN IF NOT EXISTS message_type integer NOT NULL DEFAULT 0;
ALTER TABLE message ADD COLUMN IF NOT EXISTS source character varying(50) NOT NULL DEFAULT (''::character varying);
ALTER TABLE message ADD COLUMN IF NOT EXISTS sender_user_id bigint;
ALTER TABLE message ADD COLUMN IF NOT EXISTS params character varying(1024);
ALTER TABLE message ADD COLUMN IF NOT EXISTS is_read boolean NOT NULL DEFAULT false;
ALTER TABLE message ADD COLUMN IF NOT EXISTS read_time timestamp with time zone;
ALTER TABLE message ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE message ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE message ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE message ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE message ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS notification_channel (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    name character varying(100) NOT NULL,
    type integer NOT NULL,
    config character varying(4000) NOT NULL,
    enabled boolean NOT NULL,
    description character varying(500) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_notification_channel_primary PRIMARY KEY (id)
);
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS type integer NOT NULL DEFAULT 0;
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS config character varying(4000) NOT NULL DEFAULT '';
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT false;
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS description character varying(500) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE notification_channel ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS notification_log (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    channel_id bigint NOT NULL,
    channel_name character varying(100) NOT NULL,
    channel_type integer NOT NULL,
    level integer NOT NULL,
    title character varying(256) NOT NULL,
    content character varying(4000) NOT NULL,
    target_user_ids character varying(1000) NOT NULL,
    recipients character varying(1000) NOT NULL DEFAULT (''::character varying),
    status integer NOT NULL,
    error character varying(1000) NOT NULL DEFAULT (''::character varying),
    duration_ms bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_notification_log_primary PRIMARY KEY (id)
);
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS channel_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS channel_name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS channel_type integer NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS level integer NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS title character varying(256) NOT NULL DEFAULT '';
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS content character varying(4000) NOT NULL DEFAULT '';
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS target_user_ids character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS recipients character varying(1000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS status integer NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS error character varying(1000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS duration_ms bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE notification_log ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS notification_template (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    code character varying(64) NOT NULL DEFAULT (''::character varying),
    name character varying(100) NOT NULL,
    title character varying(256) NOT NULL DEFAULT (''::character varying),
    content character varying(10000) NOT NULL,
    content_type integer NOT NULL DEFAULT (0),
    variables character varying(4000) NOT NULL DEFAULT (''::character varying),
    aliyun_template_code character varying(128) NOT NULL DEFAULT (''::character varying),
    tencent_template_id character varying(128) NOT NULL DEFAULT (''::character varying),
    description character varying(500) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_notification_template_primary PRIMARY KEY (id)
);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS code character varying(64) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS title character varying(256) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS content character varying(10000) NOT NULL DEFAULT '';
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS content_type integer NOT NULL DEFAULT (0);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS variables character varying(4000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS aliyun_template_code character varying(128) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS tencent_template_id character varying(128) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS description character varying(500) NOT NULL DEFAULT (''::character varying);
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE notification_template ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS oauth_connection (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    name character varying(50) NOT NULL,
    provider character varying(20) NOT NULL,
    key character varying(100) NOT NULL,
    secret character varying(100) NOT NULL,
    icon_url character varying(1000) NOT NULL,
    authorize_url character varying(1000) NOT NULL,
    well_known character varying(1000) NOT NULL,
    agent_id character varying(100),
    is_visible boolean NOT NULL DEFAULT TRUE,
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    update_user_id bigint NOT NULL,
    CONSTRAINT oauth_connection_pkey PRIMARY KEY (id)
);
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS provider character varying(20) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS key character varying(100) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS secret character varying(100) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS icon_url character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS authorize_url character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS well_known character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS agent_id character varying(100);
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS is_visible boolean NOT NULL DEFAULT TRUE;
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE oauth_connection ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS org_grant (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    resource_type integer NOT NULL,
    resource_id character varying(64) NOT NULL,
    org_role_id integer NOT NULL,
    scope integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT idx_org_grant_primary PRIMARY KEY (id)
);
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS resource_type integer NOT NULL DEFAULT 0;
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS resource_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS org_role_id integer NOT NULL DEFAULT 0;
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS scope integer NOT NULL DEFAULT 0;
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE org_grant ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS org_role (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    name character varying(50) NOT NULL,
    parent_id integer,
    path character varying(255) NOT NULL DEFAULT (''::text),
    sort_order integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_org_role_primary PRIMARY KEY (id)
);
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS parent_id integer;
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS path character varying(255) NOT NULL DEFAULT (''::text);
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS sort_order integer NOT NULL DEFAULT 0;
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE org_role ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS org_user_role (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    user_id bigint NOT NULL,
    role_id integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT idx_org_user_role_primary PRIMARY KEY (id)
);
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS role_id integer NOT NULL DEFAULT 0;
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE org_user_role ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS plugin (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    is_system boolean NOT NULL,
    team_id integer NOT NULL,
    plugin_id uuid NOT NULL,
    plugin_name character varying(50) NOT NULL,
    title character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    type integer NOT NULL,
    classify_id integer NOT NULL,
    is_public boolean NOT NULL,
    counter integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    usage_notes text NOT NULL DEFAULT (''::text),
    CONSTRAINT plugin_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS is_system boolean NOT NULL DEFAULT false;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS plugin_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS plugin_name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS title character varying(50) NOT NULL DEFAULT '';
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS type integer NOT NULL DEFAULT 0;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS classify_id integer NOT NULL DEFAULT 0;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS is_public boolean NOT NULL DEFAULT false;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS counter integer NOT NULL DEFAULT 0;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE plugin ADD COLUMN IF NOT EXISTS usage_notes text NOT NULL DEFAULT (''::text);

CREATE TABLE IF NOT EXISTS plugin_custom (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    server character varying(255) NOT NULL,
    headers text NOT NULL,
    queries text NOT NULL,
    type integer NOT NULL,
    openapi_file_id bigint NOT NULL,
    openapi_file_name character varying(255) NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT plugin_custom_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS server character varying(255) NOT NULL DEFAULT '';
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS headers text NOT NULL DEFAULT '';
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS queries text NOT NULL DEFAULT '';
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS type integer NOT NULL DEFAULT 0;
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS openapi_file_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS openapi_file_name character varying(255) NOT NULL DEFAULT '';
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_custom ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS plugin_dynamic (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    plugin_key character varying(50) NOT NULL,
    config text NOT NULL DEFAULT ('{}'::text),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    templete_key character varying(30) NOT NULL,
    CONSTRAINT plugin_dynamic_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS plugin_key character varying(50) NOT NULL DEFAULT '';
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS config text NOT NULL DEFAULT ('{}'::text);
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE plugin_dynamic ADD COLUMN IF NOT EXISTS templete_key character varying(30) NOT NULL DEFAULT '';

CREATE TABLE IF NOT EXISTS plugin_function (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    plugin_custom_id uuid NOT NULL,
    name character varying(255) NOT NULL,
    summary character varying(1000) NOT NULL DEFAULT (''::character varying),
    path character varying(255) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT plugin_function_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS plugin_custom_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS name character varying(255) NOT NULL DEFAULT '';
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS summary character varying(1000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_function ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS plugin_package (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    platform_plugin_id bigint NOT NULL,
    platform_version_id bigint NOT NULL,
    plugin_key character varying(64) NOT NULL,
    version character varying(32) NOT NULL,
    package_hash character varying(64) NOT NULL,
    entry_assembly character varying(512) NOT NULL,
    manifest_json text NOT NULL,
    metadata_json text NOT NULL,
    name character varying(100) NOT NULL,
    summary character varying(512) NOT NULL,
    description text NOT NULL,
    avatar_path character varying(255) NOT NULL DEFAULT '',
    enabled boolean NOT NULL DEFAULT TRUE,
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL DEFAULT 0,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT 0,
    CONSTRAINT plugin_package_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS platform_plugin_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS platform_version_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS plugin_key character varying(64) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS version character varying(32) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS package_hash character varying(64) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS entry_assembly character varying(512) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS manifest_json text NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS metadata_json text NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS summary character varying(512) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS description text NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT '';
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT TRUE;
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_package ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS plugin_semantic_note (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    plugin_id uuid NOT NULL,
    tool_key character varying(200) NOT NULL DEFAULT (''::text),
    title character varying(100) NOT NULL,
    content text NOT NULL DEFAULT (''::text),
    mode integer NOT NULL DEFAULT (0),
    sort_order integer NOT NULL DEFAULT (0),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT plugin_semantic_note_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS plugin_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS tool_key character varying(200) NOT NULL DEFAULT (''::text);
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS title character varying(100) NOT NULL DEFAULT '';
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS content text NOT NULL DEFAULT (''::text);
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS mode integer NOT NULL DEFAULT (0);
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS sort_order integer NOT NULL DEFAULT (0);
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_semantic_note ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS plugin_static (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    plugin_key character varying(50) NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT plugin_static_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS plugin_key character varying(50) NOT NULL DEFAULT '';
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_static ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS plugin_team_authorization (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    plugin_id uuid NOT NULL,
    team_id integer NOT NULL,
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT plugin_team_authorization_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS plugin_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_team_authorization ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS plugin_tool_meta (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    plugin_id uuid NOT NULL,
    tool_key character varying(200) NOT NULL,
    extra_notes text NOT NULL DEFAULT (''::text),
    call_policy text NOT NULL DEFAULT (''::text),
    read_only_hint boolean NOT NULL DEFAULT (false),
    destructive_hint boolean NOT NULL DEFAULT (false),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT plugin_tool_meta_pkey PRIMARY KEY (id)
);
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS plugin_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS tool_key character varying(200) NOT NULL DEFAULT '';
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS extra_notes text NOT NULL DEFAULT (''::text);
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS call_policy text NOT NULL DEFAULT (''::text);
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS read_only_hint boolean NOT NULL DEFAULT (false);
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS destructive_hint boolean NOT NULL DEFAULT (false);
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE plugin_tool_meta ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS prompt (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    name character varying(20) NOT NULL,
    description character varying(255) NOT NULL,
    content text NOT NULL,
    prompt_class_id integer NOT NULL,
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    is_public boolean NOT NULL,
    is_audit boolean NOT NULL,
    counter integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    team_id integer NOT NULL,
    use_count integer NOT NULL,
    CONSTRAINT idx_prompt_primary PRIMARY KEY (id)
);
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS name character varying(20) NOT NULL DEFAULT '';
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT '';
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS content text NOT NULL DEFAULT '';
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS prompt_class_id integer NOT NULL DEFAULT 0;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS is_public boolean NOT NULL DEFAULT false;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS is_audit boolean NOT NULL DEFAULT false;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS counter integer NOT NULL DEFAULT 0;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE prompt ADD COLUMN IF NOT EXISTS use_count integer NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS publication_review (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    resource_type integer NOT NULL,
    resource_id character varying(64) NOT NULL,
    resource_name character varying(100) NOT NULL DEFAULT (''::character varying),
    team_id integer NOT NULL,
    apply_reason character varying(255) NOT NULL DEFAULT (''::character varying),
    publish_version integer,
    visible_scope smallint NOT NULL DEFAULT 0,
    visible_org_grants character varying(2000) NOT NULL DEFAULT (''::character varying),
    state integer NOT NULL,
    review_comment character varying(255) NOT NULL DEFAULT (''::character varying),
    review_time timestamp with time zone,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT publication_review_pkey PRIMARY KEY (id)
);
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS resource_type integer NOT NULL DEFAULT 0;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS resource_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS resource_name character varying(100) NOT NULL DEFAULT (''::character varying);
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS apply_reason character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS publish_version integer;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS visible_scope smallint NOT NULL DEFAULT 0;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS visible_org_grants character varying(2000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS state integer NOT NULL DEFAULT 0;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS review_comment character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS review_time timestamp with time zone;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE publication_review ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS resource_usage_daily (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    usage_date date NOT NULL,
    usage_hour integer NOT NULL DEFAULT ((-1)),
    name character varying(100) NOT NULL,
    resource_id character varying(128) NOT NULL,
    count bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT idx_resource_usage_daily_primary PRIMARY KEY (id)
);
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS usage_date date NOT NULL DEFAULT CURRENT_DATE;
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS usage_hour integer NOT NULL DEFAULT ((-1));
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS resource_id character varying(128) NOT NULL DEFAULT '';
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS count bigint NOT NULL DEFAULT 0;
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE resource_usage_daily ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS resource_usage_total (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    name character varying(100) NOT NULL,
    resource_id character varying(128) NOT NULL,
    total bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT idx_resource_usage_total_primary PRIMARY KEY (id)
);
ALTER TABLE resource_usage_total ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE resource_usage_total ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE resource_usage_total ADD COLUMN IF NOT EXISTS resource_id character varying(128) NOT NULL DEFAULT '';
ALTER TABLE resource_usage_total ADD COLUMN IF NOT EXISTS total bigint NOT NULL DEFAULT 0;
ALTER TABLE resource_usage_total ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE resource_usage_total ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS setting (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    name character varying(20) NOT NULL DEFAULT (''::character varying),
    key character varying(50) NOT NULL,
    value character varying(2000) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT setting_pkey PRIMARY KEY (id)
);
ALTER TABLE setting ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE setting ADD COLUMN IF NOT EXISTS name character varying(20) NOT NULL DEFAULT (''::character varying);
ALTER TABLE setting ADD COLUMN IF NOT EXISTS key character varying(50) NOT NULL DEFAULT '';
ALTER TABLE setting ADD COLUMN IF NOT EXISTS value character varying(2000) NOT NULL DEFAULT '';
ALTER TABLE setting ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE setting ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE setting ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE setting ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE setting ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE setting ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS skill (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    key character varying(30) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    instructions text NOT NULL DEFAULT (''::text),
    files text NOT NULL DEFAULT ('[]'::text),
    is_system boolean NOT NULL,
    team_id integer NOT NULL,
    is_disable boolean NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    is_public boolean NOT NULL,
    classify_id integer NOT NULL,
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    use_count integer NOT NULL,
    CONSTRAINT skill_pkey PRIMARY KEY (id)
);
ALTER TABLE skill ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE skill ADD COLUMN IF NOT EXISTS key character varying(30) NOT NULL DEFAULT '';
ALTER TABLE skill ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE skill ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE skill ADD COLUMN IF NOT EXISTS instructions text NOT NULL DEFAULT (''::text);
ALTER TABLE skill ADD COLUMN IF NOT EXISTS files text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE skill ADD COLUMN IF NOT EXISTS is_system boolean NOT NULL DEFAULT false;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE skill ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE skill ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS is_public boolean NOT NULL DEFAULT false;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS classify_id integer NOT NULL DEFAULT 0;
ALTER TABLE skill ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE skill ADD COLUMN IF NOT EXISTS use_count integer NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS subsystem_team_auth (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    subsystem_id uuid NOT NULL,
    team_id integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_subsystem_team_auth_primary PRIMARY KEY (id)
);
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS subsystem_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE subsystem_team_auth ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS team (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    is_disable boolean NOT NULL,
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT team_pkey PRIMARY KEY (id)
);
ALTER TABLE team ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE team ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE team ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE team ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE team ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE team ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS team_agent (
    id uuid NOT NULL,
    team_id bigint NOT NULL,
    key character varying(30) NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(500) NOT NULL DEFAULT (''::character varying),
    avatar_object_key character varying(255) NOT NULL DEFAULT (''::character varying),
    enabled boolean NOT NULL,
    is_system boolean NOT NULL,
    main_prompt text NOT NULL DEFAULT (''::text),
    main_plugin_ids text NOT NULL DEFAULT ('[]'::text),
    main_wiki_ids text NOT NULL DEFAULT ('[]'::text),
    main_graph_ids text NOT NULL DEFAULT ('[]'::text),
    subs text NOT NULL DEFAULT ('[]'::text),
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT team_agent_pkey PRIMARY KEY (id)
);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS team_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS key character varying(30) NOT NULL DEFAULT '';
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS description character varying(500) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS avatar_object_key character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS enabled boolean NOT NULL DEFAULT false;
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS is_system boolean NOT NULL DEFAULT false;
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS main_prompt text NOT NULL DEFAULT (''::text);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS main_plugin_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS main_wiki_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS main_graph_ids text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS subs text NOT NULL DEFAULT ('[]'::text);
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_agent ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS team_timer (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    name character varying(100) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    app_id uuid NOT NULL,
    app_type integer NOT NULL,
    cron_expression character varying(100) NOT NULL,
    input_content text NOT NULL,
    is_enabled boolean NOT NULL,
    max_retry_count integer NOT NULL,
    retry_interval_seconds integer NOT NULL DEFAULT 60,
    last_run_time timestamp with time zone,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT team_timer_pkey PRIMARY KEY (id)
);
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS name character varying(100) NOT NULL DEFAULT '';
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS app_type integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS cron_expression character varying(100) NOT NULL DEFAULT '';
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS input_content text NOT NULL DEFAULT '';
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS is_enabled boolean NOT NULL DEFAULT false;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS max_retry_count integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS retry_interval_seconds integer NOT NULL DEFAULT 60;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS last_run_time timestamp with time zone;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE team_timer ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS team_timer_execution (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    timer_id uuid NOT NULL,
    team_id integer NOT NULL,
    trigger_type integer NOT NULL,
    trigger_user_id bigint NOT NULL,
    status integer NOT NULL,
    attempt_count integer NOT NULL,
    start_time timestamp with time zone,
    end_time timestamp with time zone,
    duration_ms bigint,
    next_retry_time timestamp with time zone,
    app_instance_id character varying(64),
    session_id uuid,
    result text,
    error_message text,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT team_timer_execution_pkey PRIMARY KEY (id)
);
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS timer_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS trigger_type integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS trigger_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS status integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS attempt_count integer NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS start_time timestamp with time zone;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS end_time timestamp with time zone;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS duration_ms bigint;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS next_retry_time timestamp with time zone;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS app_instance_id character varying(64);
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS session_id uuid;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS result text;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS error_message text;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE team_timer_execution ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS team_user (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    team_id integer NOT NULL,
    user_id bigint NOT NULL,
    role integer NOT NULL,
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT team_user_pkey PRIMARY KEY (id)
);
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS role integer NOT NULL DEFAULT 0;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_user ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS team_variable (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    team_id bigint NOT NULL,
    name character varying(50) NOT NULL DEFAULT (''::character varying),
    key character varying(100) NOT NULL,
    value text NOT NULL DEFAULT (''::text),
    is_secret boolean NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    is_deleted bigint NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT team_variable_pkey PRIMARY KEY (id)
);
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS team_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS key character varying(100) NOT NULL DEFAULT '';
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS value text NOT NULL DEFAULT (''::text);
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS is_secret boolean NOT NULL DEFAULT false;
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE team_variable ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS teamres_key (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    team_id integer NOT NULL,
    resource_type integer NOT NULL DEFAULT 1,
    resource_id character varying(64) NOT NULL,
    key character varying(255) NOT NULL,
    key_prefix character varying(16) NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_teamres_key_primary PRIMARY KEY (id)
);
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS resource_type integer NOT NULL DEFAULT 1;
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS resource_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS key character varying(255) NOT NULL DEFAULT '';
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS key_prefix character varying(16) NOT NULL DEFAULT '';
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE teamres_key ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS "user" (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    user_name character varying(50) NOT NULL,
    email character varying(255) NOT NULL,
    password character varying(1000) NOT NULL,
    nick_name character varying(50) NOT NULL,
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    phone character varying(20) NOT NULL,
    totp_secret character varying(200),
    is_disable boolean NOT NULL,
    is_admin boolean NOT NULL,
    is_totp_enabled boolean NOT NULL,
    auth_version bigint NOT NULL DEFAULT ('0'::bigint),
    password_salt character varying(1000) NOT NULL,
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    CONSTRAINT user_pkey PRIMARY KEY (id)
);
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS user_name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS email character varying(255) NOT NULL DEFAULT '';
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS password character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS nick_name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS phone character varying(20) NOT NULL DEFAULT '';
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS totp_secret character varying(200);
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS is_admin boolean NOT NULL DEFAULT false;
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS is_totp_enabled boolean NOT NULL DEFAULT false;
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS auth_version bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS password_salt character varying(1000) NOT NULL DEFAULT '';
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE "user" ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));

CREATE TABLE IF NOT EXISTS user_oauth_connection (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    user_id bigint NOT NULL,
    provider_id uuid NOT NULL,
    sub character varying(50) NOT NULL,
    union_id character varying(100),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT user_oauth_connection_pkey PRIMARY KEY (id)
);
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS provider_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS sub character varying(50) NOT NULL DEFAULT '';
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS union_id character varying(100);
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE user_oauth_connection ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS weixinwork_app (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    team_id integer NOT NULL,
    corp_id character varying(64) NOT NULL,
    corp_secret character varying(128) NOT NULL,
    agent_id integer NOT NULL,
    token character varying(128) NOT NULL,
    encoding_aes_key character varying(64) NOT NULL,
    api_endpoint character varying(200) NOT NULL DEFAULT (''::character varying),
    is_disable boolean NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT weixinwork_app_pkey PRIMARY KEY (id)
);
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS corp_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS corp_secret character varying(128) NOT NULL DEFAULT '';
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS agent_id integer NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS token character varying(128) NOT NULL DEFAULT '';
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS encoding_aes_key character varying(64) NOT NULL DEFAULT '';
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS api_endpoint character varying(200) NOT NULL DEFAULT (''::character varying);
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS is_disable boolean NOT NULL DEFAULT false;
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE weixinwork_app ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS weixinwork_app_binding (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    weixinwork_app_id uuid NOT NULL,
    channel_type integer NOT NULL,
    channel_id character varying(64) NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT weixinwork_app_binding_pkey PRIMARY KEY (id)
);
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS weixinwork_app_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS channel_type integer NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS channel_id character varying(64) NOT NULL DEFAULT '';
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE weixinwork_app_binding ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS wiki (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    name character varying(20) NOT NULL,
    description character varying(255) NOT NULL,
    counter integer NOT NULL,
    index_epoch bigint NOT NULL DEFAULT 0,
    is_lock boolean NOT NULL,
    embedding_model_id uuid NOT NULL,
    embedding_dimensions integer NOT NULL DEFAULT 1024,
    avatar_path character varying(255) NOT NULL DEFAULT (''::character varying),
    team_id integer NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    rerank_model_id uuid,
    default_workflow_config text DEFAULT (''::text),
    is_mcp_enabled boolean NOT NULL DEFAULT FALSE,
    CONSTRAINT idx_wiki_primary PRIMARY KEY (id)
);
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS name character varying(20) NOT NULL DEFAULT '';
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT '';
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS counter integer NOT NULL DEFAULT 0;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS index_epoch bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS is_lock boolean NOT NULL DEFAULT false;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS embedding_model_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS embedding_dimensions integer NOT NULL DEFAULT 1024;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS avatar_path character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS rerank_model_id uuid;
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS default_workflow_config text DEFAULT (''::text);
ALTER TABLE wiki ADD COLUMN IF NOT EXISTS is_mcp_enabled boolean NOT NULL DEFAULT FALSE;

CREATE TABLE IF NOT EXISTS wiki_document (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    wiki_id integer NOT NULL,
    file_id integer NOT NULL,
    object_key character varying(255) NOT NULL,
    file_name character varying(1024) NOT NULL,
    file_type character varying(10) NOT NULL,
    is_embedding boolean NOT NULL,
    version_no bigint NOT NULL DEFAULT ('0'::bigint),
    slice_config text NOT NULL DEFAULT ('{}'::text),
    is_update boolean NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_wiki_document_primary PRIMARY KEY (id)
);
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS file_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS object_key character varying(255) NOT NULL DEFAULT '';
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS file_name character varying(1024) NOT NULL DEFAULT '';
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS file_type character varying(10) NOT NULL DEFAULT '';
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS is_embedding boolean NOT NULL DEFAULT false;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS version_no bigint NOT NULL DEFAULT ('0'::bigint);
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS slice_config text NOT NULL DEFAULT ('{}'::text);
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS is_update boolean NOT NULL DEFAULT false;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS wiki_document_chunk_content (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    wiki_id integer NOT NULL,
    document_id integer NOT NULL,
    slice_content text NOT NULL,
    slice_order integer NOT NULL,
    slice_length integer NOT NULL,
    slice_anchor character varying(512) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_wiki_document_chunk_content_primary PRIMARY KEY (id)
);
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS document_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS slice_content text NOT NULL DEFAULT '';
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS slice_order integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS slice_length integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS slice_anchor character varying(512) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS wiki_document_chunk_metadata (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    wiki_id integer NOT NULL,
    document_id integer NOT NULL,
    chunk_id bigint NOT NULL,
    metadata_type integer NOT NULL,
    metadata_content text NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_wiki_document_chunk_metadata_primary PRIMARY KEY (id)
);
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS document_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS chunk_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS metadata_type integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS metadata_content text NOT NULL DEFAULT '';
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document_chunk_metadata ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS wiki_document_content (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    wiki_id integer NOT NULL,
    document_id integer NOT NULL,
    content text NOT NULL,
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_wiki_document_content_primary PRIMARY KEY (id)
);
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS document_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS content text NOT NULL DEFAULT '';
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document_content ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS wiki_document_image (
    id integer GENERATED BY DEFAULT AS IDENTITY,
    wiki_id integer NOT NULL,
    document_id integer NOT NULL,
    file_id bigint NOT NULL,
    object_key character varying(255) NOT NULL,
    file_name character varying(1024) NOT NULL,
    file_type character varying(10) NOT NULL,
    page_number integer,
    image_index integer NOT NULL DEFAULT ('0'::integer),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_wiki_document_image_primary PRIMARY KEY (id)
);
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS id integer GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS document_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS file_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS object_key character varying(255) NOT NULL DEFAULT '';
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS file_name character varying(1024) NOT NULL DEFAULT '';
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS file_type character varying(10) NOT NULL DEFAULT '';
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS page_number integer;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS image_index integer NOT NULL DEFAULT ('0'::integer);
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_document_image ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS wiki_recall_cluster (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    wiki_id integer NOT NULL,
    query_hash text NOT NULL,
    query text NOT NULL,
    index_epoch bigint NOT NULL,
    hits_json text NOT NULL,
    hit_count integer NOT NULL,
    recall_count integer NOT NULL,
    last_recall_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_wiki_recall_cluster_primary PRIMARY KEY (id)
);
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS query_hash text NOT NULL DEFAULT '';
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS query text NOT NULL DEFAULT '';
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS index_epoch bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS hits_json text NOT NULL DEFAULT '';
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS hit_count integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS recall_count integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS last_recall_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (CURRENT_TIMESTAMP);
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_recall_cluster ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE TABLE IF NOT EXISTS wiki_source (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    wiki_id integer NOT NULL,
    team_id integer NOT NULL,
    source_type integer NOT NULL,
    name character varying(50) NOT NULL,
    description character varying(255) NOT NULL DEFAULT (''::character varying),
    config text NOT NULL DEFAULT (''::text),
    workflow_config text NOT NULL DEFAULT (''::text),
    is_enable boolean NOT NULL DEFAULT TRUE,
    cron character varying(64) NOT NULL DEFAULT (''::character varying),
    is_event_subscription boolean NOT NULL,
    last_sync_time timestamp with time zone,
    last_sync_status integer NOT NULL,
    last_sync_message character varying(1000) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    CONSTRAINT wiki_source_pkey PRIMARY KEY (id)
);
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS source_type integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS name character varying(50) NOT NULL DEFAULT '';
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS description character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS config text NOT NULL DEFAULT (''::text);
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS workflow_config text NOT NULL DEFAULT (''::text);
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS is_enable boolean NOT NULL DEFAULT TRUE;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS cron character varying(64) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS is_event_subscription boolean NOT NULL DEFAULT false;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS last_sync_time timestamp with time zone;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS last_sync_status integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS last_sync_message character varying(1000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_source ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;

CREATE TABLE IF NOT EXISTS wiki_source_document (
    id bigint GENERATED BY DEFAULT AS IDENTITY,
    source_id uuid NOT NULL,
    wiki_id integer NOT NULL,
    team_id integer NOT NULL,
    document_id integer NOT NULL,
    external_key character varying(128) NOT NULL,
    external_title character varying(255) NOT NULL DEFAULT (''::character varying),
    external_path character varying(1000) NOT NULL DEFAULT (''::character varying),
    content_hash character varying(64) NOT NULL DEFAULT (''::character varying),
    revision character varying(64) NOT NULL DEFAULT (''::character varying),
    status integer NOT NULL,
    last_sync_time timestamp with time zone,
    last_error character varying(1000) NOT NULL DEFAULT (''::character varying),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL,
    external_doc_token character varying(128) NOT NULL DEFAULT (''::character varying),
    CONSTRAINT wiki_source_document_pkey PRIMARY KEY (id)
);
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS id bigint GENERATED BY DEFAULT AS IDENTITY;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS source_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS wiki_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS document_id integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS external_key character varying(128) NOT NULL DEFAULT '';
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS external_title character varying(255) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS external_path character varying(1000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS content_hash character varying(64) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS revision character varying(64) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS status integer NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS last_sync_time timestamp with time zone;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS last_error character varying(1000) NOT NULL DEFAULT (''::character varying);
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT 0;
ALTER TABLE wiki_source_document ADD COLUMN IF NOT EXISTS external_doc_token character varying(128) NOT NULL DEFAULT (''::character varying);

CREATE TABLE IF NOT EXISTS worker_task (
    id uuid NOT NULL DEFAULT (uuid_generate_v4()),
    bind_type character varying(20) NOT NULL,
    bind_id integer NOT NULL,
    state integer NOT NULL,
    message text NOT NULL,
    data text NOT NULL DEFAULT ('{}'::text),
    create_user_id bigint NOT NULL,
    create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    update_user_id bigint NOT NULL,
    update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now())),
    is_deleted bigint NOT NULL DEFAULT ('0'::bigint),
    CONSTRAINT idx_66048_primary PRIMARY KEY (id)
);
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS id uuid NOT NULL DEFAULT (uuid_generate_v4());
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS bind_type character varying(20) NOT NULL DEFAULT '';
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS bind_id integer NOT NULL DEFAULT 0;
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS state integer NOT NULL DEFAULT 0;
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS message text NOT NULL DEFAULT '';
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS data text NOT NULL DEFAULT ('{}'::text);
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS create_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS create_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS update_user_id bigint NOT NULL DEFAULT 0;
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS update_time timestamp with time zone NOT NULL DEFAULT (timezone('utc'::text, now()));
ALTER TABLE worker_task ADD COLUMN IF NOT EXISTS is_deleted bigint NOT NULL DEFAULT ('0'::bigint);

CREATE INDEX IF NOT EXISTS idx_access_app_key_prefix ON access_app (key_prefix);
CREATE UNIQUE INDEX IF NOT EXISTS idx_access_app_key_uindex ON access_app (key, is_deleted);
CREATE INDEX IF NOT EXISTS idx_ai_channel_protocol_family ON ai_channel (protocol_family);
CREATE INDEX IF NOT EXISTS idx_ai_channel_provider_key ON ai_channel (provider_key);
CREATE INDEX IF NOT EXISTS idx_ai_channel_team_id ON ai_channel (team_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_ai_model_channel_model ON ai_model (channel_id, model_id);
CREATE INDEX IF NOT EXISTS idx_ai_model_model_kind ON ai_model (model_kind);
CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_model_authorization_model_team_uindex ON ai_model_authorization (ai_model_id, team_id) WHERE (is_deleted = 0);
CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_model_token_audit_dim_uindex ON ai_model_token_audit (model_id, team_id, user_id, use_type, use_resource_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_ai_model_usage_log_channel_index ON ai_model_usage_log (channel);
CREATE INDEX IF NOT EXISTS idx_ai_model_usage_log_team_id_index ON ai_model_usage_log (team_id);
CREATE INDEX IF NOT EXISTS idx_app_name_index ON app (name);
CREATE INDEX IF NOT EXISTS idx_app_team_id_index ON app (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_access_point_app_uindex ON app_access_point (app_id) WHERE (is_deleted = 0);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_agent_config_app_id_live_uindex ON app_agent_config (app_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_app_agent_config_team_id ON app_agent_config (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_agent_message_session_seq_uindex ON app_agent_message (session_id, seq);
CREATE INDEX IF NOT EXISTS idx_app_agent_session_app_id ON app_agent_session (app_id);
CREATE INDEX IF NOT EXISTS idx_app_agent_session_app_user_last_index ON app_agent_session (app_id, create_user_id, last_message_time DESC);
CREATE INDEX IF NOT EXISTS idx_app_agent_session_team_id ON app_agent_session (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_favorite_app_user_live_uindex ON app_favorite (app_id, user_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_app_memory_shard_app_id ON app_memory_shard (app_id);
CREATE INDEX IF NOT EXISTS idx_app_memory_shard_user_id ON app_memory_shard (user_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_app_memory_shard_app_user_live_uindex ON app_memory_shard (app_id, user_id, is_deleted) WHERE (is_deleted = 0);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_security_config_app_id_live_uindex ON app_security_config (app_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_app_security_config_team_id ON app_security_config (team_id);
CREATE INDEX IF NOT EXISTS idx_app_security_event_app_id ON app_security_event (app_id);
CREATE INDEX IF NOT EXISTS idx_app_security_event_create_time ON app_security_event (create_time);
CREATE INDEX IF NOT EXISTS idx_app_security_event_team_id ON app_security_event (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_user_config_app_user_live_uindex ON app_user_config (app_id, user_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_app_user_config_team_id ON app_user_config (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_app_workflow_config_app_id_live_uindex ON app_workflow_config (app_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_app_workflow_config_team_id ON app_workflow_config (team_id);
CREATE INDEX IF NOT EXISTS idx_app_workflow_instance_app_id ON app_workflow_instance (app_id);
CREATE INDEX IF NOT EXISTS idx_app_workflow_instance_config_id ON app_workflow_instance (workflow_config_id);
CREATE INDEX IF NOT EXISTS idx_app_workflow_instance_team_id ON app_workflow_instance (team_id);
CREATE INDEX IF NOT EXISTS idx_classify_name_index ON classify (name);
CREATE INDEX IF NOT EXISTS idx_classify_type_index ON classify (type);
CREATE UNIQUE INDEX IF NOT EXISTS idx_dingtalk_app_app_key_uindex ON dingtalk_app (app_key) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_dingtalk_app_team_id_index ON dingtalk_app (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_dingtalk_app_binding_app_uindex ON dingtalk_app_binding (dingtalk_app_id) WHERE (is_deleted = 0 and channel_type = 0);
CREATE INDEX IF NOT EXISTS idx_dingtalk_app_binding_channel_index ON dingtalk_app_binding (channel_type, channel_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_dingtalk_app_binding_channel_uindex ON dingtalk_app_binding (dingtalk_app_id, channel_type, channel_id) WHERE (is_deleted = 0);
CREATE UNIQUE INDEX IF NOT EXISTS idx_external_user_accessapp_user_uindex ON external_user (access_app_id, external_user_id) WHERE ((access_app_id IS NOT NULL) AND (is_deleted = 0));
CREATE UNIQUE INDEX IF NOT EXISTS idx_feishu_app_app_id_uindex ON feishu_app (app_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_feishu_app_team_id_index ON feishu_app (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_feishu_app_binding_app_uindex ON feishu_app_binding (feishu_app_id) WHERE ((is_deleted = 0) AND (channel_type = 0));
CREATE INDEX IF NOT EXISTS idx_feishu_app_binding_channel_index ON feishu_app_binding (channel_type, channel_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_feishu_app_binding_channel_uindex ON feishu_app_binding (feishu_app_id, channel_type, channel_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_file_file_sha256 ON file (file_sha256);
CREATE INDEX IF NOT EXISTS idx_file_object_key ON file (object_key);
CREATE INDEX IF NOT EXISTS idx_kg_import_job_kg_id ON kg_import_job (knowledge_graph_id, id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_knowledge_graph_name_live_uindex ON knowledge_graph (name) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_knowledge_graph_team_id ON knowledge_graph (team_id);
CREATE INDEX IF NOT EXISTS idx_knowledge_graph_entity_type_knowledge_graph_id ON knowledge_graph_entity_type (knowledge_graph_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_knowledge_graph_entity_type_name_live_uindex ON knowledge_graph_entity_type (knowledge_graph_id, name) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_knowledge_graph_relation_type_knowledge_graph_id ON knowledge_graph_relation_type (knowledge_graph_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_knowledge_graph_relation_type_name_live_uindex ON knowledge_graph_relation_type (knowledge_graph_id, name) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_login_ip_ban_ip ON login_ip_ban (ip);
CREATE INDEX IF NOT EXISTS idx_login_log_create_time ON login_log (create_time);
CREATE INDEX IF NOT EXISTS idx_login_log_ip ON login_log (ip);
CREATE INDEX IF NOT EXISTS idx_login_log_user_id ON login_log (user_id);
CREATE INDEX IF NOT EXISTS idx_login_risk_event_create_time ON login_risk_event (create_time);
CREATE INDEX IF NOT EXISTS idx_login_risk_event_is_read ON login_risk_event (is_read);
CREATE INDEX IF NOT EXISTS idx_login_risk_event_user_id ON login_risk_event (user_id);
CREATE INDEX IF NOT EXISTS idx_message_recipient_read ON message (recipient_user_id, is_read) WHERE is_deleted = 0;
CREATE INDEX IF NOT EXISTS idx_message_recipient_time ON message (recipient_user_id, create_time) WHERE is_deleted = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_notification_channel_name_uindex ON notification_channel (name) WHERE is_deleted = 0;
CREATE INDEX IF NOT EXISTS idx_notification_log_channel ON notification_log (channel_id, create_time) WHERE is_deleted = 0;
CREATE INDEX IF NOT EXISTS idx_notification_log_time ON notification_log (create_time) WHERE is_deleted = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_notification_template_code_uindex ON notification_template (code) WHERE is_deleted = 0 AND code <> '';
CREATE UNIQUE INDEX IF NOT EXISTS idx_notification_template_name_uindex ON notification_template (name) WHERE is_deleted = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_org_grant_resource_role_live_uindex ON org_grant (resource_type, resource_id, org_role_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_org_grant_role_id ON org_grant (org_role_id);
CREATE INDEX IF NOT EXISTS idx_org_role_parent_id ON org_role (parent_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_org_role_path_uindex ON org_role (path) WHERE "is_deleted" = 0;
CREATE INDEX IF NOT EXISTS idx_org_user_role_role_id ON org_user_role (role_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_org_user_role_user_role_live_uindex ON org_user_role (user_id, role_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_plugin_plugin_name_index ON plugin (plugin_name);
CREATE INDEX IF NOT EXISTS idx_plugin_title_index ON plugin (title);
CREATE UNIQUE INDEX IF NOT EXISTS idx_plugin_package_key_deleted_uindex ON plugin_package (plugin_key, is_deleted);
CREATE INDEX IF NOT EXISTS idx_plugin_semantic_note_plugin_id ON plugin_semantic_note (plugin_id);
CREATE INDEX IF NOT EXISTS idx_plugin_team_authorization_plugin_id ON plugin_team_authorization (plugin_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_plugin_team_authorization_plugin_team_uindex ON plugin_team_authorization (plugin_id, team_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_plugin_team_authorization_team_id ON plugin_team_authorization (team_id);
CREATE INDEX IF NOT EXISTS idx_plugin_tool_meta_plugin_id ON plugin_tool_meta (plugin_id);
CREATE INDEX IF NOT EXISTS idx_publication_review_team_id ON publication_review (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_publication_review_resource_pending ON publication_review (resource_type, resource_id) WHERE ((is_deleted = 0) AND (state = 0));
CREATE UNIQUE INDEX IF NOT EXISTS idx_resource_usage_daily_date_hour_name_resource_uindex ON resource_usage_daily (usage_date, usage_hour, name, resource_id);
CREATE INDEX IF NOT EXISTS idx_resource_usage_daily_name_date_index ON resource_usage_daily (name, usage_date);
CREATE UNIQUE INDEX IF NOT EXISTS idx_resource_usage_total_name_resource_uindex ON resource_usage_total (name, resource_id);
CREATE INDEX IF NOT EXISTS setting_key_index ON setting USING hash (key);
CREATE UNIQUE INDEX IF NOT EXISTS idx_skill_key_live_uindex ON skill (key) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_skill_team_id ON skill (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_subsystem_team_auth_uindex ON subsystem_team_auth (subsystem_id, team_id) WHERE "is_deleted" = 0;
CREATE UNIQUE INDEX IF NOT EXISTS idx_team_name_live_uindex ON team (name) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_team_agent_team_id ON team_agent (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_team_agent_team_key_live_uindex ON team_agent (team_id, key) WHERE (is_deleted = 0) AND (key <> '');
CREATE UNIQUE INDEX IF NOT EXISTS idx_team_agent_team_name_live_uindex ON team_agent (team_id, name) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_team_timer_team_id ON team_timer (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS ux_team_timer_team_name_live_uindex ON team_timer (team_id, name, is_deleted) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_team_timer_execution_team_id ON team_timer_execution (team_id);
CREATE INDEX IF NOT EXISTS idx_team_timer_execution_timer_create ON team_timer_execution (timer_id, create_time);
CREATE INDEX IF NOT EXISTS idx_team_user_team_id ON team_user (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_team_user_team_user_live_uindex ON team_user (team_id, user_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_team_user_user_id ON team_user (user_id);
CREATE INDEX IF NOT EXISTS idx_team_variable_team_id ON team_variable (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_team_variable_team_key_live_uindex ON team_variable (team_id, key) WHERE (is_deleted = 0);
CREATE UNIQUE INDEX IF NOT EXISTS idx_teamres_key_key_uindex ON teamres_key (key, is_deleted);
CREATE INDEX IF NOT EXISTS idx_teamres_key_resource ON teamres_key (team_id, resource_type, resource_id, is_deleted);
CREATE INDEX IF NOT EXISTS idx_teamres_key_team_id ON teamres_key (team_id);
CREATE INDEX IF NOT EXISTS idx_users_email ON "user" (email);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_email_is_deleted_uindex ON "user" (email, is_deleted);
CREATE INDEX IF NOT EXISTS idx_users_phone ON "user" (phone);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_phone_is_deleted_uindex ON "user" (phone, is_deleted);
CREATE INDEX IF NOT EXISTS idx_users_user_name ON "user" (user_name);
CREATE UNIQUE INDEX IF NOT EXISTS idx_users_user_name_is_deleted_uindex ON "user" (user_name, is_deleted);
CREATE UNIQUE INDEX IF NOT EXISTS idx_user_oauth_provider_id_sub_is_deleted_uindex ON user_oauth_connection (provider_id, sub, is_deleted);
CREATE INDEX IF NOT EXISTS idx_user_oauth_provider_id_union_id ON user_oauth_connection (provider_id, union_id) WHERE union_id IS NOT NULL;
CREATE UNIQUE INDEX IF NOT EXISTS idx_weixinwork_app_corp_agent_uindex ON weixinwork_app (corp_id, agent_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_weixinwork_app_team_id_index ON weixinwork_app (team_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_weixinwork_app_binding_app_uindex ON weixinwork_app_binding (weixinwork_app_id) WHERE (is_deleted = 0 and channel_type = 0);
CREATE INDEX IF NOT EXISTS idx_weixinwork_app_binding_channel_index ON weixinwork_app_binding (channel_type, channel_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_weixinwork_app_binding_channel_uindex ON weixinwork_app_binding (weixinwork_app_id, channel_type, channel_id) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_wiki_document_object_key_index ON wiki_document (object_key);
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_content_doc_slice ON wiki_document_chunk_content (document_id, slice_order);
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_content_preview_doc_slice ON wiki_document_chunk_content (document_id, id);
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_content_preview_wiki_slice ON wiki_document_chunk_content (wiki_id);
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_metadata_deriv_type ON wiki_document_chunk_metadata (metadata_type);
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_metadata_doc_deriv ON wiki_document_chunk_metadata (document_id, metadata_type);
CREATE INDEX IF NOT EXISTS idx_wiki_document_chunk_metadata_slice_deriv ON wiki_document_chunk_metadata (chunk_id, id);
CREATE INDEX IF NOT EXISTS idx_wiki_document_image_document_id_index ON wiki_document_image (wiki_id, document_id) WHERE "is_deleted" = 0;
CREATE INDEX IF NOT EXISTS idx_wiki_recall_cluster_wiki_id ON wiki_recall_cluster (wiki_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_wiki_recall_cluster_wiki_query_uindex ON wiki_recall_cluster (wiki_id, query_hash) WHERE is_deleted = 0;
CREATE INDEX IF NOT EXISTS idx_wiki_source_team_id_index ON wiki_source (team_id);
CREATE INDEX IF NOT EXISTS idx_wiki_source_wiki_id_index ON wiki_source (wiki_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_wiki_source_wiki_name_uindex ON wiki_source (wiki_id, name) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_wiki_source_document_doc_token_index ON wiki_source_document (source_id, external_doc_token);
CREATE INDEX IF NOT EXISTS idx_wiki_source_document_document_id_index ON wiki_source_document (document_id);
CREATE UNIQUE INDEX IF NOT EXISTS idx_wiki_source_document_key_uindex ON wiki_source_document (source_id, external_key) WHERE (is_deleted = 0);
CREATE INDEX IF NOT EXISTS idx_wiki_source_document_source_id_index ON wiki_source_document (source_id);
CREATE INDEX IF NOT EXISTS idx_wiki_source_document_wiki_id_index ON wiki_source_document (wiki_id);
