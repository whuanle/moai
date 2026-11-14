-- 外部应用授权退役（is_auth）：外部应用一律匿名开放，存量行收敛为 false
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/app-is-auth-retired.sql
-- 新库无 is_auth=true 行；后端启动时 DatabasPostgresModule 亦会幂等执行同一 UPDATE。

update app set is_auth = false where is_auth = true;
comment on column app.is_auth is '已退役：外部应用一律匿名开放，字段恒 false';
