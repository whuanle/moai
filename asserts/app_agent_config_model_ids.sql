-- Agent 应用配置开放多模型选择 schema 变更（model_id 语义升级为默认模型）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_config_model_ids.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 开放给用户选择的对话模型ID列表（含默认模型），JSON uuid 字符串数组文本，如 '["..."]'；
-- 空为'[]'，运行时视为仅默认模型（model_id），存量单模型应用行为不变
alter table app_agent_config add column if not exists model_ids text not null default '[]';
comment on column app_agent_config.model_ids is '开放给用户选择的对话模型ID列表（含默认模型），JSON uuid 字符串数组文本，如 ''["..."]''；空为''[]''，运行时视为仅默认模型';
comment on column app_agent_config.model_id is '默认对话模型ID，逻辑关联ai_model.id（uuid）；必须属于模型可选列表';
