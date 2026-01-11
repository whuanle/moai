/**
 * 切割预览模块
 * 负责展示和管理文档切割后的文本块
 */

import { useState, useEffect, useCallback, useRef, useImperativeHandle, forwardRef } from "react";
import {
  Card,
  Button,
  message,
  Select,
  Space,
  Typography,
  Empty,
  Tag,
  Collapse,
  Input,
  Popconfirm,
  Modal,
  List,
  Divider,
  Checkbox,
} from "antd";
import {
  ReloadOutlined,
  FileTextOutlined,
  EditOutlined,
  DeleteOutlined,
  DragOutlined,
  PlusOutlined,
  CheckOutlined,
  DownOutlined,
  UpOutlined,
  ThunderboltOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import CodeEditorModal from "../../common/CodeEditorModal";
import { proxyRequestError } from "../../../helper/RequestError";
import { useAiModelList } from "./hooks";
import type {
  PartitionPreviewItem,
  AiModelItem,
  WikiDocumentMetadataItem,
  MetadataType,
  PreprocessStrategyType,
} from "./types";
import {
  MetadataTypeOptions,
  PreprocessStrategyOptions,
  getMetadataTypeDisplayName,
  inferMetadataTypeFromKey,
  inferMetadataTypeFromStrategy,
} from "./types";
import type {
  KeyValueOfLongAndString,
  AddWikiDocumentMetadataItem,
  WikiDocumenChunkItem,
  KeyValueOfLongAndParagraphPreprocessResult,
  KeyValueString,
} from "../../../apiClient/models";
import "./styles.css";

const { Panel } = Collapse;

interface ChunkPreviewProps {
  wikiId: string;
  documentId: string;
}

// 暴露给父组件的方法
export interface ChunkPreviewRef {
  refresh: () => void;
}

// ============================================
// Chunk 编辑模态框组件
// ============================================
interface ChunkEditModalProps {
  open: boolean;
  chunkId: string | null;
  initialText: string;
  initialMetadatas: WikiDocumentMetadataItem[] | null | undefined;
  wikiId: string;
  modelList: AiModelItem[];
  onClose: () => void;
  onSave: (text: string, metadatas: WikiDocumentMetadataItem[] | null) => void;
}

const ChunkEditModal: React.FC<ChunkEditModalProps> = ({
  open,
  chunkId,
  initialText,
  initialMetadatas,
  wikiId,
  modelList,
  onClose,
  onSave,
}) => {
  const [textEditorVisible, setTextEditorVisible] = useState(false);
  const [textEditorValue, setTextEditorValue] = useState(initialText);
  const [metadatas, setMetadatas] = useState<WikiDocumentMetadataItem[]>(initialMetadatas || []);
  const [editingMetadataIndex, setEditingMetadataIndex] = useState<number | null>(null);
  const [newMetadataType, setNewMetadataType] = useState<MetadataType | null>(null);
  const [newMetadataContent, setNewMetadataContent] = useState<string>("");
  const [aiGenerateVisible, setAiGenerateVisible] = useState(false);
  const [aiModelId, setAiModelId] = useState<number | null>(null);
  const [preprocessStrategyType, setPreprocessStrategyType] = useState<PreprocessStrategyType | null>(null);
  const [aiGenerating, setAiGenerating] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  useEffect(() => {
    if (open) {
      setTextEditorValue(initialText || "");
      setMetadatas(initialMetadatas || []);
    }
  }, [open, initialText, initialMetadatas]);

  const handleAddMetadata = useCallback(() => {
    if (!newMetadataType || !newMetadataContent?.trim()) {
      message.warning("请选择类型并输入内容");
      return;
    }
    setMetadatas((prev) => [...prev, { metadataType: newMetadataType, metadataContent: newMetadataContent.trim() }]);
    setNewMetadataType(null);
    setNewMetadataContent("");
  }, [newMetadataType, newMetadataContent]);

  const handleDeleteMetadata = useCallback((index: number) => {
    setMetadatas((prev) => prev.filter((_, i) => i !== index));
  }, []);

  const handleSave = useCallback(() => {
    if (!textEditorValue?.trim()) {
      message.warning("文本内容不能为空");
      return;
    }
    onSave(textEditorValue.trim(), metadatas.length > 0 ? metadatas : null);
  }, [textEditorValue, metadatas, onSave]);

  const handleAiGenerate = useCallback(async () => {
    if (!chunkId) {
      messageApi.warning("请先保存文本块");
      return;
    }
    if (!aiModelId || !preprocessStrategyType || !textEditorValue?.trim()) {
      messageApi.warning("请选择AI模型、优化策略，且文本内容不能为空");
      return;
    }

    try {
      setAiGenerating(true);
      const apiClient = GetApiClient();
      const chunks: KeyValueOfLongAndString[] = [{ key: chunkId, value: textEditorValue }];
      const response = await apiClient.api.wiki.document.ai_generation_chunk.post({
        wikiId: parseInt(wikiId),
        aiModelId,
        chunks,
        preprocessStrategyType,
      });

      if (response?.items && response.items.length > 0) {
        const newMetadatas: WikiDocumentMetadataItem[] = [];
        response.items.forEach((item: KeyValueOfLongAndParagraphPreprocessResult) => {
          if (item.value) {
            const result = item.value;
            if (result.metadata && result.metadata.length > 0) {
              result.metadata.forEach((meta: KeyValueString) => {
                if (meta.key && meta.value) {
                  const metadataType = inferMetadataTypeFromKey(meta.key, preprocessStrategyType);
                  if (metadataType) newMetadatas.push({ metadataType, metadataContent: meta.value });
                }
              });
            }
            if (newMetadatas.length === 0) {
              const metadataType = inferMetadataTypeFromStrategy(preprocessStrategyType);
              const content = result.processedText || result.originalText || "";
              if (content && metadataType) newMetadatas.push({ metadataType, metadataContent: content });
            }
          }
        });

        if (newMetadatas.length > 0) {
          setMetadatas((prev) => [...prev, ...newMetadatas]);
          messageApi.success(`成功生成 ${newMetadatas.length} 个元数据`);
          setAiGenerateVisible(false);
          setAiModelId(null);
          setPreprocessStrategyType(null);
        } else {
          messageApi.warning("AI生成未返回有效内容");
        }
      } else {
        messageApi.warning("AI生成未返回结果");
      }
    } catch (error) {
      console.error("Failed to generate AI metadatas:", error);
      proxyRequestError(error, messageApi, "AI生成失败，请重试");
    } finally {
      setAiGenerating(false);
    }
  }, [chunkId, aiModelId, preprocessStrategyType, textEditorValue, wikiId, messageApi]);

  return (
    <>
      {contextHolder}
      <Modal
        title={chunkId ? "编辑文本块" : "新增文本块"}
        open={open}
        onCancel={onClose}
        onOk={handleSave}
        width={1000}
        okText="保存"
        cancelText="取消"
        destroyOnClose
        maskClosable={false}
      >
        <div className="chunk-edit-section">
          <div className="chunk-edit-label">文本内容</div>
          <Button type="default" icon={<EditOutlined />} onClick={() => setTextEditorVisible(true)} style={{ width: "100%" }}>
            编辑文本
          </Button>
          <Typography.Paragraph className="chunk-edit-preview">{textEditorValue || "(空)"}</Typography.Paragraph>
        </div>

        <Divider />

        <div>
          <div className="chunk-metadata-header">
            <span className="chunk-edit-label">元数据</span>
            <Button type="dashed" icon={<PlusOutlined />} onClick={() => setAiGenerateVisible(!aiGenerateVisible)} disabled={!chunkId}>
              AI 生成策略
            </Button>
          </div>

          {aiGenerateVisible && (
            <div className="ai-generate-panel">
              <Space direction="vertical" style={{ width: "100%" }} size="middle">
                <div>
                  <div className="ai-generate-field-label">AI 模型</div>
                  <Select
                    placeholder="请选择AI模型"
                    value={aiModelId}
                    onChange={setAiModelId}
                    style={{ width: "100%" }}
                    options={modelList.map((model) => ({ label: model.name, value: model.id }))}
                  />
                </div>
                <div>
                  <div className="ai-generate-field-label">优化策略</div>
                  <Select
                    placeholder="请选择优化策略"
                    value={preprocessStrategyType}
                    onChange={setPreprocessStrategyType}
                    style={{ width: "100%" }}
                    options={PreprocessStrategyOptions}
                  />
                </div>
                <Space style={{ width: "100%", justifyContent: "flex-end" }}>
                  <Button onClick={() => { setAiGenerateVisible(false); setAiModelId(null); setPreprocessStrategyType(null); }}>取消</Button>
                  <Button type="primary" loading={aiGenerating} onClick={handleAiGenerate} disabled={!aiModelId || !preprocessStrategyType}>生成</Button>
                </Space>
              </Space>
            </div>
          )}

          <div className="metadata-add-section">
            <Space.Compact style={{ width: "100%" }}>
              <Select placeholder="选择类型" value={newMetadataType} onChange={setNewMetadataType} style={{ width: 150 }} options={MetadataTypeOptions} />
              <Input placeholder="输入内容" value={newMetadataContent} onChange={(e) => setNewMetadataContent(e.target.value)} onPressEnter={handleAddMetadata} style={{ flex: 1 }} />
              <Button type="primary" icon={<PlusOutlined />} onClick={handleAddMetadata}>添加</Button>
            </Space.Compact>
          </div>

          <List
            dataSource={metadatas}
            locale={{ emptyText: "暂无元数据" }}
            renderItem={(item, index) => (
              <List.Item
                actions={[
                  <Button key="edit" type="link" size="small" icon={<EditOutlined />} onClick={() => setEditingMetadataIndex(index)}>编辑</Button>,
                  <Popconfirm key="delete" title="确认删除" description="确定要删除这个元数据吗？" onConfirm={() => handleDeleteMetadata(index)} okText="确认" cancelText="取消">
                    <Button type="link" size="small" danger icon={<DeleteOutlined />}>删除</Button>
                  </Popconfirm>,
                ]}
              >
                {editingMetadataIndex === index ? (
                  <Space.Compact style={{ width: "100%" }}>
                    <Select
                      value={item.metadataType || null}
                      onChange={(value) => { const updated = [...metadatas]; updated[index] = { ...updated[index], metadataType: value }; setMetadatas(updated); }}
                      style={{ width: 150 }}
                      options={MetadataTypeOptions}
                    />
                    <Input
                      value={item.metadataContent || ""}
                      onChange={(e) => { const updated = [...metadatas]; updated[index] = { ...updated[index], metadataContent: e.target.value }; setMetadatas(updated); }}
                      onPressEnter={() => setEditingMetadataIndex(null)}
                      style={{ flex: 1 }}
                    />
                    <Button type="primary" icon={<CheckOutlined />} onClick={() => setEditingMetadataIndex(null)}>完成</Button>
                  </Space.Compact>
                ) : (
                  <List.Item.Meta
                    title={<Tag color="purple">{getMetadataTypeDisplayName(item.metadataType)}</Tag>}
                    description={<Typography.Text className="chunk-metadata-content">{item.metadataContent || "(空)"}</Typography.Text>}
                  />
                )}
              </List.Item>
            )}
          />
        </div>
      </Modal>

      <CodeEditorModal
        open={textEditorVisible}
        initialValue={textEditorValue}
        language="plaintext"
        title="编辑文本内容"
        onClose={() => setTextEditorVisible(false)}
        onConfirm={(value) => { setTextEditorValue(value); setTextEditorVisible(false); }}
        width={1200}
        height="70vh"
      />
    </>
  );
};


// ============================================
// 切割预览主组件
// ============================================
const ChunkPreview = forwardRef<ChunkPreviewRef, ChunkPreviewProps>(({ wikiId, documentId }, ref) => {
  const [messageApi, contextHolder] = message.useMessage();
  const [previewData, setPreviewData] = useState<{ items: PartitionPreviewItem[] } | null>(null);
  const previewDataRef = useRef(previewData);
  const [loading, setLoading] = useState(false);
  const [expandedMetadatas, setExpandedMetadatas] = useState<Set<string>>(new Set());
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);

  // 编辑模态框状态
  const [chunkEditModalVisible, setChunkEditModalVisible] = useState(false);
  const [editingChunkId, setEditingChunkId] = useState<string | null>(null);
  const [editingChunkText, setEditingChunkText] = useState<string>("");
  const [editingChunkMetadatas, setEditingChunkMetadatas] = useState<WikiDocumentMetadataItem[] | null>(null);

  // 批量生成状态
  const [batchGenerateVisible, setBatchGenerateVisible] = useState(false);
  const [selectedChunkIds, setSelectedChunkIds] = useState<Set<string>>(new Set());
  const [batchAiModelId, setBatchAiModelId] = useState<number | null>(null);
  const [batchPreprocessStrategyType, setBatchPreprocessStrategyType] = useState<PreprocessStrategyType | null>(null);
  const [batchGenerating, setBatchGenerating] = useState(false);
  const [batchGenerateResults, setBatchGenerateResults] = useState<Map<string, WikiDocumentMetadataItem[]>>(new Map());

  // 使用共享 hook
  const { modelList, loading: modelListLoading, fetchModelList } = useAiModelList();

  useEffect(() => { previewDataRef.current = previewData; }, [previewData]);

  // 获取切割预览数据
  const fetchPartitionPreview = useCallback(async () => {
    if (!wikiId || !documentId) return;
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.get_chunks.post({ wikiId: parseInt(wikiId), documentId: parseInt(documentId) });
      if (response) {
        const items = Array.isArray(response.items) ? response.items : [];
        const sortedItems = items
          .filter((item: WikiDocumenChunkItem) => item && item.chunkId)
          .sort((a: WikiDocumenChunkItem, b: WikiDocumenChunkItem) => (a.order ?? 0) - (b.order ?? 0));
        setPreviewData({
          items: sortedItems.map((item: WikiDocumenChunkItem) => ({
            chunkId: item.chunkId ?? "",
            order: item.order ?? 0,
            text: item.text ?? "",
            metadatas: item.metadatas ?? null,
          })),
        });
      }
    } catch (error) {
      console.error("Failed to fetch partition preview:", error);
      proxyRequestError(error, messageApi, "获取切割预览失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  useEffect(() => {
    fetchPartitionPreview();
    fetchModelList();
  }, [fetchPartitionPreview, fetchModelList]);

  // 暴露刷新方法给父组件
  useImperativeHandle(ref, () => ({
    refresh: fetchPartitionPreview,
  }), [fetchPartitionPreview]);

  // API 操作函数
  const reorderAndSave = useCallback(async (items: PartitionPreviewItem[]) => {
    if (!wikiId || !documentId || !items || items.length === 0) return;
    try {
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.update_chunks_order.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        chunks: items.map((item) => ({ chunkId: item.chunkId, order: item.order })),
      });
      messageApi.success("排序已更新");
    } catch (error) {
      console.error("Failed to update chunks order:", error);
      proxyRequestError(error, messageApi, "更新排序失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi]);

  const updateChunkContent = useCallback(async (chunkId: string, text: string, order: number, metadatas?: WikiDocumentMetadataItem[] | null) => {
    if (!wikiId || !documentId || !chunkId?.trim()) return;
    try {
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.update_chnuks.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        chunks: [{ chunkId, text, order, metadatas: metadatas || null }],
      });
      messageApi.success("文本块已更新");
    } catch (error) {
      console.error("Failed to update chunk:", error);
      proxyRequestError(error, messageApi, "更新文本块失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi]);

  const updateItem = useCallback(async (chunkId: string, newText: string, newMetadatas?: WikiDocumentMetadataItem[] | null) => {
    if (!chunkId?.trim()) return;
    const currentData = previewDataRef.current;
    if (!currentData) return;
    const itemToUpdate = currentData.items.find((item) => item.chunkId === chunkId);
    if (!itemToUpdate) return;
    const updatedItems = currentData.items.map((item) =>
      item.chunkId === chunkId ? { ...item, text: newText, metadatas: newMetadatas !== undefined ? newMetadatas : item.metadatas } : item
    );
    setPreviewData({ ...currentData, items: updatedItems });
    try {
      await updateChunkContent(chunkId, newText, itemToUpdate.order ?? 0, newMetadatas !== undefined ? newMetadatas : itemToUpdate.metadatas);
    } catch (error) {
      await fetchPartitionPreview();
      throw error;
    }
  }, [updateChunkContent, fetchPartitionPreview]);

  const deleteItem = useCallback(async (chunkId: string) => {
    if (!wikiId || !documentId || !chunkId?.trim()) return;
    try {
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.delete_chunk.post({ wikiId: parseInt(wikiId), documentId: parseInt(documentId), chunkId });
      messageApi.success("文本块已删除");
      await fetchPartitionPreview();
    } catch (error) {
      console.error("Failed to delete item:", error);
      proxyRequestError(error, messageApi, "删除文本块失败");
    }
  }, [wikiId, documentId, messageApi, fetchPartitionPreview]);

  const reorderItems = useCallback(async (fromIndex: number, toIndex: number) => {
    const currentData = previewDataRef.current;
    if (!currentData || fromIndex < 0 || fromIndex >= currentData.items.length || toIndex < 0 || toIndex >= currentData.items.length) return;
    const newItems = [...currentData.items];
    const [removed] = newItems.splice(fromIndex, 1);
    newItems.splice(toIndex, 0, removed);
    const updatedItems = newItems.map((item, index) => ({ ...item, order: index }));
    setPreviewData({ ...currentData, items: updatedItems });
    try {
      await reorderAndSave(updatedItems);
      await fetchPartitionPreview();
    } catch (error) {
      await fetchPartitionPreview();
    }
  }, [reorderAndSave, fetchPartitionPreview]);

  const addChunk = useCallback(async (text: string, metadatas?: WikiDocumentMetadataItem[] | null) => {
    if (!wikiId || !documentId || !text?.trim()) return;
    try {
      const currentData = previewDataRef.current;
      const maxOrder = currentData?.items?.length ? Math.max(...currentData.items.map((item) => item.order ?? 0)) : -1;
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.add_chunk.post({ wikiId: parseInt(wikiId), documentId: parseInt(documentId), text, order: maxOrder + 1, metadatas: metadatas || null });
      messageApi.success("文本块已添加");
      await fetchPartitionPreview();
    } catch (error) {
      console.error("Failed to add chunk:", error);
      proxyRequestError(error, messageApi, "添加文本块失败");
    }
  }, [wikiId, documentId, messageApi, fetchPartitionPreview]);

  // 编辑器操作
  const handleOpenChunkEditor = useCallback((chunkId: string | null, currentText: string, currentMetadatas?: WikiDocumentMetadataItem[] | null) => {
    setEditingChunkId(chunkId);
    setEditingChunkText(currentText || "");
    setEditingChunkMetadatas(currentMetadatas || null);
    setChunkEditModalVisible(true);
  }, []);

  const handleCloseChunkEditor = useCallback(() => {
    setChunkEditModalVisible(false);
    setEditingChunkId(null);
    setEditingChunkText("");
    setEditingChunkMetadatas(null);
  }, []);

  const handleSaveChunk = useCallback(async (text: string, metadatas: WikiDocumentMetadataItem[] | null) => {
    try {
      if (editingChunkId) {
        await updateItem(editingChunkId, text, metadatas);
      } else {
        await addChunk(text, metadatas);
      }
      handleCloseChunkEditor();
    } catch (error) {
      console.error("Failed to save chunk:", error);
      proxyRequestError(error, messageApi, "保存失败，请重试");
    }
  }, [editingChunkId, updateItem, addChunk, handleCloseChunkEditor, messageApi]);

  // 拖拽处理
  const handleDragStart = useCallback((e: React.DragEvent, index: number) => { setDraggedIndex(index); e.dataTransfer.effectAllowed = "move"; }, []);
  const handleDragOver = useCallback((e: React.DragEvent, index: number) => { e.preventDefault(); if (draggedIndex !== null && draggedIndex !== index) setDragOverIndex(index); }, [draggedIndex]);
  const handleDragLeave = useCallback((e: React.DragEvent) => { e.preventDefault(); setDragOverIndex(null); }, []);
  const handleDragEnd = useCallback(() => { setDraggedIndex(null); setDragOverIndex(null); }, []);
  const handleDrop = useCallback(async (e: React.DragEvent, dropIndex: number) => {
    e.preventDefault();
    if (draggedIndex !== null && draggedIndex !== dropIndex) await reorderItems(draggedIndex, dropIndex);
    setDraggedIndex(null);
    setDragOverIndex(null);
  }, [draggedIndex, reorderItems]);

  // 批量生成
  const handleBatchGenerate = useCallback(async () => {
    if (!batchAiModelId || !batchPreprocessStrategyType || selectedChunkIds.size === 0 || !previewData) {
      messageApi.warning("请选择AI模型、优化策略和至少一个文本块");
      return;
    }
    try {
      setBatchGenerating(true);
      const apiClient = GetApiClient();
      const selectedItems = previewData.items.filter(item => selectedChunkIds.has(item.chunkId));
      const chunks: KeyValueOfLongAndString[] = selectedItems.map(item => ({ key: item.chunkId, value: item.text }));
      const response = await apiClient.api.wiki.document.ai_generation_chunk.post({ wikiId: parseInt(wikiId), aiModelId: batchAiModelId, chunks, preprocessStrategyType: batchPreprocessStrategyType });
      if (response?.items && response.items.length > 0) {
        const resultsMap = new Map<string, WikiDocumentMetadataItem[]>();
        response.items.forEach((item: KeyValueOfLongAndParagraphPreprocessResult) => {
          if (item.key && item.value) {
            const metadatas: WikiDocumentMetadataItem[] = [];
            const result = item.value;
            if (result.metadata && result.metadata.length > 0) {
              result.metadata.forEach((meta: KeyValueString) => {
                if (meta.key && meta.value) {
                  const metadataType = inferMetadataTypeFromKey(meta.key, batchPreprocessStrategyType);
                  if (metadataType) metadatas.push({ metadataType, metadataContent: meta.value });
                }
              });
            }
            if (metadatas.length === 0) {
              const metadataType = inferMetadataTypeFromStrategy(batchPreprocessStrategyType);
              const content = result.processedText || result.originalText || "";
              if (content && metadataType) metadatas.push({ metadataType, metadataContent: content });
            }
            if (metadatas.length > 0) resultsMap.set(item.key, metadatas);
          }
        });
        setBatchGenerateResults(resultsMap);
        messageApi.success(`成功生成 ${resultsMap.size} 个文本块的元数据`);
      } else {
        messageApi.warning("AI生成未返回结果");
      }
    } catch (error) {
      console.error("Failed to batch generate:", error);
      proxyRequestError(error, messageApi);
    } finally {
      setBatchGenerating(false);
    }
  }, [batchAiModelId, batchPreprocessStrategyType, selectedChunkIds, previewData, wikiId, messageApi]);

  const handleSaveBatchResults = useCallback(async () => {
    if (batchGenerateResults.size === 0) {
      messageApi.warning("没有可保存的结果");
      return;
    }
    try {
      const apiClient = GetApiClient();
      const metadatasToAdd: AddWikiDocumentMetadataItem[] = [];
      batchGenerateResults.forEach((metadatas, chunkId) => {
        metadatas.forEach((m) => {
          metadatasToAdd.push({ chunkId, metadataContent: m.metadataContent || null, metadataType: m.metadataType || null });
        });
      });
      if (metadatasToAdd.length === 0) {
        messageApi.warning("没有可保存的元数据");
        return;
      }
      await apiClient.api.wiki.document.add_chunk_metadatas.post({ wikiId: parseInt(wikiId), documentId: parseInt(documentId), metadatas: metadatasToAdd });
      messageApi.success(`成功保存 ${metadatasToAdd.length} 条元数据`);
      await fetchPartitionPreview();
      setBatchGenerateResults(new Map());
      setBatchGenerateVisible(false);
      setSelectedChunkIds(new Set());
      setBatchAiModelId(null);
      setBatchPreprocessStrategyType(null);
    } catch (error) {
      console.error("Failed to save batch results:", error);
      proxyRequestError(error, messageApi);
    }
  }, [batchGenerateResults, wikiId, documentId, messageApi, fetchPartitionPreview]);

  const closeBatchModal = useCallback(() => {
    setBatchGenerateVisible(false);
    setBatchGenerateResults(new Map());
    setSelectedChunkIds(new Set());
    setBatchAiModelId(null);
    setBatchPreprocessStrategyType(null);
  }, []);

  if (!wikiId || !documentId) {
    return <Empty description="缺少必要的参数" />;
  }

  return (
    <>
      {contextHolder}
      <Collapse defaultActiveKey={["preview"]} className="doc-embed-collapse">
        <Panel
          header={
            <Space>
              <FileTextOutlined />
              <span>切割预览</span>
              <Button type="text" icon={<ReloadOutlined />} onClick={(e) => { e.stopPropagation(); fetchPartitionPreview(); }} loading={loading} size="small" />
              <Button type="text" icon={<PlusOutlined />} onClick={(e) => { e.stopPropagation(); handleOpenChunkEditor(null, "", null); }} size="small">新增块</Button>
              <Button type="text" icon={<ThunderboltOutlined />} onClick={(e) => { e.stopPropagation(); setBatchGenerateVisible(true); if (previewData?.items.length) setSelectedChunkIds(new Set(previewData.items.map(item => item.chunkId))); }} size="small">批量生成策略</Button>
            </Space>
          }
          key="preview"
        >
          {loading ? (
            <Empty description="加载中..." />
          ) : previewData && previewData.items.length > 0 ? (
            <div className="chunk-preview-grid">
              {previewData.items.map((item, index) => {
                const isDragging = draggedIndex === index;
                const isDragOver = dragOverIndex === index;
                return (
                  <Card
                    key={item.chunkId}
                    size="small"
                    draggable
                    onDragStart={(e) => handleDragStart(e, index)}
                    onDragOver={(e) => handleDragOver(e, index)}
                    onDragLeave={handleDragLeave}
                    onDragEnd={handleDragEnd}
                    onDrop={(e) => handleDrop(e, index)}
                    className={`chunk-card ${isDragging ? "dragging" : ""} ${isDragOver ? "drag-over" : ""}`}
                    title={
                      <div className="chunk-card-header">
                        <Space>
                          <DragOutlined className="chunk-drag-icon" />
                          <Tag color="blue">#{item.order + 1}</Tag>
                          <Tag color="default" title={item.chunkId || "未设置"}>ID: {item.chunkId?.length > 20 ? `${item.chunkId.substring(0, 20)}...` : item.chunkId || "N/A"}</Tag>
                        </Space>
                        <Space>
                          <Button type="text" size="small" icon={<EditOutlined />} onClick={(e) => { e.stopPropagation(); handleOpenChunkEditor(item.chunkId, item.text, item.metadatas); }}>编辑</Button>
                          <Popconfirm title="确认删除" description="确定要删除这个文本块吗？" onConfirm={(e) => { e?.stopPropagation(); deleteItem(item.chunkId); }} okText="确认" cancelText="取消">
                            <Button type="text" size="small" danger icon={<DeleteOutlined />} onClick={(e) => e.stopPropagation()}>删除</Button>
                          </Popconfirm>
                        </Space>
                      </div>
                    }
                  >
                    <div className={item.metadatas?.length ? "chunk-text-section" : ""}>
                      <Typography.Text type="secondary" className="chunk-text-label">文本内容</Typography.Text>
                      <Typography.Paragraph className="chunk-text-content" ellipsis={{ rows: 8, expandable: false }}>
                        {item.text || "(空)"}
                      </Typography.Paragraph>
                    </div>
                    {item.metadatas && item.metadatas.length > 0 && (
                      <div className="chunk-metadata-section">
                        <div className="chunk-metadata-header">
                          <Typography.Text type="secondary" style={{ fontSize: 12 }}>元数据 ({item.metadatas.length})</Typography.Text>
                          <Button
                            type="text"
                            size="small"
                            icon={expandedMetadatas.has(item.chunkId) ? <UpOutlined /> : <DownOutlined />}
                            onClick={(e) => { e.stopPropagation(); const newExpanded = new Set(expandedMetadatas); if (newExpanded.has(item.chunkId)) newExpanded.delete(item.chunkId); else newExpanded.add(item.chunkId); setExpandedMetadatas(newExpanded); }}
                            style={{ padding: 0, height: "auto" }}
                          >
                            {expandedMetadatas.has(item.chunkId) ? "收起" : "展开"}
                          </Button>
                        </div>
                        {expandedMetadatas.has(item.chunkId) && (
                          <Space direction="vertical" size="small" style={{ width: "100%" }}>
                            {item.metadatas.map((meta, idx) => (
                              <div key={idx} className="chunk-metadata-item">
                                <Tag color="purple">{getMetadataTypeDisplayName(meta.metadataType)}</Tag>
                                <Typography.Text className="chunk-metadata-content">{meta.metadataContent || "(空)"}</Typography.Text>
                              </div>
                            ))}
                          </Space>
                        )}
                      </div>
                    )}
                  </Card>
                );
              })}
            </div>
          ) : (
            <Empty description="暂无切割预览数据" />
          )}
        </Panel>
      </Collapse>

      <ChunkEditModal open={chunkEditModalVisible} chunkId={editingChunkId} initialText={editingChunkText} initialMetadatas={editingChunkMetadatas} wikiId={wikiId} modelList={modelList} onClose={handleCloseChunkEditor} onSave={handleSaveChunk} />

      {/* 批量生成策略模态窗口 */}
      <Modal
        title="批量生成策略"
        open={batchGenerateVisible}
        onCancel={closeBatchModal}
        width={800}
        maskClosable={false}
        footer={[
          <Button key="cancel" onClick={closeBatchModal}>取消</Button>,
          batchGenerateResults.size > 0 && <Button key="save" type="primary" onClick={handleSaveBatchResults}>保存结果</Button>,
          <Button key="generate" type="primary" loading={batchGenerating} onClick={handleBatchGenerate} disabled={!batchAiModelId || !batchPreprocessStrategyType || selectedChunkIds.size === 0}>生成</Button>,
        ].filter(Boolean)}
        destroyOnClose
      >
        <Space direction="vertical" style={{ width: "100%" }} size="large">
          <div className="batch-section">
            <div className="batch-section-label">AI 模型</div>
            <Select placeholder="请选择AI模型" value={batchAiModelId} onChange={setBatchAiModelId} style={{ width: "100%" }} loading={modelListLoading} options={modelList.map((model) => ({ label: model.name, value: model.id }))} />
          </div>
          <div className="batch-section">
            <div className="batch-section-label">优化策略</div>
            <Select placeholder="请选择优化策略" value={batchPreprocessStrategyType} onChange={setBatchPreprocessStrategyType} style={{ width: "100%" }} options={PreprocessStrategyOptions} />
          </div>
          <div className="batch-section">
            <div className="batch-section-label">选择文本块 ({selectedChunkIds.size} / {previewData?.items.length || 0})</div>
            <div style={{ marginBottom: 8 }}>
              <Space>
                <Button type="link" size="small" onClick={() => { if (previewData) setSelectedChunkIds(new Set(previewData.items.map(item => item.chunkId))); }}>全选</Button>
                <Button type="link" size="small" onClick={() => setSelectedChunkIds(new Set())}>全不选</Button>
              </Space>
            </div>
            <div className="batch-chunk-list">
              <Space direction="vertical" style={{ width: "100%" }} size="small">
                {previewData?.items.map((item) => (
                  <div key={item.chunkId} className="batch-chunk-item">
                    <Checkbox checked={selectedChunkIds.has(item.chunkId)} onChange={(e) => { const newSelected = new Set(selectedChunkIds); if (e.target.checked) newSelected.add(item.chunkId); else newSelected.delete(item.chunkId); setSelectedChunkIds(newSelected); }}>
                      <div className="batch-chunk-info">
                        <div className="batch-chunk-title">#{item.order + 1} - {item.chunkId.substring(0, 20)}...</div>
                        <Typography.Text type="secondary" style={{ fontSize: 12 }} ellipsis={{ tooltip: item.text || "(空)" }}>{item.text || "(空)"}</Typography.Text>
                      </div>
                    </Checkbox>
                  </div>
                ))}
              </Space>
            </div>
          </div>
          {batchGenerateResults.size > 0 && (
            <div className="batch-section">
              <div className="batch-section-label">生成结果 ({batchGenerateResults.size} 个文本块)</div>
              <div className="batch-results-container">
                <Space direction="vertical" style={{ width: "100%" }} size="small">
                  {Array.from(batchGenerateResults.entries()).map(([chunkId, metadatas]) => {
                    const item = previewData?.items.find(i => i.chunkId === chunkId);
                    return (
                      <div key={chunkId} className="batch-result-item">
                        <div className="batch-result-title">#{item?.order !== undefined ? item.order + 1 : "?"} - {chunkId.substring(0, 20)}...</div>
                        <Space direction="vertical" size="small" style={{ width: "100%" }}>
                          {metadatas.map((m, idx) => (
                            <div key={idx} className="batch-result-metadata">
                              <div className="batch-result-metadata-content">
                                <Tag color="purple" style={{ marginRight: 8 }}>{getMetadataTypeDisplayName(m.metadataType)}</Tag>
                                <Typography.Text style={{ fontSize: 12 }} ellipsis={{ tooltip: m.metadataContent || "(空)" }}>{m.metadataContent || "(空)"}</Typography.Text>
                              </div>
                              <Button type="text" danger size="small" icon={<DeleteOutlined />} onClick={() => { const newResults = new Map(batchGenerateResults); const current = newResults.get(chunkId) || []; const updated = current.filter((_, i) => i !== idx); if (updated.length > 0) newResults.set(chunkId, updated); else newResults.delete(chunkId); setBatchGenerateResults(newResults); }} style={{ marginLeft: 8, flexShrink: 0 }} />
                            </div>
                          ))}
                        </Space>
                      </div>
                    );
                  })}
                </Space>
              </div>
            </div>
          )}
        </Space>
      </Modal>
    </>
  );
});

export default ChunkPreview;
