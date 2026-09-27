-- 团队模型网关密钥下线（存量库删表，幂等）
-- 网关与外部接口的接入统一收敛到应用接入 key（moai-ac-，access_app），团队接入 key（moai-，team_api_key）
-- 的创建/管理接口与认证路径已删除。新库由 EnsureCreated 直接建成（实体已无该表），存量库执行本脚本删除残留表。
DROP TABLE IF EXISTS public.team_api_key;
