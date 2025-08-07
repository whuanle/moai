import { useState, useEffect, useCallback } from "react";
import { useParams, useNavigate } from "react-router";
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
} from "antd";

import { GetApiClient } from "../ServiceClient";
import {
  ReloadOutlined,
  FileTextOutlined,
  ClockCircleOutlined,
} from "@ant-design/icons";
import { formatDateTime } from "../../helper/DateTimeHelper";

// 类型定义
interface DocumentInfo {
  documentId: number;
  fileName: string;
  fileSize: number;
  contentType: string;
  createTime: string;
  createUserName: string;
  embedding: boolean;
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
          embedding: response.embedding || false,
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
  const navigate = useNavigate();
  const [form] = Form.useForm();

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
    }
  }, [wikiId, documentId, fetchDocumentInfo, fetchTasks]);

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

      <Card
        title={
          <Space>
            <FileTextOutlined />
            <span>文档向量化</span>
          </Space>
        }
        loading={docLoading}
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
        )}
      </Card>

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
    </>
  );
}
