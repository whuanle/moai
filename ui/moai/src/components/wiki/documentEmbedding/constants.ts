/**
 * 文档嵌入相关常量定义
 */

import type { 
  ParagrahProcessorMetadataType as DerivativeType,
  PreprocessStrategyType,
} from "../../../apiClient/models";

/**
 * 衍生类型映射（支持多种格式）
 * 用于将API返回的衍生类型转换为中文显示
 */
export const DERIVATIVE_TYPE_MAP: Record<string, string> = {
  // 小写格式
  outline: "大纲",
  question: "问题",
  keyword: "关键词",
  summary: "摘要",
  aggregatedSubParagraph: "聚合段落",
  // 枚举键名格式（首字母大写）
  Outline: "大纲",
  Question: "问题",
  Keyword: "关键词",
  Summary: "摘要",
  AggregatedSubParagraph: "聚合段落",
} as const;

/**
 * 衍生类型选项
 * 用于下拉选择框
 */
export const DERIVATIVE_TYPE_OPTIONS = [
  { label: "大纲", value: "outline" },
  { label: "问题", value: "question" },
  { label: "关键词", value: "keyword" },
  { label: "摘要", value: "summary" },
  { label: "聚合段落", value: "aggregatedSubParagraph" },
] as const;

/**
 * 预处理策略选项
 * 用于AI生成策略选择
 */
export const PREPROCESS_STRATEGY_OPTIONS = [
  { label: "大纲生成", value: "outlineGeneration" },
  { label: "问题生成", value: "questionGeneration" },
  { label: "关键词摘要融合", value: "keywordSummaryFusion" },
  { label: "语义聚合", value: "semanticAggregation" },
] as const;

/**
 * 任务状态颜色映射
 * 用于任务状态标签显示
 */
export const TASK_STATUS_COLOR_MAP: Record<string, string> = {
  completed: "success",
  processing: "processing",
  failed: "error",
  wait: "warning",
  none: "warning",
} as const;

/**
 * 默认表单初始值
 */
export const DEFAULT_FORM_VALUES = {
  partition: {
    maxTokensPerChunk: 1000,
    overlap: 100,
    chunkHeader: "",
  },
  aiPartition: {
    promptTemplate: `你是一个专业的中文知识库文档拆分助手。

请根据用户提供的完整文档内容按照以下要求拆分文本：

1. 每个文本块长度尽量不超过 1000 个字符，可根据语义适当调整。
2. 相邻文本块需要保留约 50 个字符的重叠内容以保证上下文衔接。
3.尽可能不要拆开代码或段落，尽可能让语义相近的内容在一个段落内。

3. 只允许引用原文内容，不要编造或总结。

4. 输出统一使用 JSON，格式如下：

{
  "chunks": [
    { "order": 1, "text": "第一块原文内容" },
    { "order": 2, "text": "第二块原文内容" }
  ]
}

order 从 1 开始递增，text 为对应的原文片段。

只输出 JSON，不要附加其他解释。`,
  },
  embedding: {
    isEmbedSourceText: false,
    threadCount: 5,
  },
} as const;

/**
 * 根据预处理策略类型获取衍生类型
 * @param strategyType 预处理策略类型
 * @returns 对应的衍生类型
 */
export function getDerivativeTypeByStrategy(
  strategyType: PreprocessStrategyType | null
): DerivativeType | null {
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
}

/**
 * 根据元数据key推断衍生类型
 * @param key 元数据key
 * @param fallbackStrategy 备用策略类型
 * @returns 衍生类型
 */
export function inferDerivativeTypeFromKey(
  key: string,
  fallbackStrategy?: PreprocessStrategyType | null
): DerivativeType | null {
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
  
  // 如果无法从key推断，使用备用策略
  if (fallbackStrategy) {
    return getDerivativeTypeByStrategy(fallbackStrategy);
  }
  
  return null;
}

/**
 * 判断任务是否可以取消
 * @param state 任务状态
 * @returns 是否可以取消
 */
export function canCancelTask(state: string): boolean {
  const lowerState = state.toLowerCase();
  return (
    lowerState === "none" ||
    lowerState === "wait" ||
    lowerState === "processing"
  );
}

