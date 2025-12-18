/**
 * AI生成结果处理工具函数
 * 用于解析和处理AI生成的元数据
 */

import type { 
  WikiDocumentDerivativeItem,
  PreprocessStrategyType,
} from "../../../../apiClient/models";
import { 
  getDerivativeTypeByStrategy, 
  inferDerivativeTypeFromKey 
} from "../constants";

/**
 * 从AI生成响应中提取元数据
 * @param responseItems API响应项数组
 * @param preprocessStrategyType 预处理策略类型
 * @returns 元数据数组
 */
export function extractDerivativesFromResponse(
  responseItems: Array<{ key?: string; value?: any }>,
  preprocessStrategyType: PreprocessStrategyType
): WikiDocumentDerivativeItem[] {
  const derivatives: WikiDocumentDerivativeItem[] = [];

  responseItems.forEach((item) => {
    if (!item.value) return;

    const result = item.value;
    
    // 处理 metadata（包含关键词、摘要、问题列表等）
    if (result.metadata && Array.isArray(result.metadata) && result.metadata.length > 0) {
      result.metadata.forEach((meta: any) => {
        if (meta.key && meta.value) {
          const derivativeType = inferDerivativeTypeFromKey(
            meta.key,
            preprocessStrategyType
          );
          
          if (derivativeType) {
            derivatives.push({
              derivativeType: derivativeType,
              derivativeContent: meta.value,
            });
          }
        }
      });
    }
    
    // 如果没有 metadata，使用 processedText 或 originalText
    if (derivatives.length === 0) {
      const derivativeType = getDerivativeTypeByStrategy(preprocessStrategyType);
      const content = result.processedText || result.originalText || "";
      
      if (content && derivativeType) {
        derivatives.push({
          derivativeType: derivativeType,
          derivativeContent: content,
        });
      }
    }
  });

  return derivatives;
}

/**
 * 从批量生成响应中提取元数据并按chunkId分组
 * @param responseItems API响应项数组
 * @param preprocessStrategyType 预处理策略类型
 * @returns 按chunkId分组的元数据Map
 */
export function extractBatchDerivativesFromResponse(
  responseItems: Array<{ key?: string; value?: any }>,
  preprocessStrategyType: PreprocessStrategyType
): Map<string, WikiDocumentDerivativeItem[]> {
  const resultsMap = new Map<string, WikiDocumentDerivativeItem[]>();
  
  responseItems.forEach((item) => {
    if (!item.key || !item.value) return;

    const chunkId = item.key;
    const derivatives = extractDerivativesFromResponse(
      [{ value: item.value }],
      preprocessStrategyType
    );
    
    if (derivatives.length > 0) {
      resultsMap.set(chunkId, derivatives);
    }
  });

  return resultsMap;
}

