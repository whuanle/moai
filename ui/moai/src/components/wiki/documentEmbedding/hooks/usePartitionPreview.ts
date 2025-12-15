/**
 * 切割预览管理Hook
 * 负责获取、更新、删除和重排序文档切割预览数据
 */

import { useState, useEffect, useCallback, useRef } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { PartitionPreviewItem } from "../types";
import type { WikiDocumentDerivativeItem } from "../../../../apiClient/models";

/**
 * 切割预览管理Hook
 * @param wikiId Wiki ID
 * @param documentId 文档ID
 * @returns 预览数据、加载状态、上下文持有者和各种操作方法
 */
export function usePartitionPreview(wikiId: string, documentId: string) {
  const [previewData, setPreviewData] = useState<{
    items: PartitionPreviewItem[];
    chunkHeader?: string | null;
    maxTokensPerChunk?: number | null;
    overlap?: number | null;
  } | null>(null);
  // 使用 ref 存储最新的 previewData，避免闭包问题
  const previewDataRef = useRef(previewData);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 同步 ref
  useEffect(() => {
    previewDataRef.current = previewData;
  }, [previewData]);

  const fetchPartitionPreview = useCallback(async () => {
    if (!wikiId || !documentId) {
      return;
    }

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.get_chunks.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      if (response) {
        const items = response.items || [];
        // 按 order 排序
        const sortedItems = [...items].sort((a, b) => {
          const orderA = a.order ?? 0;
          const orderB = b.order ?? 0;
          return orderA - orderB;
        });

        setPreviewData({
          items: sortedItems.map((item) => ({
            chunkId: item.chunkId ?? "",
            order: item.order ?? 0,
            text: item.text ?? "",
            derivatives: item.derivatives ?? null,
          })),
          chunkHeader: null,
          maxTokensPerChunk: null,
          overlap: null,
        });
      }
    } catch (error) {
      console.error("Failed to fetch partition preview:", error);
      proxyRequestError(error, messageApi, "获取切割预览失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  // 重新排序并保存到后端
  const reorderAndSave = useCallback(async (items: PartitionPreviewItem[]) => {
    if (!wikiId || !documentId) {
      console.error("Missing wikiId or documentId:", { wikiId, documentId });
      return;
    }

    try {
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.update_chunks_order.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        chunks: items.map((item) => ({
          chunkId: item.chunkId,
          order: item.order,
        })),
      });
      
      messageApi.success("排序已更新");
    } catch (error) {
      console.error("Failed to update chunks order:", error);
      proxyRequestError(error, messageApi, "更新排序失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi]);

  // 更新chunk内容
  const updateChunkContent = useCallback(async (
    chunkId: string, 
    text: string, 
    order: number, 
    derivatives?: WikiDocumentDerivativeItem[] | null
  ) => {
    if (!wikiId || !documentId) {
      console.error("Missing wikiId or documentId:", { wikiId, documentId });
      return;
    }

    try {
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.update_chnuks.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        chunks: [{
          chunkId: chunkId,
          text: text,
          order: order,
          derivatives: derivatives || null,
        }],
      });
      
      messageApi.success("文本块已更新");
    } catch (error) {
      console.error("Failed to update chunk:", error);
      proxyRequestError(error, messageApi, "更新文本块失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi]);

  const updateItem = useCallback(
    async (chunkId: string, newText: string, newDerivatives?: WikiDocumentDerivativeItem[] | null) => {
      const currentData = previewDataRef.current;
      if (!currentData) {
        console.error("Cannot update item: previewData is null");
        return;
      }
      
      const itemToUpdate = currentData.items.find((item) => item.chunkId === chunkId);
      if (!itemToUpdate) {
        console.error("Cannot find item with chunkId:", chunkId);
        return;
      }
      
      // 更新状态
      const updatedItems = currentData.items.map((item) =>
        item.chunkId === chunkId
          ? { ...item, text: newText, derivatives: newDerivatives !== undefined ? newDerivatives : item.derivatives }
          : item
      );
      
      setPreviewData({
        ...currentData,
        items: updatedItems,
      });
      
      // 调用更新API
      try {
        await updateChunkContent(
          chunkId, 
          newText, 
          itemToUpdate.order ?? 0,
          newDerivatives !== undefined ? newDerivatives : itemToUpdate.derivatives
        );
      } catch (error) {
        // 如果更新失败，重新获取数据以恢复状态
        await fetchPartitionPreview();
        throw error;
      }
    },
    [updateChunkContent, fetchPartitionPreview]
  );

  const deleteItem = useCallback(async (chunkId: string) => {
    if (!wikiId || !documentId) {
      console.error("Missing wikiId or documentId");
      return;
    }

    try {
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.delete_chunk.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        chunkId: chunkId,
      });
      
      messageApi.success("文本块已删除");
      
      // 删除后重新获取数据
      await fetchPartitionPreview();
      
      // 重新排序（从0开始）
      const currentData = previewDataRef.current;
      if (currentData && currentData.items.length > 0) {
        const reorderedItems = currentData.items
          .filter((item) => item.chunkId !== chunkId)
          .map((item, index) => ({
            ...item,
            order: index,
          }));
        
        if (reorderedItems.length > 0) {
          await reorderAndSave(reorderedItems);
          await fetchPartitionPreview();
        }
      }
    } catch (error) {
      console.error("Failed to delete item:", error);
      proxyRequestError(error, messageApi, "删除文本块失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi, fetchPartitionPreview, reorderAndSave]);

  const reorderItems = useCallback(async (fromIndex: number, toIndex: number) => {
    const currentData = previewDataRef.current;
    if (!currentData) {
      console.error("Cannot reorder items: previewData is null");
      return;
    }
    
    // 计算更新后的 items
    const newItems = [...currentData.items];
    const [removed] = newItems.splice(fromIndex, 1);
    newItems.splice(toIndex, 0, removed);
    // 更新 order：从 0 开始重新生成
    const updatedItems = newItems.map((item, index) => ({
      ...item,
      order: index,
    }));
    
    // 更新状态
    setPreviewData({
      ...currentData,
      items: updatedItems,
    });
    
    // 调用排序API保存
    try {
      await reorderAndSave(updatedItems);
      await fetchPartitionPreview();
    } catch (error) {
      await fetchPartitionPreview();
      throw error;
    }
  }, [reorderAndSave, fetchPartitionPreview]);

  // 新增chunk
  const addChunk = useCallback(async (text: string, derivatives?: WikiDocumentDerivativeItem[] | null) => {
    if (!wikiId || !documentId) {
      console.error("Missing wikiId or documentId");
      return;
    }

    try {
      const currentData = previewDataRef.current;
      const maxOrder = currentData && currentData.items && currentData.items.length > 0 
        ? Math.max(...currentData.items.map((item) => item.order ?? 0))
        : -1;
      const newOrder = maxOrder + 1;

      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.add_chunk.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        text: text,
        order: newOrder,
        derivatives: derivatives || null,
      });
      
      messageApi.success("文本块已添加");
      
      // 添加后重新获取数据
      await fetchPartitionPreview();
      
      // 重新排序（从0开始）
      const updatedData = previewDataRef.current;
      if (updatedData && updatedData.items.length > 0) {
        const reorderedItems = updatedData.items.map((item, index) => ({
          ...item,
          order: index,
        }));
        
        await reorderAndSave(reorderedItems);
        await fetchPartitionPreview();
      }
    } catch (error) {
      console.error("Failed to add chunk:", error);
      proxyRequestError(error, messageApi, "添加文本块失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi, fetchPartitionPreview, reorderAndSave]);

  // 重新排序所有 chunks（从 0 开始）
  const reorderAllChunks = useCallback(async () => {
    const currentData = previewDataRef.current;
    if (!currentData || !currentData.items || currentData.items.length === 0) {
      return;
    }

    const reorderedItems = currentData.items.map((item, index) => ({
      ...item,
      order: index,
    }));

    try {
      await reorderAndSave(reorderedItems);
      await fetchPartitionPreview();
    } catch (error) {
      console.error("Failed to reorder all chunks:", error);
    }
  }, [reorderAndSave, fetchPartitionPreview]);

  return {
    previewData,
    loading,
    contextHolder,
    fetchPartitionPreview,
    updateItem,
    deleteItem,
    reorderItems,
    addChunk,
    reorderAllChunks,
  };
}

