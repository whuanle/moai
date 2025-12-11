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
} from "@ant-design/icons";
import { formatDateTime } from "../../helper/DateTimeHelper";
import CodeEditorModal from "../common/CodeEditorModal";

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
}

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
      const response = await apiClient.api.wiki.document.get_partition_document.post({
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
            chunkId: item.chunkId || "",
            order: item.order || 0,
            text: item.text || "",
          })),
          chunkHeader: response.chunkHeader,
          maxTokensPerChunk: response.maxTokensPerChunk,
          overlap: response.overlap,
        });
      }
    } catch (error) {
      console.error("Failed to fetch partition preview:", error);
      messageApi.error("获取切割预览失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, documentId, messageApi]);

  // 先定义 updatePartitionDocument，因为其他函数会依赖它
  const updatePartitionDocument = useCallback(async (items?: PartitionPreviewItem[]) => {
    if (!wikiId || !documentId) {
      console.error("Missing wikiId or documentId:", { wikiId, documentId });
      return;
    }

    // 如果传入了 items，使用传入的 items，否则使用 previewData 中的 items
    const itemsToUpdate = items !== undefined ? items : previewData?.items;
    // 允许空数组，因为删除所有块时 items 可能为空
    if (itemsToUpdate === undefined) {
      console.error("No items to update:", { items, previewData });
      return;
    }

    try {
      console.log("Updating partition document:", {
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        itemsCount: itemsToUpdate.length,
      });
      
      const apiClient = GetApiClient();
      const requestBody = {
        wikiId: parseInt(wikiId),
        documentId: parseInt(documentId),
        items: itemsToUpdate.map((item) => ({
          order: item.order,
          text: item.text,
        })),
      };
      
      console.log("Request body:", requestBody);
      
      await apiClient.api.wiki.document.update_text_partition_document.post(requestBody);
      messageApi.success("切割文档已更新");
      console.log("Update successful");
    } catch (error) {
      console.error("Failed to update partition document:", error);
      messageApi.error("更新切割文档失败");
      throw error; // 重新抛出错误，让调用者知道保存失败
    }
  }, [wikiId, documentId, previewData, messageApi]);

  const updateItem = useCallback(
    async (chunkId: string, newText: string) => {
      // 直接从 ref 获取最新的 previewData，避免闭包问题
      const currentData = previewDataRef.current;
      if (!currentData) {
        console.error("Cannot update item: previewData is null");
        return;
      }
      
      // 计算更新后的 items
      const updatedItems = currentData.items.map((item) =>
        item.chunkId === chunkId ? { ...item, text: newText } : item
      );
      
      // 更新状态
      setPreviewData({
        ...currentData,
        items: updatedItems,
      });
      
      // 更新内存后，自动调用 updatePartitionDocument
      try {
        await updatePartitionDocument(updatedItems);
      } catch (error) {
        // 如果更新失败，重新获取数据以恢复状态
        await fetchPartitionPreview();
        throw error;
      }
    },
    [updatePartitionDocument, fetchPartitionPreview]
  );

  const deleteItem = useCallback(async (order: number) => {
    // 直接从 ref 获取最新的 previewData，避免闭包问题
    const currentData = previewDataRef.current;
    if (!currentData) {
      console.error("Cannot delete item: previewData is null");
      return;
    }
    
    // 计算更新后的 items，按 order 过滤
    const filtered = currentData.items.filter((item) => item.order !== order);
    // 更新 order：重新排序，确保 order 从 1 开始连续
    const updatedItems = filtered.map((item, index) => ({
      ...item,
      order: index + 1,
    }));
    
    console.log("Deleting item by order:", order, "original count:", currentData.items.length, "updated count:", updatedItems.length, "updatedItems:", updatedItems);
    
    // 更新状态
    setPreviewData({
      ...currentData,
      items: updatedItems,
    });
    
    // 更新内存后，自动调用 updatePartitionDocument
    try {
      await updatePartitionDocument(updatedItems);
    } catch (error) {
      // 如果更新失败，重新获取数据以恢复状态
      await fetchPartitionPreview();
      throw error;
    }
  }, [updatePartitionDocument, fetchPartitionPreview]);

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
    // 更新 order
    const updatedItems = newItems.map((item, index) => ({
      ...item,
      order: index + 1,
    }));
    
    // 更新状态
    setPreviewData({
      ...currentData,
      items: updatedItems,
    });
    
    // 更新内存后，自动调用 updatePartitionDocument
    try {
      await updatePartitionDocument(updatedItems);
    } catch (error) {
      // 如果更新失败，重新获取数据以恢复状态
      await fetchPartitionPreview();
      throw error;
    }
  }, [updatePartitionDocument, fetchPartitionPreview]);

  return {
    previewData,
    loading,
    contextHolder,
    fetchPartitionPreview,
    updateItem,
    deleteItem,
    reorderItems,
    updatePartitionDocument,
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
  const [activePartitionTab, setActivePartitionTab] = useState<string>("normal");
  const [codeEditorVisible, setCodeEditorVisible] = useState(false);
  const [codeEditorInitialValue, setCodeEditorInitialValue] = useState<string>("");
  const [editingChunkId, setEditingChunkId] = useState<string | null>(null);
  const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

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
    updatePartitionDocument,
  } = usePartitionPreview(wikiId || "", documentId || "");
  const {
    loading: partitionLoading,
    contextHolder: partitionContextHolder,
    submitPartition,
  } = usePartitionOperations(wikiId || "", documentId || "", async () => {
    fetchDocumentInfo();
    await fetchPartitionPreview();
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

  useEffect(() => {
    if (wikiId && documentId) {
      fetchDocumentInfo();
      fetchTasks();
      fetchPartitionPreview();
    }
  }, [wikiId, documentId, fetchDocumentInfo, fetchTasks, fetchPartitionPreview]);

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

  const handleOpenCodeEditor = useCallback(
    (chunkId: string, currentText: string) => {
      setEditingChunkId(chunkId);
      setCodeEditorInitialValue(currentText || "");
      setCodeEditorVisible(true);
    },
    []
  );

  const handleCloseCodeEditor = useCallback(() => {
    setCodeEditorVisible(false);
    setEditingChunkId(null);
    setCodeEditorInitialValue("");
  }, []);

  const handleConfirmCodeEditor = useCallback(
    async (value: string) => {
      if (!editingChunkId || !previewData) {
        console.error("Missing editingChunkId or previewData");
        return;
      }

      try {
        // updateItem 会自动更新内存并调用 updatePartitionDocument
        await updatePreviewItem(editingChunkId, value);
        setCodeEditorVisible(false);
        setEditingChunkId(null);
        setCodeEditorInitialValue("");
      } catch (error) {
        console.error("Failed to update partition document:", error);
        // 不关闭编辑器，让用户知道保存失败
        message.error("保存失败，请重试");
      }
    },
    [editingChunkId, previewData, updatePreviewItem]
  );

  const handleDeleteItem = useCallback(
    async (order: number) => {
      if (!previewData) return;
      
      try {
        // deleteItem 会自动更新内存并调用 updatePartitionDocument
        await deletePreviewItem(order);
      } catch (error) {
        console.error("Failed to delete item:", error);
        // 错误已经在 deleteItem 中处理，这里不需要额外处理
      }
    },
    [previewData, deletePreviewItem]
  );

  const handleDragStart = useCallback((e: React.DragEvent, index: number) => {
    setDraggedIndex(index);
    e.dataTransfer.effectAllowed = "move";
    e.dataTransfer.setData("text/plain", index.toString());
    const target = e.currentTarget as HTMLElement;
    target.style.opacity = "0.5";
  }, []);

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    e.dataTransfer.dropEffect = "move";
  }, []);

  const handleDragEnd = useCallback((e: React.DragEvent) => {
    const target = e.currentTarget as HTMLElement;
    target.style.opacity = "1";
    setDraggedIndex(null);
  }, []);

  const handleDrop = useCallback(
    async (e: React.DragEvent, dropIndex: number) => {
      e.preventDefault();
      e.stopPropagation();
      if (draggedIndex === null) return;
      
      const dragIndex = draggedIndex;
      if (dragIndex !== dropIndex) {
        try {
          // reorderItems 会自动更新内存并调用 updatePartitionDocument
          await reorderPreviewItems(dragIndex, dropIndex);
        } catch (error) {
          console.error("Failed to reorder items:", error);
          // 错误已经在 reorderItems 中处理，这里不需要额外处理
        }
      }
      const target = e.currentTarget as HTMLElement;
      target.style.opacity = "1";
      setDraggedIndex(null);
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
      {docContextHolder}
      {tasksContextHolder}
      {embedContextHolder}
      {partitionContextHolder}
      {previewContextHolder}

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
                  <div>
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

                        <Alert
                          message="智能切割功能"
                          description="智能切割功能正在开发中，敬请期待。"
                          type="info"
                          showIcon
                          style={{ marginTop: 16 }}
                        />
                      </>
                    )}
                  </div>
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
                display: "flex",
                flexWrap: "wrap",
                gap: "12px",
              }}
            >
              {previewData.items.map((item, index) => {
                // 只显示前500字
                const displayText =
                  item.text && item.text.length > 500
                    ? item.text.substring(0, 500) + "..."
                    : item.text || "";

                return (
                  <Card
                    key={item.chunkId}
                    size="small"
                    draggable
                    onDragStart={(e) => handleDragStart(e, index)}
                    onDragOver={handleDragOver}
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
                          <Tag color="blue">#{item.order}</Tag>
                          <span style={{ fontSize: "12px" }}>标题: {item.chunkId}</span>
                        </Space>
                        <Space>
                          <Button
                            type="text"
                            size="small"
                            icon={<EditOutlined />}
                            onClick={(e) => {
                              e.stopPropagation();
                              handleOpenCodeEditor(item.chunkId, item.text);
                            }}
                          />
                          <Popconfirm
                            title="确认删除"
                            description="确定要删除这个文本块吗？"
                            onConfirm={(e) => {
                              e?.stopPropagation();
                              handleDeleteItem(item.order);
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
                            />
                          </Popconfirm>
                        </Space>
                      </Space>
                    }
                    style={{
                      width: "400px",
                      marginBottom: 0,
                      cursor: "pointer",
                    }}
                    bodyStyle={{ padding: "12px" }}
                    onClick={() => handleOpenCodeEditor(item.chunkId, item.text)}
                  >
                    {previewData.chunkHeader && (
                      <div
                        style={{
                          fontSize: "12px",
                          fontWeight: 500,
                          marginBottom: "6px",
                          color: "#1890ff",
                        }}
                      >
                        {previewData.chunkHeader}
                      </div>
                    )}
                    <Typography.Paragraph
                      style={{
                        marginBottom: 0,
                        fontSize: "13px",
                        lineHeight: "1.6",
                        whiteSpace: "pre-wrap",
                        wordBreak: "break-word",
                        maxHeight: "150px",
                        overflow: "hidden",
                      }}
                      ellipsis={{ rows: 5, expandable: false }}
                    >
                      {displayText}
                    </Typography.Paragraph>
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

      {/* 代码编辑器模态窗口 */}
      <CodeEditorModal
        open={codeEditorVisible}
        initialValue={codeEditorInitialValue}
        language="plaintext"
        title="编辑文本块"
        onClose={handleCloseCodeEditor}
        onConfirm={handleConfirmCodeEditor}
        width={1200}
        height="70vh"
      />
    </>
  );
}
