-- app_agent_session.user_type 存量修复
-- 背景：RefreshTokenCommandHandler 曾漏设 UserType，刷新换发的 access token typ=none，
--   导致内部用户经浏览器会话创建的 app_agent_session.user_type 落 0（none），日志页被误标为外部用户。
-- 修复范围：user_type=0 且 create_user_id 能匹配内部用户表的行（外部用户会话 create_user_id=0 或走 external 体系恒为 1/2）。
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app_agent_session_user_type_fix.sql
-- 新库由修复后的代码直接写正确值，无需执行本脚本。

update app_agent_session
set user_type = 3
where user_type = 0
  and create_user_id in (select id from "user");
