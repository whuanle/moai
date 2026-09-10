-- 知识库增量 DDL：wiki.rerank_model_id（重排序模型，可选）
-- 背景：本仓库无 EF Migration，启动 EnsureCreated 只对空库生效；已有数据库需手动执行本文件，
--       同时保持与 src/database 下 WikiEntity 及其 Configuration 一致。
-- 说明：重排序模型与向量化配置解耦——wiki 锁定（已有文档被向量化）后仍可绑定/解绑/更换，
--       为空表示检索时不使用重排序。

ALTER TABLE public.wiki
    ADD COLUMN IF NOT EXISTS rerank_model_id uuid NULL;

COMMENT ON COLUMN public.wiki.rerank_model_id IS '重排序模型的id，可选；为空表示不使用重排序（锁定后仍可修改）';
