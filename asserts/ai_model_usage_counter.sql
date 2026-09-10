-- AI 模型用量累计字段可能长期增长，统一升级为 PostgreSQL bigint。
-- PostgreSQL 允许对已经是 bigint 的列重复执行 TYPE bigint。

DO $$
BEGIN
    IF to_regclass('public.ai_model_useage_log') IS NOT NULL
        AND to_regclass('public.ai_model_usage_log') IS NULL THEN
        ALTER TABLE ai_model_useage_log RENAME TO ai_model_usage_log;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
            AND table_name = 'ai_model_token_audit'
            AND column_name = 'useri_id')
        AND NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public'
                AND table_name = 'ai_model_token_audit'
                AND column_name = 'user_id') THEN
        ALTER TABLE ai_model_token_audit RENAME COLUMN useri_id TO user_id;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
            AND table_name = 'ai_model_usage_log'
            AND column_name = 'useri_id')
        AND NOT EXISTS (
            SELECT 1 FROM information_schema.columns
            WHERE table_schema = 'public'
                AND table_name = 'ai_model_usage_log'
                AND column_name = 'user_id') THEN
        ALTER TABLE ai_model_usage_log RENAME COLUMN useri_id TO user_id;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.columns
        WHERE table_schema = 'public'
            AND table_name = 'ai_model_token_audit'
            AND column_name = 'model_id'
            AND data_type <> 'uuid') THEN
        RAISE EXCEPTION 'ai_model_token_audit.model_id 必须先随 ai_model 主表迁移为 uuid';
    END IF;
END $$;

ALTER TABLE ai_model_token_audit
    ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS use_type integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS use_resource_id uuid NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000'::uuid;

ALTER TABLE ai_model_token_audit
    ALTER COLUMN user_id TYPE bigint,
    ALTER COLUMN completion_tokens TYPE bigint,
    ALTER COLUMN prompt_tokens TYPE bigint,
    ALTER COLUMN total_tokens TYPE bigint,
    ALTER COLUMN count TYPE bigint;

ALTER TABLE ai_model_usage_log
    ADD COLUMN IF NOT EXISTS team_id integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS use_type integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS use_resource_id integer NOT NULL DEFAULT 0,
    ALTER COLUMN user_id TYPE bigint;

CREATE UNIQUE INDEX IF NOT EXISTS idx_ai_model_token_audit_dim_uindex
    ON ai_model_token_audit (model_id, team_id, user_id, use_type, use_resource_id)
    WHERE is_deleted = 0;

CREATE INDEX IF NOT EXISTS idx_ai_model_usage_log_team_id_index
    ON ai_model_usage_log (team_id);