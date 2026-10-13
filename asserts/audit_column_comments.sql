-- 补齐框架审计列的 comment：EF 基类注入的 create_user_id/create_time/update_user_id/update_time/is_deleted
-- 此前各建表 SQL 只给业务列写了 comment，审计列（含部分表的 is_deleted）落库后无备注；
-- 所有列均已 NOT NULL，无需 ALTER，仅补 comment。
-- 执行方式：psql 直连执行，幂等（重复执行只覆盖注释）。

-- ============ app_access_point ============
comment on column public.app_access_point.create_user_id is '创建人';
comment on column public.app_access_point.create_time is '创建时间';
comment on column public.app_access_point.update_user_id is '更新人';
comment on column public.app_access_point.update_time is '更新时间';
comment on column public.app_access_point.is_deleted is '软删除，0=未删除';

-- ============ app_memory_shard（无 is_deleted 列）============
comment on column public.app_memory_shard.create_user_id is '创建人';
comment on column public.app_memory_shard.create_time is '创建时间';
comment on column public.app_memory_shard.update_user_id is '更新人';
comment on column public.app_memory_shard.update_time is '更新时间';

-- ============ app_security_config ============
comment on column public.app_security_config.create_user_id is '创建人';
comment on column public.app_security_config.create_time is '创建时间';
comment on column public.app_security_config.update_user_id is '更新人';
comment on column public.app_security_config.update_time is '更新时间';
comment on column public.app_security_config.is_deleted is '软删除，0=未删除';

-- ============ app_user_config ============
comment on column public.app_user_config.create_user_id is '创建人';
comment on column public.app_user_config.create_time is '创建时间';
comment on column public.app_user_config.update_user_id is '更新人';
comment on column public.app_user_config.update_time is '更新时间';
comment on column public.app_user_config.is_deleted is '软删除，0=未删除';

-- ============ app_workflow_config ============
comment on column public.app_workflow_config.create_user_id is '创建人';
comment on column public.app_workflow_config.create_time is '创建时间';
comment on column public.app_workflow_config.update_user_id is '更新人';
comment on column public.app_workflow_config.update_time is '更新时间';
comment on column public.app_workflow_config.is_deleted is '软删除，0=未删除';

-- ============ app_workflow_instance ============
comment on column public.app_workflow_instance.create_user_id is '创建人';
comment on column public.app_workflow_instance.create_time is '创建时间';
comment on column public.app_workflow_instance.update_user_id is '更新人';
comment on column public.app_workflow_instance.update_time is '更新时间';
comment on column public.app_workflow_instance.is_deleted is '软删除，0=未删除';

-- ============ external_user ============
comment on column public.external_user.create_user_id is '创建人';
comment on column public.external_user.create_time is '创建时间';
comment on column public.external_user.update_user_id is '更新人';
comment on column public.external_user.update_time is '更新时间';
comment on column public.external_user.is_deleted is '软删除，0=未删除';

-- ============ knowledge_graph ============
comment on column public.knowledge_graph.create_user_id is '创建人';
comment on column public.knowledge_graph.create_time is '创建时间';
comment on column public.knowledge_graph.update_user_id is '更新人';
comment on column public.knowledge_graph.update_time is '更新时间';
comment on column public.knowledge_graph.is_deleted is '软删除，0=未删除';

-- ============ knowledge_graph_entity_type ============
comment on column public.knowledge_graph_entity_type.create_user_id is '创建人';
comment on column public.knowledge_graph_entity_type.create_time is '创建时间';
comment on column public.knowledge_graph_entity_type.update_user_id is '更新人';
comment on column public.knowledge_graph_entity_type.update_time is '更新时间';
comment on column public.knowledge_graph_entity_type.is_deleted is '软删除，0=未删除';

-- ============ knowledge_graph_relation_type ============
comment on column public.knowledge_graph_relation_type.create_user_id is '创建人';
comment on column public.knowledge_graph_relation_type.create_time is '创建时间';
comment on column public.knowledge_graph_relation_type.update_user_id is '更新人';
comment on column public.knowledge_graph_relation_type.update_time is '更新时间';
comment on column public.knowledge_graph_relation_type.is_deleted is '软删除，0=未删除';

-- ============ skill ============
comment on column public.skill.create_user_id is '创建人';
comment on column public.skill.create_time is '创建时间';
comment on column public.skill.update_user_id is '更新人';
comment on column public.skill.update_time is '更新时间';
comment on column public.skill.is_deleted is '软删除，0=未删除';

-- ============ team_timer（is_deleted 已有注释）============
comment on column public.team_timer.create_user_id is '创建人';
comment on column public.team_timer.create_time is '创建时间';
comment on column public.team_timer.update_user_id is '更新人';
comment on column public.team_timer.update_time is '更新时间';

-- ============ team_timer_execution（is_deleted 已有注释）============
comment on column public.team_timer_execution.create_user_id is '创建人';
comment on column public.team_timer_execution.create_time is '创建时间';
comment on column public.team_timer_execution.update_user_id is '更新人';
comment on column public.team_timer_execution.update_time is '更新时间';

-- ============ app_security_event ============
comment on column public.app_security_event.create_user_id is '创建人';
comment on column public.app_security_event.create_time is '创建时间';
comment on column public.app_security_event.update_user_id is '更新人';
comment on column public.app_security_event.update_time is '更新时间';
comment on column public.app_security_event.is_deleted is '软删除，0=未删除';

-- ============ 顺带补齐两条漏掉的业务列 ============
comment on column public.plugin.is_system is '是否系统插件，true=系统内置插件（team_id=0 全局可用），false=团队自定义插件';
comment on column public.setting.name is '设置项显示名称';
