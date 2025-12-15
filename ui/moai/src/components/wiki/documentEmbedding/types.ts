/**
 * 文档嵌入相关类型定义
 */

import type { 
  WikiDocumentDerivativeItem, 
  PreprocessStrategyType,
} from "../../../apiClient/models";

/**
 * 文档信息接口
 */
export interface DocumentInfo {
  documentId: number;
  fileName: string;
  fileSize: number;
  contentType: string;
  createTime: string;
  createUserName: string;
  embedding: boolean;
  partionConfig?: {
    chunkHeader?: string | null;
    maxTokensPerChunk?: number | null;
    overlap?: number | null;
  } | null;
}

/**
 * 任务信息接口
 */
export interface TaskInfo {
  id: string;
  fileName: string;
  tokenizer: string;
  maxTokensPerParagraph: number;
  overlappingTokens: number;
  state: string;
  message: string;
  createTime: string;
  documentId: string;
}

/**
 * 切割预览项接口
 */
export interface PartitionPreviewItem {
  chunkId: string;
  order: number;
  text: string;
  derivatives?: WikiDocumentDerivativeItem[] | null;
}

/**
 * Chunk编辑模态窗口属性
 */
export interface ChunkEditModalProps {
  open: boolean;
  chunkId: string | null;
  initialText: string;
  initialDerivatives: WikiDocumentDerivativeItem[] | null | undefined;
  wikiId: string;
  documentId: string;
  modelList: Array<{ id: number; name: string }>;
  onClose: () => void;
  onSave: (text: string, derivatives: WikiDocumentDerivativeItem[] | null) => void;
}

/**
 * 批量生成模态窗口属性
 */
export interface BatchGenerateModalProps {
  open: boolean;
  previewData: {
    items: PartitionPreviewItem[];
  } | null;
  modelList: Array<{ id: number; name: string }>;
  onClose: () => void;
  onGenerate: (
    selectedChunkIds: Set<string>,
    aiModelId: number,
    preprocessStrategyType: PreprocessStrategyType
  ) => Promise<Map<string, WikiDocumentDerivativeItem[]>>;
  onSave: (
    results: Map<string, WikiDocumentDerivativeItem[]>,
    aiModelId: number | null
  ) => Promise<void>;
}

