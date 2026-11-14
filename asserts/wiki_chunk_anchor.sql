-- 切片标题锚点（等价于启动时幂等 DDL，免手工执行）
-- D46：切片记录所在章节的标题面包屑（如「标题A > 标题B」），召回引用可落到章节

ALTER TABLE wiki_document_chunk_content ADD COLUMN IF NOT EXISTS slice_anchor varchar(512) NOT NULL DEFAULT '';
