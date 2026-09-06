-- 团队模型网关 API 密钥表（模型 API 转发功能）
-- 表结构以 src/database/MoAI.Database.Postgres/Data/TeamApiKeyConfiguration.cs 为准
-- 约定：expire_time=9999-12-31 23:59:59+00 表示永不过期；last_used_time='0001-01-01 00:00:00+00' 表示从未使用

CREATE TABLE IF NOT EXISTS team_api_key
(
    id              uuid         NOT NULL DEFAULT uuid_generate_v4(),
    team_id         integer      NOT NULL,
    name            varchar(100) NOT NULL,
    key_prefix      varchar(32)  NOT NULL,
    key_sha256      bytea        NOT NULL,
    creator_user_id bigint       NOT NULL DEFAULT 0,
    is_disable      boolean      NOT NULL DEFAULT false,
    expire_time     timestamptz  NOT NULL DEFAULT '9999-12-31 23:59:59+00',
    last_used_time  timestamptz  NOT NULL DEFAULT '0001-01-01 00:00:00+00',
    create_user_id  bigint       NOT NULL DEFAULT 0,
    create_time     timestamptz  NOT NULL DEFAULT CURRENT_TIMESTAMP,
    update_user_id  bigint       NOT NULL DEFAULT 0,
    update_time     timestamptz  NOT NULL DEFAULT timezone('utc'::text, now()),
    is_deleted      bigint       NOT NULL DEFAULT 0,
    CONSTRAINT idx_team_api_key_primary PRIMARY KEY (id)
);

COMMENT ON TABLE team_api_key IS '团队模型网关API密钥，团队管理员创建并维护，团队成员使用密钥通过 /v1 开放接口调用团队已授权的模型';
COMMENT ON COLUMN team_api_key.id IS 'id';
COMMENT ON COLUMN team_api_key.team_id IS '所属团队id';
COMMENT ON COLUMN team_api_key.name IS '密钥名称';
COMMENT ON COLUMN team_api_key.key_prefix IS '密钥前缀，仅用于列表展示，如 moai-Ab12CdEf';
COMMENT ON COLUMN team_api_key.key_sha256 IS '密钥的sha256，密钥原文不落库';
COMMENT ON COLUMN team_api_key.creator_user_id IS '创建密钥的管理员用户id，密钥的可用性与该用户状态绑定';
COMMENT ON COLUMN team_api_key.is_disable IS '是否禁用';
COMMENT ON COLUMN team_api_key.expire_time IS '过期时间，9999-12-31 23:59:59+00=永不过期';
COMMENT ON COLUMN team_api_key.last_used_time IS '最近一次调用时间，0001-01-01 00:00:00+00=创建后从未使用';
COMMENT ON COLUMN team_api_key.create_user_id IS '创建人';
COMMENT ON COLUMN team_api_key.create_time IS '创建时间';
COMMENT ON COLUMN team_api_key.update_user_id IS '更新人';
COMMENT ON COLUMN team_api_key.update_time IS '更新时间';
COMMENT ON COLUMN team_api_key.is_deleted IS '软删除';

CREATE UNIQUE INDEX IF NOT EXISTS idx_team_api_key_sha256_uindex
    ON team_api_key (key_sha256)
    WHERE (is_deleted = 0);

CREATE INDEX IF NOT EXISTS idx_team_api_key_team_id_index
    ON team_api_key (team_id);
