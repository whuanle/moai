-- 知识图谱导入任务表（B2 导入状态机）：存量库补表，幂等
-- 无 EF Migration：EnsureCreated 只对空库生效，存量库需手动执行本文件，
-- 并保持与 src/database 下 KgImportJobEntity 及其 Configuration 一致
CREATE TABLE IF NOT EXISTS kg_import_job (
    id bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
    knowledge_graph_id bigint NOT NULL,
    team_id integer NOT NULL,
    type varchar(20) NOT NULL,
    file_name varchar(255) NOT NULL,
    object_key varchar(500) NOT NULL,
    ai_model_id uuid NULL,
    status varchar(20) NOT NULL,
    total_rows integer NOT NULL,
    done_rows integer NOT NULL,
    error_json text NULL,
    result_json text NULL,
    is_deleted bigint NOT NULL DEFAULT 0,
    create_user_id bigint NOT NULL DEFAULT 0,
    create_time timestamptz NOT NULL DEFAULT timezone('utc', now()),
    update_user_id bigint NOT NULL DEFAULT 0,
    update_time timestamptz NOT NULL DEFAULT timezone('utc', now())
);

COMMENT ON TABLE kg_import_job IS '知识图谱导入任务（AI 文档导入异步状态机：pending/extracting/parsing/merging/done/failed/cancelled）';
COMMENT ON COLUMN kg_import_job.knowledge_graph_id IS '所属图谱 id（仅托管图）';
COMMENT ON COLUMN kg_import_job.team_id IS '所属团队 id';
COMMENT ON COLUMN kg_import_job.type IS '任务类型：file/json';
COMMENT ON COLUMN kg_import_job.file_name IS '导入文件名';
COMMENT ON COLUMN kg_import_job.object_key IS '导入文件的对象存储 key';
COMMENT ON COLUMN kg_import_job.ai_model_id IS '抽取使用的对话模型 id';
COMMENT ON COLUMN kg_import_job.status IS '任务状态：pending/extracting/parsing/merging/done/failed/cancelled';
COMMENT ON COLUMN kg_import_job.total_rows IS '总行数（解析出的节点+边草稿数）';
COMMENT ON COLUMN kg_import_job.done_rows IS '已完成行数（落库进度）';
COMMENT ON COLUMN kg_import_job.error_json IS '失败原因（JSON：message/stage），成功为 null';
COMMENT ON COLUMN kg_import_job.result_json IS '结果统计 JSON，未完成为 null';
COMMENT ON COLUMN kg_import_job.is_deleted IS '软删除，0=未删除';
COMMENT ON COLUMN kg_import_job.create_user_id IS '创建人';
COMMENT ON COLUMN kg_import_job.create_time IS '创建时间';
COMMENT ON COLUMN kg_import_job.update_user_id IS '更新人';
COMMENT ON COLUMN kg_import_job.update_time IS '更新时间';

CREATE INDEX IF NOT EXISTS idx_kg_import_job_kg_id ON kg_import_job (knowledge_graph_id, id);
