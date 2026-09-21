-- 飞书渠道绑定：应用渠道独占，知识库外部源等订阅型渠道一对多（存量库改造，幂等）
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/feishu_app_wiki_channel.sql
-- 背景：同一飞书应用只能建一条长连接，事件应共享给多个业务渠道；但应用渠道（对话机器人）必须独占，
--       否则同一条消息会被两个应用同时消费。故单飞书应用独占索引收缩为「应用渠道独占」。

-- 原单飞书应用独占索引（同一飞书应用只能有一条绑定）不再适用，外部源等订阅型渠道需一对多
drop index if exists idx_feishu_app_binding_feishu_app_uindex;

-- 应用渠道独占：同一飞书应用只能绑定一个团队应用
create unique index if not exists idx_feishu_app_binding_app_uindex on feishu_app_binding (feishu_app_id) where (is_deleted = 0 and channel_type = 0);

-- 同一飞书应用对同一渠道记录只允许一条绑定，避免重复绑定同一外部源/应用
create unique index if not exists idx_feishu_app_binding_channel_uindex on feishu_app_binding (feishu_app_id, channel_type, channel_id) where (is_deleted = 0);

comment on table feishu_app_binding is '飞书应用绑定，将飞书应用绑定到应用/知识库外部源等渠道；应用渠道独占，外部源等订阅型渠道可一对多';
comment on column feishu_app_binding.channel_type is '渠道类型，见 FeishuChannelType（0=app 独占型，1=wikiSource 订阅型可一对多）';
comment on column feishu_app_binding.channel_id is '渠道记录 id 字符串，应用渠道为 app.id（uuid），知识库外部源渠道为 wiki_source.id（uuid）';
