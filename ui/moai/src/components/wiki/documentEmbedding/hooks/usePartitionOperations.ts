/**
 * 文档切割操作Hook
 * 负责普通切割和智能切割操作
 */

import { useState, useCallback } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { PartitionPreviewItem } from "../types";

/**
 * 普通切割操作Hook
 * @param wikiId Wiki ID
 * @param documentId 文档ID
 * @param onSuccess 成功回调
 * @returns 加载状态、上下文持有者和提交函数
 */
export function usePartitionOperations(
  wikiId: string,
  documentId: string,
  onSuccess: () => void
) {
  const [loading, setLoading] = useState(false);
  const [previewItems, setPreviewItems] = useState<PartitionPreviewItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();

  const submitPartition = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        return;
      }

      try {
        setLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.text_partition_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          maxTokensPerChunk: values.maxTokensPerChunk,
          overlap: values.overlap,
          chunkHeader: values.chunkHeader || null,
        });

        messageApi.success("文档切割预览成功");
        onSuccess();
      } catch (error) {
        console.error("Failed to submit partition task:", error);
        proxyRequestError(error, messageApi, "文档切割失败");
      } finally {
        setLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onSuccess]
  );

  return {
    loading,
    previewItems,
    contextHolder,
    submitPartition,
    setPreviewItems,
  };
}

/**
 * 智能切割操作Hook
 * @param wikiId Wiki ID
 * @param documentId 文档ID
 * @param onSuccess 成功回调
 * @returns 加载状态、上下文持有者和提交函数
 */
export function useAiPartitionOperations(
  wikiId: string,
  documentId: string,
  onSuccess: () => void
) {
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const submitAiPartition = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        return;
      }

      if (!values.aiModelId) {
        messageApi.warning("请选择AI模型");
        return;
      }

      try {
        setLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.ai_text_partition_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          aiModelId: values.aiModelId,
          promptTemplate: values.promptTemplate || null,
        });

        messageApi.success("智能切割任务已提交");
        onSuccess();
      } catch (error) {
        console.error("Failed to submit AI partition task:", error);
        proxyRequestError(error, messageApi, "智能切割失败");
      } finally {
        setLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onSuccess]
  );

  return {
    loading,
    contextHolder,
    submitAiPartition,
  };
}

