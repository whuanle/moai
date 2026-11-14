-- 团队资源 key 表 + 团队接入去范围化 + 资源功能开关
-- 与 DatabasPostgresModule 幂等 DDL 等价；存量库手工升级时执行本文件即可
-- @TR-S1 teamres_key 建表（一资源一把 key：app 行=应用 Guid、wiki 行=wikiId、kg 行=kgId，moai-tr- 前缀；key 无启停状态）
-- @TR-S2 access_app.scopes 退役（moai-ac- 不再勾选范围，全量访问 /api/teamres）
-- @TR-S3 资源功能开关（app.is_acp_a2a_enabled 单一开关同管 ACP/A2A、wiki/knowledge_graph.is_mcp_enabled；开启时自动生成/复用该资源的 key）

-- 早期草稿版 teamres_key（resource_id bigint / 含 status 列）仅存在于本分支开发库，按结构判断整体重建/回收列
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_name = 'teamres_key' AND column_name = 'resource_id' AND data_type = 'bigint'
    ) THEN
        DROP TABLE teamres_key;
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS teamres_key (
    id uuid NOT NULL DEFAULT uuid_generate_v4(),
    team_id int NOT NULL DEFAULT 0,
    resource_type int NOT NULL DEFAULT 1,
    resource_id varchar(64) NOT NULL DEFAULT ''::character varying,
    key varchar(255) NOT NULL,
    key_prefix varchar(16) NOT NULL DEFAULT ''::character varying,
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    update_user_id bigint NOT NULL DEFAULT 0,
    update_time timestamptz NOT NULL DEFAULT timezone('utc'::text, now()),
    is_deleted bigint NOT NULL DEFAULT 0,
    CONSTRAINT idx_teamres_key_primary PRIMARY KEY (id)
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_teamres_key_key_uindex ON teamres_key (key, is_deleted);
CREATE INDEX IF NOT EXISTS idx_teamres_key_team_id ON teamres_key (team_id);
CREATE INDEX IF NOT EXISTS idx_teamres_key_resource ON teamres_key (team_id, resource_type, resource_id, is_deleted);

ALTER TABLE teamres_key DROP COLUMN IF EXISTS status;

ALTER TABLE app ADD COLUMN IF NOT EXISTS is_acp_a2a_enabled boolean NOT NULL DEFAULT false;
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns WHERE table_name = 'app' AND column_name = 'is_acp_enabled') THEN
        UPDATE app SET is_acp_a2a_enabled = true WHERE is_acp_enabled OR is_a2a_enabled;
    END IF;
END $$;
ALTER TABLE app DROP COLUMN IF EXISTS is_acp_enabled;
ALTER TABLE app DROP COLUMN IF EXISTS is_a2a_enabled;

ALTER TABLE wiki ADD COLUMN IF NOT EXISTS is_mcp_enabled boolean NOT NULL DEFAULT false;
ALTER TABLE knowledge_graph ADD COLUMN IF NOT EXISTS is_mcp_enabled boolean NOT NULL DEFAULT false;

ALTER TABLE access_app DROP COLUMN IF EXISTS scopes;
