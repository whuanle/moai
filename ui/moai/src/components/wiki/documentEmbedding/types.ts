/**
 * 文档嵌入模块共享类型定义
 */

import type {
  WikiDocumentMetadataItem,
  ParagrahProcessorMetadataType as MetadataType,
  PreprocessStrategyType,
} from "../../../apiClient/models";

// 重新导出 API 类型
export type { WikiDocumentMetadataItem, MetadataType, PreprocessStrategyType };

/**
 * 文档信息接口 - 对应 QueryWikiDocumentInfoCommandResponse
 */
export interface DocumentInfo {
  documentId: number;
  fileName: string;
  fileSize: number;
  contentType: string;
  createTime?: string | null;
  createUserName?: string | null;
  updateTime?: string | null;
  updateUserName?: string | null;
  isEmbedding: boolean;
  chunkCount: number;
  metadataCount: number;
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
  metadatas?: WikiDocumentMetadataItem[] | null;
}

/**
 * AI 模型项
 */
export interface AiModelItem {
  id: number;
  name: string;
}

/**
 * 元数据类型映射表
 */
export const MetadataTypeMap: Record<string, string> = {
  outline: "大纲",
  question: "问题",
  keyword: "关键词",
  summary: "摘要",
  aggregatedSubParagraph: "聚合段落",
  Outline: "大纲",
  Question: "问题",
  Keyword: "关键词",
  Summary: "摘要",
  AggregatedSubParagraph: "聚合段落",
};

/**
 * 元数据类型选项列表
 */
export const MetadataTypeOptions = [
  { label: "大纲", value: "outline" },
  { label: "问题", value: "question" },
  { label: "关键词", value: "keyword" },
  { label: "摘要", value: "summary" },
  { label: "聚合段落", value: "aggregatedSubParagraph" },
];

/**
 * 预处理策略选项列表
 */
export const PreprocessStrategyOptions = [
  { label: "大纲生成", value: "outlineGeneration" },
  { label: "问题生成", value: "questionGeneration" },
  { label: "关键词摘要融合", value: "keywordSummaryFusion" },
  { label: "语义聚合", value: "semanticAggregation" },
];

/**
 * 根据元数据 key 推断元数据类型
 */
export const inferMetadataTypeFromKey = (
  key: string | null | undefined,
  strategyType?: PreprocessStrategyType | null
): MetadataType | null => {
  if (!key) {
    return inferMetadataTypeFromStrategy(strategyType);
  }

  const keyLower = key.toLowerCase();
  if (keyLower.includes("outline") || keyLower.includes("大纲")) {
    return "outline";
  }
  if (keyLower.includes("question") || keyLower.includes("问题")) {
    return "question";
  }
  if (keyLower.includes("keyword") || keyLower.includes("关键词")) {
    return "keyword";
  }
  if (keyLower.includes("summary") || keyLower.includes("摘要")) {
    return "summary";
  }

  return inferMetadataTypeFromStrategy(strategyType);
};

/**
 * 根据预处理策略类型推断元数据类型
 */
export const inferMetadataTypeFromStrategy = (
  strategyType?: PreprocessStrategyType | null
): MetadataType | null => {
  if (!strategyType) return null;

  switch (strategyType) {
    case "outlineGeneration":
      return "outline";
    case "questionGeneration":
      return "question";
    case "keywordSummaryFusion":
      return "keyword";
    case "semanticAggregation":
      return "aggregatedSubParagraph";
    default:
      return null;
  }
};

/**
 * 获取元数据类型的显示名称
 */
export const getMetadataTypeDisplayName = (type: string | null | undefined): string => {
  if (!type) return "未知";

  if (MetadataTypeMap[type]) {
    return MetadataTypeMap[type];
  }

  const typeStr = String(type);
  if (MetadataTypeMap[typeStr]) {
    return MetadataTypeMap[typeStr];
  }

  const typeLower = typeStr.toLowerCase();
  if (MetadataTypeMap[typeLower]) {
    return MetadataTypeMap[typeLower];
  }

  return typeStr;
};
