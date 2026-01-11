/**
 * 文档嵌入模块共享 hooks
 */

import { useState, useCallback } from "react";
import { message } from "antd";
import { GetApiClient } from "../../ServiceClient";
import { proxyRequestError } from "../../../helper/RequestError";
import type { DocumentInfo } from "./types";

// 从上层 hooks 导出，保持向后兼容
export { useAiModelList } from "../wiki_hooks";

/**
 * 获取文档信息
 */
export function useDocumentInfo(wikiId: string, documentId: string) {
  const [documentInfo, setDocumentInfo] = useState<DocumentInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchDocumentInfo = useCallback(async () => {
    if (!wikiId || !documentId) return;

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.document_info.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      if (response) {
        setDocumentInfo({
          documentId: response.documentId ?? 0,
          fileName: response.fileName ?? "",
          fileSize: response.fileSize ?? 0,
          contentType: response.contentType ?? "",
          createTime: response.createTime,
          createUserName: response.createUserName,
          updateTime: response.updateTime,
          updateUserName: response.updateUserName,
          isEmbedding: response.isEmbedding ?? false,
          chunkCount: response.chunkCount ?? 0,
          metadataCount: response.metedataCount ?? 0,
          partionConfig: response.partionConfig
            ? {
                chunkHeader: response.partionConfig.chunkHeader,
                maxTokensPerChunk: response.partionConfig.maxTokensPerChunk,
                overlap: response.partionConfig.overlap,
              }
            : null,
        });
      }
    } catch (error) {
      console.error("Failed to fetch document info:", error);
      proxyRequestError(error, messageApi, "获取文档信息失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  return {
    documentInfo,
    loading,
    contextHolder,
    fetchDocumentInfo,
  };
}
