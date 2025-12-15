/**
 * 向量化操作Hook
 * 负责文档向量化和清空向量操作
 */

import { useState, useCallback } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";

/**
 * 向量化操作Hook
 * @param wikiId Wiki ID
 * @param documentId 文档ID
 * @param onSuccess 成功回调
 * @returns 加载状态、清空加载状态、上下文持有者和操作方法
 */
export function useEmbeddingOperations(
  wikiId: string,
  documentId: string,
  onSuccess: () => void
) {
  const [loading, setLoading] = useState(false);
  const [clearLoading, setClearLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const submitEmbedding = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        return;
      }

      try {
        setLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.embedding_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          isEmbedSourceText: values.isEmbedSourceText ?? false,
          threadCount: values.threadCount ?? null,
        });

        messageApi.success("向量化任务已提交");
        onSuccess();
      } catch (error) {
        console.error("Failed to submit embedding task:", error);
        proxyRequestError(error, messageApi, "提交向量化任务失败");
      } finally {
        setLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onSuccess]
  );

  const clearVectors = useCallback(async () => {
    if (!wikiId || !documentId) {
      return;
    }

    try {
      setClearLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.clear_embeddingt.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      messageApi.success("向量已清空");
      onSuccess();
    } catch (error) {
      console.error("Failed to clear vectors:", error);
      proxyRequestError(error, messageApi, "清空向量失败");
    } finally {
      setClearLoading(false);
    }
  }, [wikiId, documentId, messageApi, onSuccess]);

  return {
    loading,
    clearLoading,
    contextHolder,
    submitEmbedding,
    clearVectors,
  };
}

