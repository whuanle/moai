/**
 * 文档嵌入组件
 * 此文件已重构，实际实现已拆分到 documentEmbedding/ 目录下的子模块中
 * 
 * 子模块包括：
 * - DocumentPartition: 文档切割（普通切割和智能切割）
 * - ChunkPreview: 切割预览（文本块管理、拖拽排序、元数据编辑）
 * - DocumentVectorization: 文档向量化
 * - RecallTest: 召回测试
 * - TaskList: 任务列表
 */

import DocumentEmbedding from "./documentEmbedding/index";
export default DocumentEmbedding;
export { DocumentPartition, ChunkPreview, DocumentVectorization, RecallTest, TaskList } from "./documentEmbedding/index";
export * from "./documentEmbedding/types";
