-- 登录安全风控三张表：login_log（登录日志）/ login_risk_event（风险事件）/ login_ip_ban（IP 封禁）
-- 后加表：EnsureCreated 只在建库时建表，存量库执行本脚本补齐（启动时 DatabasPostgresModule 也会幂等补建，免手工执行）

CREATE TABLE IF NOT EXISTS login_log (
    id uuid NOT NULL DEFAULT uuid_generate_v4(),
    user_id bigint NOT NULL DEFAULT 0,
    user_name varchar(200) NOT NULL DEFAULT ''::character varying,
    login_type int NOT NULL DEFAULT 0,
    is_success boolean NOT NULL DEFAULT false,
    fail_reason varchar(200) NOT NULL DEFAULT ''::character varying,
    ip varchar(64) NOT NULL DEFAULT ''::character varying,
    country varchar(64) NOT NULL DEFAULT ''::character varying,
    province varchar(64) NOT NULL DEFAULT ''::character varying,
    city varchar(64) NOT NULL DEFAULT ''::character varying,
    isp varchar(128) NOT NULL DEFAULT ''::character varying,
    user_agent varchar(500) NOT NULL DEFAULT ''::character varying,
    risk_level int NOT NULL DEFAULT 0,
    risk_reasons varchar(2000) NOT NULL DEFAULT ''::character varying,
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    CONSTRAINT login_log_pkey PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS idx_login_log_user_id ON login_log (user_id);
CREATE INDEX IF NOT EXISTS idx_login_log_ip ON login_log (ip);
CREATE INDEX IF NOT EXISTS idx_login_log_create_time ON login_log (create_time);

CREATE TABLE IF NOT EXISTS login_risk_event (
    id uuid NOT NULL DEFAULT uuid_generate_v4(),
    user_id bigint NOT NULL DEFAULT 0,
    user_name varchar(200) NOT NULL DEFAULT ''::character varying,
    rule_code varchar(8) NOT NULL DEFAULT ''::character varying,
    risk_level int NOT NULL DEFAULT 0,
    summary varchar(1000) NOT NULL DEFAULT ''::character varying,
    detail varchar(2000) NOT NULL DEFAULT ''::character varying,
    is_read boolean NOT NULL DEFAULT false,
    ip varchar(64) NOT NULL DEFAULT ''::character varying,
    province varchar(64) NOT NULL DEFAULT ''::character varying,
    city varchar(64) NOT NULL DEFAULT ''::character varying,
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    CONSTRAINT login_risk_event_pkey PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS idx_login_risk_event_user_id ON login_risk_event (user_id);
CREATE INDEX IF NOT EXISTS idx_login_risk_event_is_read ON login_risk_event (is_read);
CREATE INDEX IF NOT EXISTS idx_login_risk_event_create_time ON login_risk_event (create_time);

CREATE TABLE IF NOT EXISTS login_ip_ban (
    id uuid NOT NULL DEFAULT uuid_generate_v4(),
    ip varchar(64) NOT NULL DEFAULT ''::character varying,
    reason varchar(200) NOT NULL DEFAULT ''::character varying,
    source_rule varchar(32) NOT NULL DEFAULT ''::character varying,
    expire_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    is_lifted boolean NOT NULL DEFAULT false,
    lift_user_id bigint NOT NULL DEFAULT 0,
    lift_time timestamptz NULL,
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    CONSTRAINT login_ip_ban_pkey PRIMARY KEY (id)
);
CREATE INDEX IF NOT EXISTS idx_login_ip_ban_ip ON login_ip_ban (ip);
