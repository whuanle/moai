-- 知识库文档内嵌图片附件（wiki_document_image）schema 变更
-- 用法（存量库）：
--   docker exec -i moai-postgres psql -U postgres -d moai < asserts/wiki_document_image.sql
-- 新库由 EnsureCreated 依据实体/配置直接建成，无需执行本脚本。
-- 执行后请重跑 tool/PostgresScaffold 逆向生成，保持库与实体一致。

-- 文档内嵌图片附件：提取 pdf/docx/xlsx/pptx 时落出的内嵌图片，上传对象存储后在此登记，
-- 图片本体按内容寻址（object_key 含 sha256），同一图片可能被多个文档引用，删除时须做引用检查
create table if not exists wiki_document_image (
  id serial not null,
  wiki_id integer not null,
  document_id integer not null,
  file_id bigint not null,
  object_key varchar(255) not null,
  file_name varchar(1024) not null,
  file_type varchar(10) not null,
  page_number integer null,
  image_index integer not null default 0,
  create_user_id bigint not null default 0,
  create_time timestamptz not null default timezone('utc', now()),
  update_user_id bigint not null default 0,
  update_time timestamptz not null default timezone('utc', now()),
  is_deleted bigint not null default 0,
  constraint idx_wiki_document_image_primary primary key (id)
);
comment on table wiki_document_image is '知识库文档内嵌图片附件，文档提取时落出的图片资产登记';
comment on column wiki_document_image.id is '自增主键';
comment on column wiki_document_image.wiki_id is '知识库id';
comment on column wiki_document_image.document_id is '知识库文档id（wiki_document.id，逻辑关联，仓库约定不建物理外键）';
comment on column wiki_document_image.file_id is '文件id（file.id，图片本体在对象存储，按内容寻址可被多文档共享）';
comment on column wiki_document_image.object_key is '文件路径，wiki/{wikiId}/image/{sha256}.{ext}';
comment on column wiki_document_image.file_name is '图片文件名，取自文档内部部件名或提取序名';
comment on column wiki_document_image.file_type is '文件扩展名称，如.png';
comment on column wiki_document_image.page_number is '所在页码（pdf 提取可解析），null=未知';
comment on column wiki_document_image.image_index is '文档内序号，按提取产出顺序从 0 递增';
comment on column wiki_document_image.create_time is '创建时间';
comment on column wiki_document_image.create_user_id is '创建人';
comment on column wiki_document_image.update_time is '更新时间';
comment on column wiki_document_image.update_user_id is '更新人';
comment on column wiki_document_image.is_deleted is '软删除';

create index if not exists idx_wiki_document_image_document_id_index on wiki_document_image (wiki_id, document_id) where (is_deleted = 0);
