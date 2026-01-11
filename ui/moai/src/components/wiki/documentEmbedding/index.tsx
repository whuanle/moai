/**
 * 文档嵌入模块入口
 * 整合文档切割、切割预览、文档向量化、召回测试、任务列表五个子模块
 */

import { useRef, useCallback } from "react";
import { useParams } from "react-router";
import { Card, Empty } from "antd";
import DocumentPartition from "./DocumentPartition";
import ChunkPreview, { type ChunkPreviewRef } from "./ChunkPreview";
import DocumentVectorization from "./DocumentVectorization";
import RecallTest from "./RecallTest";
import TaskList from "./TaskList";
import "./styles.css";

/**
 * 文档嵌入主组件
 */
export default function DocumentEmbedding() {
  const { id: wikiId, documentId } = useParams();
  const chunkPreviewRef = useRef<ChunkPreviewRef>(null);

  // 切割成功后刷新预览
  const handlePartitionSuccess = useCallback(() => {
    chunkPreviewRef.current?.refresh();
  }, []);

  // 参数验证
  if (!wikiId || !documentId) {
    return (
      <Card>
        <Empty description="缺少必要的参数（Wiki ID 或 Document ID）" />
      </Card>
    );
  }

  return (
    <>
      {/* 文档切割 */}
      <DocumentPartition wikiId={wikiId} documentId={documentId} onPartitionSuccess={handlePartitionSuccess} />

      {/* 切割预览 */}
      <ChunkPreview ref={chunkPreviewRef} wikiId={wikiId} documentId={documentId} />

      {/* 文档向量化 */}
      <DocumentVectorization wikiId={wikiId} documentId={documentId} />

      {/* 召回测试 */}
      <RecallTest wikiId={wikiId} documentId={documentId} />

      {/* 任务列表 */}
      <TaskList wikiId={wikiId} documentId={documentId} />
    </>
  );
}

// 导出子模块供单独使用
export { DocumentPartition, ChunkPreview, DocumentVectorization, RecallTest, TaskList };
export * from "./types";
