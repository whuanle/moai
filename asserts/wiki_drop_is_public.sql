-- 知识库去掉“公开”字段（存量库删列，幂等）
-- 团队知识库不存在公开概念：非团队成员一律 404，接口与前端已同步移除 is_public。
-- 新库由 EnsureCreated 直接建成（实体已无该列），存量库执行本脚本删除残留列。
ALTER TABLE public.wiki DROP COLUMN IF EXISTS is_public;
