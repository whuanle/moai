import { useState, useEffect, useCallback, useRef } from "react";
import { useParams } from "react-router";
import {
  Card,
  Form,
  Button,
  message,
  InputNumber,
  Select,
  Table,
  Space,
  Tooltip,
  Typography,
  Row,
  Col,
  Statistic,
  Alert,
  Empty,
  Tag,
  Collapse,
  Tabs,
  Input,
  Popconfirm,
  Modal,
  List,
  Divider,
  Checkbox,
} from "antd";

import { GetApiClient } from "../ServiceClient";
import {
  ReloadOutlined,
  FileTextOutlined,
  ClockCircleOutlined,
  ScissorOutlined,
  EditOutlined,
  DeleteOutlined,
  DragOutlined,
  PlusOutlined,
  CloseOutlined,
  CheckOutlined,
  DownOutlined,
  UpOutlined,
  ThunderboltOutlined,
} from "@ant-design/icons";
import { formatDateTime } from "../../helper/DateTimeHelper";
import CodeEditorModal from "../common/CodeEditorModal";
import type { 
  WikiDocumentDerivativeItem, 
  ParagrahProcessorMetadataType, 
  ParagrahProcessorMetadataType as DerivativeType,
  PreprocessStrategyType,
  KeyValueOfInt64AndString,
  AddWikiDocumentDerivativeItem,
} from "../../apiClient/models";
import { proxyRequestError } from "../../helper/RequestError";

const { Panel } = Collapse;

// 类型定义
interface DocumentInfo {
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

interface TaskInfo {
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

interface PartitionPreviewItem {
  chunkId: string;
  order: number;
  text: string;
  derivatives?: WikiDocumentDerivativeItem[] | null;
}

// 衍生类型映射（支持多种格式）
const DerivativeTypeMap: Record<string, string> = {
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
};

// 衍生类型选项
const DerivativeTypeOptions = [
  { label: "大纲", value: "outline" },
  { label: "问题", value: "question" },
  { label: "关键词", value: "keyword" },
  { label: "摘要", value: "summary" },
  { label: "聚合段落", value: "aggregatedSubParagraph" },
];

// 预处理策略选项
const PreprocessStrategyOptions = [
  { label: "大纲生成", value: "outlineGeneration" },
  { label: "问题生成", value: "questionGeneration" },
  { label: "关键词摘要融合", value: "keywordSummaryFusion" },
  { label: "语义聚合", value: "semanticAggregation" },
];

// Chunk 编辑组件
interface ChunkEditModalProps {
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

const ChunkEditModal: React.FC<ChunkEditModalProps> = ({
  open,
  chunkId,
  initialText,
  initialDerivatives,
  wikiId,
  documentId,
  modelList,
  onClose,
  onSave,
}) => {
  const [textEditorVisible, setTextEditorVisible] = useState(false);
  const [textEditorValue, setTextEditorValue] = useState(initialText);
  const [derivatives, setDerivatives] = useState<WikiDocumentDerivativeItem[]>(
    initialDerivatives || []
  );
  const [editingDerivativeIndex, setEditingDerivativeIndex] = useState<number | null>(null);
  const [newDerivativeType, setNewDerivativeType] = useState<DerivativeType | null>(null);
  const [newDerivativeContent, setNewDerivativeContent] = useState<string>("");
  const [aiGenerateVisible, setAiGenerateVisible] = useState(false);
  const [aiModelId, setAiModelId] = useState<number | null>(null);
  const [preprocessStrategyType, setPreprocessStrategyType] = useState<PreprocessStrategyType | null>(null);
  const [aiGenerating, setAiGenerating] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  useEffect(() => {
    if (open) {
      setTextEditorValue(initialText);
      setDerivatives(initialDerivatives || []);
    }
  }, [open, initialText, initialDerivatives]);

  const handleTextEditorConfirm = (value: string) => {
    setTextEditorValue(value);
    setTextEditorVisible(false);
  };

  const handleAddDerivative = () => {
    if (!newDerivativeType || !newDerivativeContent.trim()) {
      message.warning("请选择类型并输入内容");
      return;
    }
    setDerivatives([
      ...derivatives,
      {
        derivativeType: newDerivativeType,
        derivativeContent: newDerivativeContent.trim(),
      },
    ]);
    setNewDerivativeType(null);
    setNewDerivativeContent("");
  };

  const handleEditDerivative = (index: number, type: DerivativeType, content: string) => {
    const updated = [...derivatives];
    updated[index] = {
      derivativeType: type,
      derivativeContent: content,
    };
    setDerivatives(updated);
    setEditingDerivativeIndex(null);
  };

  const handleDeleteDerivative = (index: number) => {
    setDerivatives(derivatives.filter((_, i) => i !== index));
  };

  const handleSave = () => {
    onSave(textEditorValue, derivatives.length > 0 ? derivatives : null);
  };

  const handleAiGenerate = useCallback(async () => {
    if (!chunkId) {
      messageApi.warning("请先保存文本块");
      return;
    }
    if (!aiModelId) {
      messageApi.warning("请选择AI模型");
      return;
    }
    if (!preprocessStrategyType) {
      messageApi.warning("请选择优化策略");
      return;
    }
    if (!textEditorValue.trim()) {
      messageApi.warning("文本内容不能为空");
      return;
    }

    try {
      setAiGenerating(true);
      const apiClient = GetApiClient();
      
      // 构建 chunks 参数
      const chunks: KeyValueOfInt64AndString[] = [{
        key: chunkId,
        value: textEditorValue,
      }];

      const response = await apiClient.api.wiki.document.ai_generation_chunk.post({
        wikiId: parseInt(wikiId),
        aiModelId: aiModelId,
        chunks: chunks,
        preprocessStrategyType: preprocessStrategyType,
      });

      if (response?.items && response.items.length > 0) {
        // 处理响应，将结果添加到 derivatives
        const newDerivatives: WikiDocumentDerivativeItem[] = [];
        
        response.items.forEach((item) => {
          if (item.value) {
            const result = item.value;
            
            // 处理 metadata（包含关键词、摘要、问题列表等）
            if (result.metadata && result.metadata.length > 0) {
              result.metadata.forEach((meta) => {
                if (meta.key && meta.value) {
                  // 根据 key 确定衍生类型
                  let derivativeType: DerivativeType | null = null;
                  const keyLower = meta.key.toLowerCase();
                  if (keyLower.includes("outline") || keyLower.includes("大纲")) {
                    derivativeType = "outline";
                  } else if (keyLower.includes("question") || keyLower.includes("问题")) {
                    derivativeType = "question";
                  } else if (keyLower.includes("keyword") || keyLower.includes("关键词")) {
                    derivativeType = "keyword";
                  } else if (keyLower.includes("summary") || keyLower.includes("摘要")) {
                    derivativeType = "summary";
                  } else {
                    // 默认根据策略类型确定
                    switch (preprocessStrategyType) {
                      case "outlineGeneration":
                        derivativeType = "outline";
                        break;
                      case "questionGeneration":
                        derivativeType = "question";
                        break;
                      case "keywordSummaryFusion":
                        derivativeType = "keyword";
                        break;
                      case "semanticAggregation":
                        derivativeType = "aggregatedSubParagraph";
                        break;
                    }
                  }
                  
                  if (derivativeType) {
                    newDerivatives.push({
                      derivativeType: derivativeType,
                      derivativeContent: meta.value,
                    });
                  }
                }
              });
            }
            
            // 如果没有 metadata，使用 preprocessedText 或 originalText
            if (newDerivatives.length === 0) {
              let derivativeType: DerivativeType | null = null;
              switch (preprocessStrategyType) {
                case "outlineGeneration":
                  derivativeType = "outline";
                  break;
                case "questionGeneration":
                  derivativeType = "question";
                  break;
                case "keywordSummaryFusion":
                  derivativeType = "keyword";
                  break;
                case "semanticAggregation":
                  derivativeType = "aggregatedSubParagraph";
                  break;
              }
              
              const content = result.processedText || result.originalText || "";
              if (content && derivativeType) {
                newDerivatives.push({
                  derivativeType: derivativeType,
                  derivativeContent: content,
                });
              }
            }
          }
        });

        if (newDerivatives.length > 0) {
          setDerivatives([...derivatives, ...newDerivatives]);
          messageApi.success(`成功生成 ${newDerivatives.length} 个衍生内容`);
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
      console.error("Failed to generate AI derivatives:", error);

      proxyRequestError(error, messageApi, "AI生成失败，请重试");
    } finally {
      setAiGenerating(false);
    }
  }, [chunkId, aiModelId, preprocessStrategyType, textEditorValue, wikiId, derivatives, messageApi]);

  return (
    <>
      <Modal
        title={chunkId ? "编辑文本块" : "新增文本块"}
        open={open}
        onCancel={onClose}
        onOk={handleSave}
        width={1000}
        okText="保存"
        cancelText="取消"
        destroyOnClose
      >
        <div style={{ marginBottom: 24 }}>
          <div style={{ marginBottom: 8, fontWeight: 500 }}>文本内容</div>
          <Button
            type="default"
            icon={<EditOutlined />}
            onClick={() => setTextEditorVisible(true)}
            style={{ width: "100%" }}
          >
            编辑文本
          </Button>
          <Typography.Paragraph
            style={{
              marginTop: 12,
              padding: 12,
              backgroundColor: "#f5f5f5",
              borderRadius: 4,
              maxHeight: 200,
              overflow: "auto",
              whiteSpace: "pre-wrap",
              wordBreak: "break-word",
            }}
          >
            {textEditorValue || "(空)"}
          </Typography.Paragraph>
        </div>

        <Divider />

        <div>
          <div style={{ marginBottom: 16, fontWeight: 500, display: "flex", justifyContent: "space-between", alignItems: "center" }}>
            <span>衍生内容</span>
            <Button
              type="dashed"
              icon={<PlusOutlined />}
              onClick={() => setAiGenerateVisible(!aiGenerateVisible)}
              disabled={!chunkId}
            >
              AI 生成策略
            </Button>
          </div>

          {/* AI 生成策略面板 */}
          {aiGenerateVisible && (
            <div style={{ marginBottom: 16, padding: 16, backgroundColor: "#f0f7ff", borderRadius: 4, border: "1px solid #91d5ff" }}>
              <Space direction="vertical" style={{ width: "100%" }} size="middle">
                <div>
                  <div style={{ marginBottom: 8, fontSize: 13, fontWeight: 500 }}>AI 模型</div>
                  <Select
                    placeholder="请选择AI模型"
                    value={aiModelId}
                    onChange={setAiModelId}
                    style={{ width: "100%" }}
                    options={modelList.map((model) => ({
                      label: model.name,
                      value: model.id,
                    }))}
                  />
                </div>
                <div>
                  <div style={{ marginBottom: 8, fontSize: 13, fontWeight: 500 }}>优化策略</div>
                  <Select
                    placeholder="请选择优化策略"
                    value={preprocessStrategyType}
                    onChange={setPreprocessStrategyType}
                    style={{ width: "100%" }}
                    options={PreprocessStrategyOptions}
                  />
                </div>
                <Space style={{ width: "100%", justifyContent: "flex-end" }}>
                  <Button onClick={() => {
                    setAiGenerateVisible(false);
                    setAiModelId(null);
                    setPreprocessStrategyType(null);
                  }}>
                    取消
                  </Button>
                  <Button
                    type="primary"
                    loading={aiGenerating}
                    onClick={handleAiGenerate}
                    disabled={!aiModelId || !preprocessStrategyType}
                  >
                    生成
                  </Button>
                </Space>
              </Space>
            </div>
          )}
          
          {/* 新增衍生内容 */}
          <div style={{ marginBottom: 16, padding: 12, backgroundColor: "#fafafa", borderRadius: 4 }}>
            <Space.Compact style={{ width: "100%" }}>
              <Select
                placeholder="选择类型"
                value={newDerivativeType}
                onChange={setNewDerivativeType}
                style={{ width: 150 }}
                options={DerivativeTypeOptions}
              />
              <Input
                placeholder="输入内容"
                value={newDerivativeContent}
                onChange={(e) => setNewDerivativeContent(e.target.value)}
                onPressEnter={handleAddDerivative}
                style={{ flex: 1 }}
              />
              <Button type="primary" icon={<PlusOutlined />} onClick={handleAddDerivative}>
                添加
              </Button>
            </Space.Compact>
          </div>

          {/* 衍生内容列表 */}
          <List
            dataSource={derivatives}
            locale={{ emptyText: "暂无衍生内容" }}
            renderItem={(item, index) => (
              <List.Item
                actions={[
                  <Button
                    key="edit"
                    type="link"
                    size="small"
                    icon={<EditOutlined />}
                    onClick={() => setEditingDerivativeIndex(index)}
                  >
                    编辑
                  </Button>,
                  <Popconfirm
                    key="delete"
                    title="确认删除"
                    description="确定要删除这个衍生内容吗？"
                    onConfirm={() => handleDeleteDerivative(index)}
                    okText="确认"
                    cancelText="取消"
                  >
                    <Button
                      type="link"
                      size="small"
                      danger
                      icon={<DeleteOutlined />}
                    >
                      删除
                    </Button>
                  </Popconfirm>,
                ]}
              >
                {editingDerivativeIndex === index ? (
                  <Space.Compact style={{ width: "100%" }}>
                    <Select
                      value={item.derivativeType || null}
                      onChange={(value) => {
                        const updated = [...derivatives];
                        updated[index] = {
                          ...updated[index],
                          derivativeType: value,
                        };
                        setDerivatives(updated);
                      }}
                      style={{ width: 150 }}
                      options={DerivativeTypeOptions}
                    />
                    <Input
                      value={item.derivativeContent || ""}
                      onChange={(e) => {
                        const updated = [...derivatives];
                        updated[index] = {
                          ...updated[index],
                          derivativeContent: e.target.value,
                        };
                        setDerivatives(updated);
                      }}
                      onPressEnter={() => setEditingDerivativeIndex(null)}
                      style={{ flex: 1 }}
                    />
                    <Button
                      type="primary"
                      icon={<CheckOutlined />}
                      onClick={() => setEditingDerivativeIndex(null)}
                    >
                      完成
                    </Button>
                  </Space.Compact>
                ) : (
                  <List.Item.Meta
                    title={
                      <Tag color="purple">
                        {DerivativeTypeMap[item.derivativeType || ""] || item.derivativeType || "未知"}
                      </Tag>
                    }
                    description={
                      <Typography.Text
                        style={{
                          fontSize: 13,
                          color: "#666",
                          whiteSpace: "pre-wrap",
                          wordBreak: "break-word",
                        }}
                      >
                        {item.derivativeContent || "(空)"}
                      </Typography.Text>
                    }
                  />
                )}
              </List.Item>
            )}
          />
        </div>
      </Modal>

      {/* 文本编辑器 */}
      <CodeEditorModal
        open={textEditorVisible}
        initialValue={textEditorValue}
        language="plaintext"
        title="编辑文本内容"
        onClose={() => setTextEditorVisible(false)}
        onConfirm={handleTextEditorConfirm}
        width={1200}
        height="70vh"
      />
    </>
  );
};

// 自定义Hook - 文档信息管理
const useDocumentInfo = (wikiId: string, documentId: string) => {
  const [documentInfo, setDocumentInfo] = useState<DocumentInfo | null>(null);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchDocumentInfo = useCallback(async () => {
    if (!wikiId || !documentId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.document_info.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      if (response) {
        setDocumentInfo({
          documentId: response.documentId!,
          fileName: response.fileName || "",
          fileSize: response.fileSize || 0,
          contentType: response.contentType || "",
          createTime: response.createTime || "",
          createUserName: response.createUserName || "",
          embedding: false, // QueryWikiDocumentInfoCommandResponse 不包含 embedding 字段
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
      messageApi.error("获取文档信息失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  return { documentInfo, loading, contextHolder, fetchDocumentInfo };
};

// 自定义Hook - 任务列表管理
const useTaskList = (wikiId: string, documentId: string) => {
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchTasks = useCallback(async () => {
    if (!wikiId || !documentId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.wiki.document.task_list.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      if (response) {
        const formattedTasks: TaskInfo[] = response.map((task: any) => ({
          id: task.id || "",
          fileName: task.fileName || "",
          tokenizer: task.tokenizer || "",
          maxTokensPerParagraph: task.maxTokensPerParagraph || 0,
          overlappingTokens: task.overlappingTokens || 0,
          state: task.state || "",
          message: task.message || "",
          createTime: task.createTime || "",
          documentId: task.documentId || "",
        }));
        setTasks(formattedTasks);
      }
    } catch (error) {
      console.error("Failed to fetch tasks:", error);
      messageApi.error("获取任务列表失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  const cancelTask = useCallback(
    async (taskId: string) => {
      try {
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.create_document.post({
          taskId: taskId,
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
        });

        messageApi.success("任务已取消");
        fetchTasks();
      } catch (error) {
        console.error("Failed to cancel task:", error);
        messageApi.error("取消任务失败");
      }
    },
    [wikiId, documentId, messageApi, fetchTasks]
  );

  return { tasks, loading, contextHolder, fetchTasks, cancelTask };
};

// 自定义Hook - 获取切割预览
const usePartitionPreview = (wikiId: string, documentId: string) => {
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
          items: sortedItems.map((item) => {
            // 调试：检查原始数据
            if (item.derivatives && item.derivatives.length > 0) {
              console.log("Raw item derivatives:", item.derivatives);
              console.log("First derivative:", item.derivatives[0]);
              console.log("Keys in first derivative:", item.derivatives[0] ? Object.keys(item.derivatives[0]) : []);
            }
            
            return {
              chunkId: item.chunkId ?? "",
              order: item.order ?? 0,
              text: item.text ?? "",
              // 直接使用 API 返回的 derivatives，不做额外映射
              // API 客户端已经处理了字段名转换
              derivatives: item.derivatives ?? null,
            };
          }),
          chunkHeader: null, // 新API可能不返回这些字段
          maxTokensPerChunk: null,
          overlap: null,
        });
      }
    } catch (error) {
      console.error("Failed to fetch partition preview:", error);
      messageApi.error("获取切割预览失败");
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
      // 使用 update_chunks_order API 更新排序
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
      messageApi.error("更新排序失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi]);

  // 更新chunk内容
  const updateChunkContent = useCallback(async (chunkId: string, text: string, order: number, derivatives?: WikiDocumentDerivativeItem[] | null) => {
    if (!wikiId || !documentId) {
      console.error("Missing wikiId or documentId:", { wikiId, documentId });
      return;
    }

    try {
      const apiClient = GetApiClient();
      // 使用 update_chnuks API 更新chunk（传入单个chunk的数组）
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
      messageApi.error("更新文本块失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi]);

  const updateItem = useCallback(
    async (chunkId: string, newText: string, newDerivatives?: WikiDocumentDerivativeItem[] | null) => {
      // 直接从 ref 获取最新的 previewData，避免闭包问题
      const currentData = previewDataRef.current;
      if (!currentData) {
        console.error("Cannot update item: previewData is null");
        return;
      }
      
      // 找到要更新的item
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
      messageApi.error("删除文本块失败");
      throw error;
    }
  }, [wikiId, documentId, messageApi, fetchPartitionPreview, reorderAndSave]);

  const reorderItems = useCallback(async (fromIndex: number, toIndex: number) => {
    // 直接从 ref 获取最新的 previewData，避免闭包问题
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
      // 重新获取数据以确保同步
      await fetchPartitionPreview();
    } catch (error) {
      // 如果更新失败，重新获取数据以恢复状态
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
      messageApi.error("添加文本块失败");
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
};

// 自定义Hook - 文档切割操作
const usePartitionOperations = (
  wikiId: string,
  documentId: string,
  onSuccess: () => void
) => {
  const [loading, setLoading] = useState(false);
  const [previewItems, setPreviewItems] = useState<PartitionPreviewItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();

  const submitPartition = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }

      try {
        setLoading(true);
        const apiClient = GetApiClient();
        const response = await apiClient.api.wiki.document.text_partition_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          maxTokensPerChunk: values.maxTokensPerChunk,
          overlap: values.overlap,
          chunkHeader: values.chunkHeader || null,
        });

        if (response) {
          messageApi.success("文档切割预览成功");
          // 注意：根据API定义，响应可能不包含items，这里需要根据实际API响应调整
          // 如果API返回items，需要从响应中获取
          onSuccess();
        }
      } catch (error) {
        console.error("Failed to submit partition task:", error);
        messageApi.error("文档切割失败");
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
};

// 自定义Hook - 获取AI模型列表
const useAiModelList = () => {
  const [modelList, setModelList] = useState<Array<{ id: number; name: string }>>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchModelList = useCallback(async () => {
    try {
      setLoading(true);
      const apiClient = GetApiClient();
      const response = await apiClient.api.aimodel.modellist.post({
        aiModelType: "chat",
      });

      if (response?.aiModels) {
        const models = response.aiModels
          .filter((item: any) => item.id != null && item.name != null)
          .map((item: any) => ({
            id: item.id!,
            name: item.name || "",
          }));
        setModelList(models);
      }
    } catch (error) {
      console.error("Failed to fetch AI model list:", error);
      messageApi.error("获取AI模型列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  return {
    modelList,
    loading,
    contextHolder,
    fetchModelList,
  };
};

// 自定义Hook - 智能切割操作
const useAiPartitionOperations = (
  wikiId: string,
  documentId: string,
  onSuccess: () => void
) => {
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const submitAiPartition = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }

      if (!values.aiModelId) {
        messageApi.error("请选择AI模型");
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
        messageApi.error("智能切割失败");
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
};

// 自定义Hook - 向量化操作
const useEmbeddingOperations = (
  wikiId: string,
  documentId: string,
  onSuccess: () => void
) => {
  const [loading, setLoading] = useState(false);
  const [clearLoading, setClearLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const submitEmbedding = useCallback(
    async (values: any) => {
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }

      try {
        setLoading(true);
        const apiClient = GetApiClient();
        await apiClient.api.wiki.document.embedding_document.post({
          wikiId: parseInt(wikiId),
          documentId: parseInt(documentId),
          tokenizer: values.tokenizer,
          maxTokensPerParagraph: values.maxTokensPerParagraph,
          overlappingTokens: values.overlappingTokens,
        });

        messageApi.success("向量化任务已提交");
        onSuccess();
      } catch (error) {
        console.error("Failed to submit embedding task:", error);
        messageApi.error("提交向量化任务失败");
      } finally {
        setLoading(false);
      }
    },
    [wikiId, documentId, messageApi, onSuccess]
  );

  const clearVectors = useCallback(async () => {
    if (!wikiId || !documentId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setClearLoading(true);
      const apiClient = GetApiClient();
      await apiClient.api.wiki.document.clear_document.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
      });

      messageApi.success("向量已清空");
      onSuccess();
    } catch (error) {
      console.error("Failed to clear vectors:", error);
      messageApi.error("清空向量失败");
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
};

// 任务状态渲染组件
const TaskStatusTag: React.FC<{ state: string }> = ({ state }) => {
  const getStatusColor = (state: string) => {
    const lowerState = state.toLowerCase();
    switch (lowerState) {
      case "completed":
        return "success";
      case "processing":
        return "processing";
      case "failed":
        return "error";
      case "wait":
        return "warning";
      default:
        return "default";
    }
  };

  return <Tag color={getStatusColor(state)}>{state}</Tag>;
};

// 主组件
export default function DocumentEmbedding() {
  const { id: wikiId, documentId } = useParams();
  const [form] = Form.useForm();
  const [partitionForm] = Form.useForm();
  const [aiPartitionForm] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();
  const [activePartitionTab, setActivePartitionTab] = useState<string>("normal");
  const [chunkEditModalVisible, setChunkEditModalVisible] = useState(false);
  const [editingChunkId, setEditingChunkId] = useState<string | null>(null);
  const [editingChunkText, setEditingChunkText] = useState<string>("");
  const [editingChunkDerivatives, setEditingChunkDerivatives] = useState<WikiDocumentDerivativeItem[] | null | undefined>(null);
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);
  const [dragOverIndex, setDragOverIndex] = useState<number | null>(null);
  const [expandedDerivatives, setExpandedDerivatives] = useState<Set<string>>(new Set());
  const [batchGenerateVisible, setBatchGenerateVisible] = useState(false);
  const [selectedChunkIds, setSelectedChunkIds] = useState<Set<string>>(new Set());
  const [batchAiModelId, setBatchAiModelId] = useState<number | null>(null);
  const [batchPreprocessStrategyType, setBatchPreprocessStrategyType] = useState<PreprocessStrategyType | null>(null);
  const [batchGenerating, setBatchGenerating] = useState(false);
  const [batchGenerateResults, setBatchGenerateResults] = useState<Map<string, WikiDocumentDerivativeItem[]>>(new Map());

  // 使用自定义Hooks
  const {
    documentInfo,
    loading: docLoading,
    contextHolder: docContextHolder,
    fetchDocumentInfo,
  } = useDocumentInfo(wikiId || "", documentId || "");
  const {
    tasks,
    loading: tasksLoading,
    contextHolder: tasksContextHolder,
    fetchTasks,
    cancelTask,
  } = useTaskList(wikiId || "", documentId || "");
  const {
    previewData,
    loading: previewLoading,
    contextHolder: previewContextHolder,
    fetchPartitionPreview,
    updateItem: updatePreviewItem,
    deleteItem: deletePreviewItem,
    reorderItems: reorderPreviewItems,
    addChunk: addPreviewChunk,
    reorderAllChunks: reorderAllPreviewChunks,
  } = usePartitionPreview(wikiId || "", documentId || "");
  const {
    loading: partitionLoading,
    contextHolder: partitionContextHolder,
    submitPartition,
  } = usePartitionOperations(wikiId || "", documentId || "", async () => {
    fetchDocumentInfo();
    await fetchPartitionPreview();
    // 切割后重新排序 - 等待数据加载完成
    setTimeout(() => {
      reorderAllPreviewChunks();
    }, 500);
  });
  const {
    loading: embedLoading,
    clearLoading,
    contextHolder: embedContextHolder,
    submitEmbedding,
    clearVectors,
  } = useEmbeddingOperations(wikiId || "", documentId || "", () => {
    fetchTasks();
    fetchDocumentInfo();
  });
  const {
    modelList,
    loading: modelListLoading,
    contextHolder: modelListContextHolder,
    fetchModelList,
  } = useAiModelList();
  const {
    loading: aiPartitionLoading,
    contextHolder: aiPartitionContextHolder,
    submitAiPartition,
  } = useAiPartitionOperations(wikiId || "", documentId || "", async () => {
    fetchDocumentInfo();
    await fetchPartitionPreview();
    // AI 切割后重新排序 - 等待数据加载完成
    setTimeout(() => {
      reorderAllPreviewChunks();
    }, 500);
  });

  useEffect(() => {
    if (wikiId && documentId) {
      fetchDocumentInfo();
      fetchTasks();
      fetchPartitionPreview();
      fetchModelList();
    }
  }, [wikiId, documentId, fetchDocumentInfo, fetchTasks, fetchPartitionPreview, fetchModelList]);

  // 当获取到文档信息后，如果有 partionConfig，则更新表单默认值
  useEffect(() => {
    if (documentInfo?.partionConfig) {
      const config = documentInfo.partionConfig;
      partitionForm.setFieldsValue({
        maxTokensPerChunk: config.maxTokensPerChunk ?? 1000,
        overlap: config.overlap ?? 100,
        chunkHeader: config.chunkHeader ?? "",
      });
    }
  }, [documentInfo, partitionForm]);

  const handleSubmit = useCallback(
    async (values: any) => {
      await submitEmbedding(values);
    },
    [submitEmbedding]
  );

  const handleCancelTask = useCallback(
    async (taskId: string) => {
      await cancelTask(taskId);
    },
    [cancelTask]
  );

  const handlePartitionSubmit = useCallback(
    async (values: any) => {
      await submitPartition(values);
    },
    [submitPartition]
  );

  const handleAiPartitionSubmit = useCallback(
    async (values: any) => {
      await submitAiPartition(values);
    },
    [submitAiPartition]
  );

  const handleOpenChunkEditor = useCallback(
    (chunkId: string | null, currentText: string, currentDerivatives?: WikiDocumentDerivativeItem[] | null) => {
      setEditingChunkId(chunkId);
      setEditingChunkText(currentText || "");
      setEditingChunkDerivatives(currentDerivatives || null);
      setChunkEditModalVisible(true);
    },
    []
  );

  const handleCloseChunkEditor = useCallback(() => {
    setChunkEditModalVisible(false);
    setEditingChunkId(null);
    setEditingChunkText("");
    setEditingChunkDerivatives(null);
  }, []);

  const handleAddChunk = useCallback(() => {
    handleOpenChunkEditor(null, "", null);
  }, [handleOpenChunkEditor]);

  const handleSaveChunk = useCallback(
    async (text: string, derivatives: WikiDocumentDerivativeItem[] | null) => {
      try {
        if (editingChunkId) {
          // 编辑现有chunk
          await updatePreviewItem(editingChunkId, text, derivatives);
        } else {
          // 新增chunk
          await addPreviewChunk(text, derivatives);
        }
        handleCloseChunkEditor();
      } catch (error) {
        console.error("Failed to save chunk:", error);
        message.error("保存失败，请重试");
      }
    },
    [editingChunkId, updatePreviewItem, addPreviewChunk, handleCloseChunkEditor]
  );

  const handleDeleteItem = useCallback(
    async (chunkId: string) => {
      if (!previewData) return;
      
      try {
        // deleteItem 会自动更新内存并重新排序
        await deletePreviewItem(chunkId);
      } catch (error) {
        console.error("Failed to delete item:", error);
        // 错误已经在 deleteItem 中处理，这里不需要额外处理
      }
    },
    [previewData, deletePreviewItem]
  );

  // 批量生成策略
  const handleBatchGenerate = useCallback(async () => {
    if (!batchAiModelId) {
      messageApi.warning("请选择AI模型");
      return;
    }
    if (!batchPreprocessStrategyType) {
      messageApi.warning("请选择优化策略");
      return;
    }
    if (selectedChunkIds.size === 0) {
      messageApi.warning("请至少选择一个文本块");
      return;
    }
    if (!previewData) {
      messageApi.warning("没有可用的文本块");
      return;
    }

    try {
      setBatchGenerating(true);
      const apiClient = GetApiClient();
      
      // 构建 chunks 参数，只包含选中的块
      const selectedItems = previewData.items.filter(item => selectedChunkIds.has(item.chunkId));
      const chunks: KeyValueOfInt64AndString[] = selectedItems.map(item => ({
        key: item.chunkId,
        value: item.text,
      }));

      const response = await apiClient.api.wiki.document.ai_generation_chunk.post({
        wikiId: parseInt(wikiId || ""),
        aiModelId: batchAiModelId,
        chunks: chunks,
        preprocessStrategyType: batchPreprocessStrategyType,
      });

      if (response?.items && response.items.length > 0) {
        // 处理响应，按 chunkId 匹配结果
        const resultsMap = new Map<string, WikiDocumentDerivativeItem[]>();
        
        response.items.forEach((item) => {
          if (item.key && item.value) {
            const chunkId = item.key;
            const result = item.value;
            const derivatives: WikiDocumentDerivativeItem[] = [];
            
            // 处理 metadata（包含关键词、摘要、问题列表等）
            if (result.metadata && result.metadata.length > 0) {
              result.metadata.forEach((meta) => {
                if (meta.key && meta.value) {
                  // 根据 key 确定衍生类型
                  let derivativeType: DerivativeType | null = null;
                  const keyLower = meta.key.toLowerCase();
                  if (keyLower.includes("outline") || keyLower.includes("大纲")) {
                    derivativeType = "outline";
                  } else if (keyLower.includes("question") || keyLower.includes("问题")) {
                    derivativeType = "question";
                  } else if (keyLower.includes("keyword") || keyLower.includes("关键词")) {
                    derivativeType = "keyword";
                  } else if (keyLower.includes("summary") || keyLower.includes("摘要")) {
                    derivativeType = "summary";
                  } else {
                    // 默认根据策略类型确定
                    switch (batchPreprocessStrategyType) {
                      case "outlineGeneration":
                        derivativeType = "outline";
                        break;
                      case "questionGeneration":
                        derivativeType = "question";
                        break;
                      case "keywordSummaryFusion":
                        derivativeType = "keyword";
                        break;
                      case "semanticAggregation":
                        derivativeType = "aggregatedSubParagraph";
                        break;
                    }
                  }
                  
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
              let derivativeType: DerivativeType | null = null;
              switch (batchPreprocessStrategyType) {
                case "outlineGeneration":
                  derivativeType = "outline";
                  break;
                case "questionGeneration":
                  derivativeType = "question";
                  break;
                case "keywordSummaryFusion":
                  derivativeType = "keyword";
                  break;
                case "semanticAggregation":
                  derivativeType = "aggregatedSubParagraph";
                  break;
              }
              
              const content = result.processedText || result.originalText || "";
              if (content && derivativeType) {
                derivatives.push({
                  derivativeType: derivativeType,
                  derivativeContent: content,
                });
              }
            }
            
            if (derivatives.length > 0) {
              resultsMap.set(chunkId, derivatives);
            }
          }
        });
        
        setBatchGenerateResults(resultsMap);
        messageApi.success(`成功生成 ${resultsMap.size} 个文本块的衍生内容`);
      } else {
        messageApi.warning("AI生成未返回结果");
      }
    } catch (error) {
      console.error("Failed to batch generate derivatives:", error);
      proxyRequestError(error, messageApi);
    } finally {
      setBatchGenerating(false);
    }
  }, [batchAiModelId, batchPreprocessStrategyType, selectedChunkIds, previewData, wikiId, messageApi]);

  // 保存批量生成结果
  const handleSaveBatchResults = useCallback(async () => {
    if (batchGenerateResults.size === 0) {
      messageApi.warning("没有可保存的结果");
      return;
    }

    try {
      const apiClient = GetApiClient();
      
      // 将所有衍生内容转换为 AddWikiDocumentDerivativeItem[] 格式
      const derivativesToAdd: AddWikiDocumentDerivativeItem[] = [];
      
      batchGenerateResults.forEach((derivatives, chunkId) => {
        if (derivatives && derivatives.length > 0) {
          derivatives.forEach((derivative) => {
            derivativesToAdd.push({
              chunkId: chunkId,
              derivativeContent: derivative.derivativeContent || null,
              derivativeType: derivative.derivativeType || null,
            });
          });
        }
      });

      if (derivativesToAdd.length === 0) {
        messageApi.warning("没有可保存的衍生内容");
        return;
      }

      // 调用批量添加衍生内容 API
      if (!wikiId || !documentId) {
        messageApi.error("缺少必要的参数");
        return;
      }

      await apiClient.api.wiki.document.add_chunk_derivatives.post({
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        aiModelId: batchAiModelId || null,
        derivatives: derivativesToAdd,
      });

      messageApi.success(`成功保存 ${derivativesToAdd.length} 条衍生内容`);
      
      // 重新获取数据以刷新显示
      await fetchPartitionPreview();
      
      // 清空结果并关闭模态窗口
      setBatchGenerateResults(new Map());
      setBatchGenerateVisible(false);
      setSelectedChunkIds(new Set());
      setBatchAiModelId(null);
      setBatchPreprocessStrategyType(null);
    } catch (error) {
      console.error("Failed to save batch results:", error);
      proxyRequestError(error, messageApi);
    }
  }, [batchGenerateResults, wikiId, documentId, batchAiModelId, messageApi, fetchPartitionPreview]);

  const handleDragStart = useCallback((e: React.DragEvent, index: number) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", index.toString());
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent, index: number) => {
    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = "move";
    if (draggedIndex !== null && draggedIndex !== index) {
      setDragOverIndex(index);
    }
  }, [draggedIndex]);

  const handleDragLeave = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragOverIndex(null);
  }, []);

  const handleDragEnd = useCallback(() => {
    setDraggedIndex(null);
    setDragOverIndex(null);
  }, []);

  const handleDrop = useCallback(
    async (e: React.DragEvent, dropIndex: number) => {
      e.preventDefault();
      e.stopPropagation();
      if (draggedIndex === null) return;
      
      const dragIndex = draggedIndex;
      if (dragIndex !== dropIndex) {
        try {
          await reorderPreviewItems(dragIndex, dropIndex);
        } catch (error) {
          console.error("Failed to reorder items:", error);
        }
      }
      setDraggedIndex(null);
      setDragOverIndex(null);
    },
    [draggedIndex, reorderPreviewItems]
  );

  const canCancelTask = useCallback((state: string) => {
    const lowerState = state.toLowerCase();
    return (
      lowerState === "none" ||
      lowerState === "wait" ||
      lowerState === "processing"
    );
  }, []);

  const taskColumns = [
    {
      title: "任务ID",
      dataIndex: "id",
      key: "id",
      width: 220,
      ellipsis: true,
    },
    {
      title: "文件名",
      dataIndex: "fileName",
      key: "fileName",
      width: 200,
      ellipsis: true,
    },
    {
      title: "分词器",
      dataIndex: "tokenizer",
      key: "tokenizer",
      width: 120,
    },
    {
      title: "每段最大Token数",
      dataIndex: "maxTokensPerParagraph",
      key: "maxTokensPerParagraph",
      width: 150,
    },
    {
      title: "重叠Token数",
      dataIndex: "overlappingTokens",
      key: "overlappingTokens",
      width: 120,
    },
    {
      title: "状态",
      dataIndex: "state",
      key: "state",
      width: 120,
      render: (state: string) => <TaskStatusTag state={state} />,
    },
    {
      title: "执行信息",
      dataIndex: "message",
      key: "message",
      width: 200,
      ellipsis: true,
    },
    {
      title: "创建时间",
      dataIndex: "createTime",
      key: "createTime",
      width: 180,
      render: (text: string) => formatDateTime(text),
    },
    {
      title: "操作",
      key: "action",
      width: 120,
      fixed: "right" as const,
      render: (_: any, record: TaskInfo) => (
        <Space>
          {canCancelTask(record.state) && (
            <Button
              type="link"
              danger
              size="small"
              onClick={() => handleCancelTask(record.id)}
            >
              取消
            </Button>
          )}
        </Space>
      ),
    },
  ];

  if (!documentId) {
    return (
      <Card>
        <Empty description="缺少文档ID" />
      </Card>
    );
  }

  return (
    <>
      {contextHolder}
      {docContextHolder}
      {tasksContextHolder}
      {embedContextHolder}
      {partitionContextHolder}
      {previewContextHolder}
      {modelListContextHolder}
      {aiPartitionContextHolder}

      {/* 文档切割卡片 */}
      <Collapse
        defaultActiveKey={["partition"]}
        style={{ marginBottom: 16 }}
      >
        <Panel
          header={
            <Space>
              <ScissorOutlined />
              <span>文档切割</span>
            </Space>
          }
          key="partition"
        >
          <Tabs
            activeKey={activePartitionTab}
            onChange={setActivePartitionTab}
            items={[
              {
                key: "normal",
                label: "普通切割",
                children: (
                  <Form
                    form={partitionForm}
                    layout="vertical"
                    onFinish={handlePartitionSubmit}
                    initialValues={{
                      maxTokensPerChunk: 1000,
                      overlap: 100,
                      chunkHeader: "",
                    }}
                  >
                    {documentInfo && (
                      <>
                        {/* 文档信息统计 */}
                        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
                          <Col span={8}>
                            <Statistic
                              title="文档名称"
                              value={documentInfo.fileName}
                              valueStyle={{ fontSize: "16px" }}
                            />
                          </Col>
                          <Col span={8}>
                            <Statistic
                              title="文件大小"
                              value={documentInfo.fileSize}
                              suffix="bytes"
                              valueStyle={{ fontSize: "16px" }}
                            />
                          </Col>
                        </Row>

                        {/* 切割配置表单 */}
                        <Row gutter={[16, 16]}>
                          <Col span={12}>
                            <div>
                              <div
                                style={{
                                  fontSize: "14px",
                                  fontWeight: 500,
                                  marginBottom: "8px",
                                }}
                              >
                                每段最大Token数
                              </div>
                              <div
                                style={{
                                  fontSize: "12px",
                                  marginBottom: "8px",
                                  color: "#8c8c8c",
                                }}
                              >
                                当对文档进行分段时，每个分段通常包含一个段落。此参数控制每个段落的最大token数量。
                              </div>
                              <Form.Item
                                name="maxTokensPerChunk"
                                rules={[
                                  {
                                    required: true,
                                    message: "请输入每段最大Token数",
                                  },
                                ]}
                              >
                                <InputNumber
                                  min={1}
                                  max={100000}
                                  style={{ width: "100%" }}
                                />
                              </Form.Item>
                            </div>
                          </Col>
                          <Col span={12}>
                            <div>
                              <div
                                style={{
                                  fontSize: "14px",
                                  fontWeight: 500,
                                  marginBottom: "8px",
                                }}
                              >
                                重叠Token数
                              </div>
                              <div
                                style={{
                                  fontSize: "12px",
                                  marginBottom: "8px",
                                  color: "#8c8c8c",
                                }}
                              >
                                分段之间的重叠token数量，用于保持上下文的连贯性。
                              </div>
                              <Form.Item
                                name="overlap"
                                rules={[
                                  { required: true, message: "请输入重叠Token数" },
                                ]}
                              >
                                <InputNumber
                                  min={0}
                                  max={1000}
                                  style={{ width: "100%" }}
                                />
                              </Form.Item>
                            </div>
                          </Col>
                        </Row>

                        <Row gutter={[16, 16]}>
                          <Col span={24}>
                            <div>
                              <div
                                style={{
                                  fontSize: "14px",
                                  fontWeight: 500,
                                  marginBottom: "8px",
                                }}
                              >
                                分块标题（可选）
                              </div>
                              <div
                                style={{
                                  fontSize: "12px",
                                  marginBottom: "8px",
                                  color: "#8c8c8c",
                                }}
                              >
                                可选，在每个分块前添加的标题。
                              </div>
                              <Form.Item name="chunkHeader">
                                <Input placeholder="请输入分块标题（可选）" />
                              </Form.Item>
                            </div>
                          </Col>
                        </Row>

                        <Form.Item>
                          <Button
                            type="primary"
                            htmlType="submit"
                            loading={partitionLoading}
                            icon={<ScissorOutlined />}
                          >
                            开始切割
                          </Button>
                        </Form.Item>

                        {/* 提示信息 */}
                        <Alert
                          message="普通切割说明"
                          description="普通切割适合对称检索，按照固定的token数量和重叠数量对文档进行分段。"
                          type="info"
                          showIcon
                          style={{ marginTop: 16 }}
                        />
                      </>
                    )}
                  </Form>
                ),
              },
              {
                key: "smart",
                label: "智能切割",
                children: (
                  <Form
                    form={aiPartitionForm}
                    layout="vertical"
                    onFinish={handleAiPartitionSubmit}
                    initialValues={{
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
                    }}
                  >
                    {documentInfo && (
                      <>
                        {/* 文档信息统计 */}
                        <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
                          <Col span={8}>
                            <Statistic
                              title="文档名称"
                              value={documentInfo.fileName}
                              valueStyle={{ fontSize: "16px" }}
                            />
                          </Col>
                          <Col span={8}>
                            <Statistic
                              title="文件大小"
                              value={documentInfo.fileSize}
                              suffix="bytes"
                              valueStyle={{ fontSize: "16px" }}
                            />
                          </Col>
                        </Row>

                        {/* 智能切割配置表单 */}
                        <Row gutter={[16, 16]}>
                          <Col span={8}>
                            <div>
                              <div
                                style={{
                                  fontSize: "14px",
                                  fontWeight: 500,
                                  marginBottom: "8px",
                                }}
                              >
                                AI模型
                              </div>
                              <div
                                style={{
                                  fontSize: "12px",
                                  marginBottom: "8px",
                                  color: "#8c8c8c",
                                  minHeight: "36px",
                                }}
                              >
                                选择用于智能切割的AI模型。
                              </div>
                              <Form.Item
                                name="aiModelId"
                                rules={[
                                  { required: true, message: "请选择AI模型" },
                                ]}
                              >
                                <Select
                                  placeholder="请选择AI模型"
                                  loading={modelListLoading}
                                  options={modelList.map((model) => ({
                                    label: model.name,
                                    value: model.id,
                                  }))}
                                />
                              </Form.Item>
                            </div>
                          </Col>
                        </Row>

                        <Row gutter={[16, 16]}>
                          <Col span={24}>
                            <div>
                              <div
                                style={{
                                  fontSize: "14px",
                                  fontWeight: 500,
                                  marginBottom: "8px",
                                }}
                              >
                                提示词模板
                              </div>
                              <div
                                style={{
                                  fontSize: "12px",
                                  marginBottom: "8px",
                                  color: "#8c8c8c",
                                }}
                              >
                                用于指导AI进行智能切割的提示词模板，请务必提示模型输出 JSON 格式的数据。
                              </div>
                              <Form.Item name="promptTemplate">
                                <Input.TextArea
                                  rows={4}
                                  placeholder="请输入提示词模板"
                                />
                              </Form.Item>
                            </div>
                          </Col>
                        </Row>

                        <Form.Item>
                          <Button
                            type="primary"
                            htmlType="submit"
                            loading={aiPartitionLoading}
                            icon={<ScissorOutlined />}
                          >
                            智能切割
                          </Button>
                        </Form.Item>

                        {/* 提示信息 */}
                        <Alert
                          message="智能切割说明"
                          description="智能切割使用AI模型来理解文档内容，按照语义和上下文进行更智能的分段，适合复杂文档的切割需求。"
                          type="info"
                          showIcon
                          style={{ marginTop: 16 }}
                        />
                      </>
                    )}
                  </Form>
                ),
              },
            ]}
          />
        </Panel>
      </Collapse>

      {/* 切割预览卡片 */}
      <Collapse defaultActiveKey={["preview"]} style={{ marginTop: 16 }}>
        <Panel
          header={
            <Space>
              <FileTextOutlined />
              <span>切割预览</span>
              <Button
                type="text"
                icon={<ReloadOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  fetchPartitionPreview();
                }}
                loading={previewLoading}
                size="small"
              />
              <Button
                type="text"
                icon={<PlusOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  handleAddChunk();
                }}
                size="small"
              >
                新增块
              </Button>
              <Button
                type="text"
                icon={<ThunderboltOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  setBatchGenerateVisible(true);
                  // 初始化选中所有块
                  if (previewData && previewData.items.length > 0) {
                    setSelectedChunkIds(new Set(previewData.items.map(item => item.chunkId)));
                  }
                }}
                size="small"
              >
                批量生成策略
              </Button>
            </Space>
          }
          key="preview"
        >
          {previewLoading ? (
            <div style={{ textAlign: "center", padding: "50px 0" }}>
              <Empty description="加载中..." />
            </div>
          ) : previewData && previewData.items.length > 0 ? (
            <div
              style={{
                display: "grid",
                gridTemplateColumns: "repeat(auto-fill, minmax(450px, 1fr))",
                gap: "16px",
              }}
            >
              {previewData.items.map((item, index) => {
                const isDragging = draggedIndex === index;
                const isDragOver = dragOverIndex === index;
                const displayText = item.text || "(空)";

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
                    title={
                      <Space style={{ width: "100%", justifyContent: "space-between" }}>
                        <Space>
                          <DragOutlined
                            style={{
                              cursor: "move",
                              color: "#8c8c8c",
                            }}
                          />
                          <Tag color="blue">#{item.order + 1}</Tag>
                          <Tag color="default" title={item.chunkId || "未设置"}>
                            ID: {item.chunkId 
                              ? (item.chunkId.length > 20 ? `${item.chunkId.substring(0, 20)}...` : item.chunkId)
                              : "N/A"}
                          </Tag>
                        </Space>
                        <Space>
                          <Button
                            type="text"
                            size="small"
                            icon={<EditOutlined />}
                            onClick={(e) => {
                              e.stopPropagation();
                              handleOpenChunkEditor(item.chunkId, item.text, item.derivatives);
                            }}
                          >
                            编辑
                          </Button>
                          <Popconfirm
                            title="确认删除"
                            description="确定要删除这个文本块吗？"
                            onConfirm={(e) => {
                              e?.stopPropagation();
                              handleDeleteItem(item.chunkId);
                            }}
                            onCancel={(e) => {
                              e?.stopPropagation();
                            }}
                            okText="确认"
                            cancelText="取消"
                          >
                            <Button
                              type="text"
                              size="small"
                              danger
                              icon={<DeleteOutlined />}
                              onClick={(e) => {
                                e.stopPropagation();
                              }}
                            >
                              删除
                            </Button>
                          </Popconfirm>
                        </Space>
                      </Space>
                    }
                    style={{
                      marginBottom: 0,
                      cursor: isDragging ? "grabbing" : "grab",
                      opacity: isDragging ? 0.5 : 1,
                      border: isDragOver ? "2px dashed #1890ff" : "1px solid #d9d9d9",
                      backgroundColor: isDragOver ? "#e6f7ff" : "white",
                      transition: "all 0.2s",
                    }}
                    bodyStyle={{ padding: "16px" }}
                  >
                    {/* 文本内容 */}
                    <div style={{ marginBottom: item.derivatives && item.derivatives.length > 0 ? 16 : 0 }}>
                      <Typography.Text
                        type="secondary"
                        style={{ fontSize: 12, marginBottom: 8, display: "block" }}
                      >
                        文本内容
                      </Typography.Text>
                      <Typography.Paragraph
                        style={{
                          fontSize: "14px",
                          lineHeight: "1.6",
                          whiteSpace: "pre-wrap",
                          wordBreak: "break-word",
                          marginBottom: 0,
                          maxHeight: "200px",
                          overflow: "auto",
                          padding: "8px",
                          backgroundColor: "#fafafa",
                          borderRadius: "4px",
                        }}
                        ellipsis={{ rows: 8, expandable: false }}
                      >
                        {displayText}
                      </Typography.Paragraph>
                    </div>

                    {/* 衍生内容 */}
                    {(() => {
                      const hasDerivatives = item.derivatives && Array.isArray(item.derivatives) && item.derivatives.length > 0;
                      if (!hasDerivatives) {
                        console.log("No derivatives for item:", item.chunkId, "derivatives:", item.derivatives);
                      }
                      return hasDerivatives;
                    })() && item.derivatives && (
                      <div style={{ marginTop: 16, paddingTop: 16, borderTop: "1px solid #f0f0f0" }}>
                        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
                          <Typography.Text
                            type="secondary"
                            style={{ fontSize: 12 }}
                          >
                            衍生内容 ({item.derivatives.length})
                          </Typography.Text>
                          <Button
                            type="text"
                            size="small"
                            icon={expandedDerivatives.has(item.chunkId) ? <UpOutlined /> : <DownOutlined />}
                            onClick={(e) => {
                              e.stopPropagation();
                              const newExpanded = new Set(expandedDerivatives);
                              if (newExpanded.has(item.chunkId)) {
                                newExpanded.delete(item.chunkId);
                              } else {
                                newExpanded.add(item.chunkId);
                              }
                              setExpandedDerivatives(newExpanded);
                            }}
                            style={{ padding: 0, height: "auto" }}
                          >
                            {expandedDerivatives.has(item.chunkId) ? "收起" : "展开"}
                          </Button>
                        </div>
                        {expandedDerivatives.has(item.chunkId) && (
                          <Space direction="vertical" size="small" style={{ width: "100%" }}>
                            {item.derivatives.map((derivative: any, idx: number) => {
                            // 尝试多种可能的字段名
                            const type = derivative?.derivativeType 
                              ?? derivative?.DerivativeType 
                              ?? derivative?.type 
                              ?? null;
                            const content = derivative?.derivativeContent 
                              ?? derivative?.DerivativeContent 
                              ?? derivative?.content 
                              ?? "";
                            
                            // 调试：打印实际数据
                            if (idx === 0) {
                              console.log("Displaying derivative:", {
                                raw: derivative,
                                type: type,
                                content: content,
                                keys: derivative ? Object.keys(derivative) : [],
                                hasDerivativeType: 'derivativeType' in (derivative || {}),
                                hasDerivativeContent: 'derivativeContent' in (derivative || {}),
                              });
                            }
                            
                            return (
                              <div
                                key={idx}
                                style={{
                                  padding: "8px 12px",
                                  backgroundColor: "#f5f5f5",
                                  borderRadius: "4px",
                                  border: "1px solid #e8e8e8",
                                }}
                              >
                                <Tag
                                  color="purple"
                                  style={{ marginBottom: 6, marginRight: 0 }}
                                >
                                  {(() => {
                                    if (!type) {
                                      console.log("Type is null/undefined for derivative:", derivative);
                                      return "未知";
                                    }
                                    // 尝试直接匹配
                                    if (DerivativeTypeMap[type]) {
                                      return DerivativeTypeMap[type];
                                    }
                                    // 尝试转换为字符串后匹配
                                    const typeStr = String(type);
                                    if (DerivativeTypeMap[typeStr]) {
                                      return DerivativeTypeMap[typeStr];
                                    }
                                    // 尝试小写匹配
                                    const typeLower = typeStr.toLowerCase();
                                    if (DerivativeTypeMap[typeLower]) {
                                      return DerivativeTypeMap[typeLower];
                                    }
                                    // 返回原始值
                                    console.log("Type not found in map:", type, "available keys:", Object.keys(DerivativeTypeMap));
                                    return typeStr;
                                  })()}
                                </Tag>
                                <Typography.Text
                                  style={{
                                    fontSize: "13px",
                                    color: "#666",
                                    display: "block",
                                    whiteSpace: "pre-wrap",
                                    wordBreak: "break-word",
                                  }}
                                >
                                  {content || "(空)"}
                                </Typography.Text>
                              </div>
                            );
                          })}
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

      {/* 文档向量化卡片 */}
      <Collapse defaultActiveKey={["embedding"]}>
        <Panel
          header={
            <Space>
              <FileTextOutlined />
              <span>文档向量化</span>
            </Space>
          }
          key="embedding"
        >
          {docLoading ? (
            <div style={{ textAlign: "center", padding: "50px 0" }}>
              <Empty description="加载中..." />
            </div>
          ) : (
            documentInfo && (
          <>
            {/* 文档信息统计 */}
            <Row gutter={[16, 16]} style={{ marginBottom: 24 }}>
              <Col span={8}>
                <Statistic
                  title="文档名称"
                  value={documentInfo.fileName}
                  valueStyle={{ fontSize: "16px" }}
                />
              </Col>
              <Col span={8}>
                <Statistic
                  title="文件大小"
                  value={documentInfo.fileSize}
                  suffix="bytes"
                  valueStyle={{ fontSize: "16px" }}
                />
              </Col>
              <Col span={8}>
                <Statistic
                  title="向量化状态"
                  value={documentInfo.embedding ? "已向量化" : "未向量化"}
                  valueStyle={{
                    fontSize: "16px",
                    color: documentInfo.embedding ? "#52c41a" : "#faad14",
                  }}
                />
              </Col>
            </Row>

            {/* 向量化配置表单 */}
            <Form
              form={form}
              layout="vertical"
              onFinish={handleSubmit}
              initialValues={{
                tokenizer: "cl100k",
                maxTokensPerParagraph: 1000,
                overlappingTokens: 100,
              }}
            >
              <Row gutter={[16, 16]}>
                <Col span={12}>
                  <div>
                    <div
                      style={{
                        fontSize: "14px",
                        fontWeight: 500,
                        marginBottom: "8px",
                      }}
                    >
                      分词器
                    </div>
                    <div
                      style={{
                        fontSize: "12px",
                        marginBottom: "8px",
                        color: "#8c8c8c",
                      }}
                    >
                      本地检测文档token 数量的算法。
                    </div>
                    <Form.Item
                      name="tokenizer"
                      rules={[{ required: true, message: "请选择分词器" }]}
                    >
                      <Select
                        placeholder="请选择分词器"
                        options={[
                          { label: "p50k", value: "p50k" },
                          { label: "cl100k", value: "cl100k" },
                          { label: "o200k", value: "o200k" },
                        ]}
                      />
                    </Form.Item>
                  </div>
                </Col>
                <Col span={12}>
                  <div>
                    <div
                      style={{
                        fontSize: "14px",
                        fontWeight: 500,
                        marginBottom: "8px",
                      }}
                    >
                      每段最大Token数
                    </div>
                    <div
                      style={{
                        fontSize: "12px",
                        marginBottom: "8px",
                        color: "#8c8c8c",
                      }}
                    >
                      当对文档进行分段时，每个分段通常包含一个段落。此参数控制每个段落的最大token数量。
                    </div>
                    <Form.Item
                      name="maxTokensPerParagraph"
                      rules={[
                        { required: true, message: "请输入每段最大Token数" },
                      ]}
                    >
                      <InputNumber
                        min={1}
                        max={100000}
                        style={{ width: "100%" }}
                      />
                    </Form.Item>
                  </div>
                </Col>
              </Row>

              <Row gutter={[16, 16]}>
                <Col span={12}>
                  <div>
                    <div
                      style={{
                        fontSize: "14px",
                        fontWeight: 500,
                        marginBottom: "8px",
                      }}
                    >
                      重叠Token数
                    </div>
                    <div
                      style={{
                        fontSize: "12px",
                        marginBottom: "8px",
                        color: "#8c8c8c",
                      }}
                    >
                      分段之间的重叠token数量，用于保持上下文的连贯性。
                    </div>
                    <Form.Item
                      name="overlappingTokens"
                      rules={[{ required: true, message: "请输入重叠Token数" }]}
                    >
                      <InputNumber
                        min={0}
                        max={1000}
                        style={{ width: "100%" }}
                      />
                    </Form.Item>
                  </div>
                </Col>
              </Row>

              <Form.Item>
                <Space>
                  <Button
                    type="primary"
                    htmlType="submit"
                    loading={embedLoading}
                    icon={<FileTextOutlined />}
                  >
                    开始向量化
                  </Button>
                  <Tooltip title="清空该文档的所有向量">
                    <Button
                      type="default"
                      onClick={clearVectors}
                      loading={clearLoading}
                      danger
                    >
                      清空向量
                    </Button>
                  </Tooltip>
                </Space>
              </Form.Item>
            </Form>

            {/* 提示信息 */}
            <Alert
              message="向量化说明"
              description="文档向量化是将文档内容转换为向量表示的过程，用于后续的语义搜索和相似度计算。"
              type="info"
              showIcon
              style={{ marginTop: 16 }}
            />
            </>
          )
          )}
        </Panel>
      </Collapse>

      {/* 任务列表 */}
      <Card
        title={
          <Space>
            <ClockCircleOutlined />
            <span>任务列表</span>
            <Button
              type="text"
              icon={<ReloadOutlined />}
              onClick={fetchTasks}
              loading={tasksLoading}
            />
          </Space>
        }
        style={{ marginTop: 16 }}
      >
        <Table
          columns={taskColumns}
          dataSource={tasks}
          rowKey="id"
          loading={tasksLoading}
          scroll={{ x: 1200 }}
          pagination={false}
          locale={{
            emptyText: <Empty description="暂无任务" />,
          }}
        />
      </Card>

      {/* Chunk 编辑模态窗口 */}
      <ChunkEditModal
        open={chunkEditModalVisible}
        chunkId={editingChunkId}
        initialText={editingChunkText}
        initialDerivatives={editingChunkDerivatives}
        wikiId={wikiId || ""}
        documentId={documentId || ""}
        modelList={modelList}
        onClose={handleCloseChunkEditor}
        onSave={handleSaveChunk}
      />

      {/* 批量生成策略模态窗口 */}
      <Modal
        title="批量生成策略"
        open={batchGenerateVisible}
        onCancel={() => {
          setBatchGenerateVisible(false);
          setBatchGenerateResults(new Map());
          setSelectedChunkIds(new Set());
          setBatchAiModelId(null);
          setBatchPreprocessStrategyType(null);
        }}
        width={800}
        footer={[
          <Button
            key="cancel"
            onClick={() => {
              setBatchGenerateVisible(false);
              setBatchGenerateResults(new Map());
              setSelectedChunkIds(new Set());
              setBatchAiModelId(null);
              setBatchPreprocessStrategyType(null);
            }}
          >
            取消
          </Button>,
          batchGenerateResults.size > 0 && (
            <Button
              key="save"
              type="primary"
              onClick={handleSaveBatchResults}
            >
              保存结果
            </Button>
          ),
          <Button
            key="generate"
            type="primary"
            loading={batchGenerating}
            onClick={handleBatchGenerate}
            disabled={!batchAiModelId || !batchPreprocessStrategyType || selectedChunkIds.size === 0}
          >
            生成
          </Button>,
        ].filter(Boolean)}
        destroyOnClose
      >
        <Space direction="vertical" style={{ width: "100%" }} size="large">
          {/* AI 模型选择 */}
          <div>
            <div style={{ marginBottom: 8, fontWeight: 500 }}>AI 模型</div>
            <Select
              placeholder="请选择AI模型"
              value={batchAiModelId}
              onChange={setBatchAiModelId}
              style={{ width: "100%" }}
              options={modelList.map((model) => ({
                label: model.name,
                value: model.id,
              }))}
            />
          </div>

          {/* 优化策略选择 */}
          <div>
            <div style={{ marginBottom: 8, fontWeight: 500 }}>优化策略</div>
            <Select
              placeholder="请选择优化策略"
              value={batchPreprocessStrategyType}
              onChange={setBatchPreprocessStrategyType}
              style={{ width: "100%" }}
              options={PreprocessStrategyOptions}
            />
          </div>

          {/* 块列表选择 */}
          <div>
            <div style={{ marginBottom: 8, fontWeight: 500 }}>
              选择文本块 ({selectedChunkIds.size} / {previewData?.items.length || 0})
            </div>
            <div style={{ marginBottom: 8 }}>
              <Space>
                <Button
                  type="link"
                  size="small"
                  onClick={() => {
                    if (previewData) {
                      setSelectedChunkIds(new Set(previewData.items.map(item => item.chunkId)));
                    }
                  }}
                >
                  全选
                </Button>
                <Button
                  type="link"
                  size="small"
                  onClick={() => {
                    setSelectedChunkIds(new Set());
                  }}
                >
                  全不选
                </Button>
              </Space>
            </div>
            <div
              style={{
                maxHeight: "300px",
                overflow: "auto",
                border: "1px solid #d9d9d9",
                borderRadius: "4px",
                padding: "8px",
              }}
            >
              <Space direction="vertical" style={{ width: "100%" }} size="small">
                {previewData?.items.map((item) => (
                  <div key={item.chunkId} style={{ display: "flex", alignItems: "flex-start" }}>
                    <Checkbox
                      checked={selectedChunkIds.has(item.chunkId)}
                      onChange={(e) => {
                        const newSelected = new Set(selectedChunkIds);
                        if (e.target.checked) {
                          newSelected.add(item.chunkId);
                        } else {
                          newSelected.delete(item.chunkId);
                        }
                        setSelectedChunkIds(newSelected);
                      }}
                    >
                      <div style={{ marginLeft: 8, flex: 1 }}>
                        <div style={{ fontWeight: 500, marginBottom: 4 }}>
                          #{item.order + 1} - {item.chunkId.substring(0, 20)}...
                        </div>
                        <Typography.Text
                          type="secondary"
                          style={{ fontSize: 12 }}
                          ellipsis={{ tooltip: item.text || "(空)" }}
                        >
                          {item.text || "(空)"}
                        </Typography.Text>
                      </div>
                    </Checkbox>
                  </div>
                ))}
              </Space>
            </div>
          </div>

          {/* 生成结果预览 */}
          {batchGenerateResults.size > 0 && (
            <div>
              <div style={{ marginBottom: 8, fontWeight: 500 }}>
                生成结果 ({batchGenerateResults.size} 个文本块)
              </div>
              <div
                style={{
                  maxHeight: "300px",
                  overflow: "auto",
                  border: "1px solid #d9d9d9",
                  borderRadius: "4px",
                  padding: "8px",
                  backgroundColor: "#fafafa",
                }}
              >
                <Space direction="vertical" style={{ width: "100%" }} size="small">
                  {Array.from(batchGenerateResults.entries()).map(([chunkId, derivatives]) => {
                    const item = previewData?.items.find(i => i.chunkId === chunkId);
                    return (
                      <div
                        key={chunkId}
                        style={{
                          padding: "8px",
                          backgroundColor: "white",
                          borderRadius: "4px",
                          border: "1px solid #e8e8e8",
                          position: "relative",
                        }}
                      >
                        <div style={{ fontWeight: 500, marginBottom: 8 }}>
                          #{item?.order !== undefined ? item.order + 1 : "?"} - {chunkId.substring(0, 20)}...
                        </div>
                        <Space direction="vertical" size="small" style={{ width: "100%" }}>
                          {derivatives.map((derivative, idx) => (
                            <div
                              key={idx}
                              style={{
                                padding: "4px 8px",
                                backgroundColor: "#f5f5f5",
                                borderRadius: "4px",
                                display: "flex",
                                justifyContent: "space-between",
                                alignItems: "flex-start",
                              }}
                            >
                              <div style={{ flex: 1, minWidth: 0 }}>
                                <Tag color="purple" style={{ marginRight: 8 }}>
                                  {DerivativeTypeMap[derivative.derivativeType || ""] || derivative.derivativeType || "未知"}
                                </Tag>
                                <Typography.Text
                                  style={{ fontSize: 12 }}
                                  ellipsis={{ tooltip: derivative.derivativeContent || "(空)" }}
                                >
                                  {derivative.derivativeContent || "(空)"}
                                </Typography.Text>
                              </div>
                              <Button
                                type="text"
                                danger
                                size="small"
                                icon={<DeleteOutlined />}
                                onClick={() => {
                                  const newResults = new Map(batchGenerateResults);
                                  const currentDerivatives = newResults.get(chunkId) || [];
                                  const updatedDerivatives = currentDerivatives.filter((_, index) => index !== idx);
                                  
                                  if (updatedDerivatives.length > 0) {
                                    newResults.set(chunkId, updatedDerivatives);
                                  } else {
                                    // 如果删除后没有衍生内容了，也删除该文本块的条目
                                    newResults.delete(chunkId);
                                  }
                                  
                                  setBatchGenerateResults(newResults);
                                }}
                                style={{ marginLeft: 8, flexShrink: 0 }}
                              />
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
}
