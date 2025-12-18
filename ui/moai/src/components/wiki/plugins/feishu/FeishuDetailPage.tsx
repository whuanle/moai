import React, { useState, useEffect, useRef, useCallback } from "react";
import { useNavigate, useParams } from "react-router";
import {
  Card,
  Button,
  Table,
  Tag,
  Space,
  message,
  Spin,
  Typography,
  Switch,
  Collapse,
  Modal,
  Form,
  Input,
} from "antd";
import {
  PlayCircleOutlined,
  StopOutlined,
  ReloadOutlined,
  ArrowLeftOutlined,
  EditOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import {
  QueryWikiFeishuPageTasksCommandResponse,
  WikiFeishuPageItem,
  StartWikiFeishuPluginTaskCommand,
  WorkerState,
  WorkerStateObject,
  QueryWikiFeishuConfigCommandResponse,
  WikiFeishuConfig,
  UpdateWikiFeishuConfigCommand,
  WikiPluginAutoProcessConfig,
} from "../../../../apiClient/models";
import {
  proxyRequestError,
  proxyFormRequestError,
} from "../../../../helper/RequestError";
import StartTaskConfigModal from "../common/StartTaskConfigModal";

const { Title, Text } = Typography;
const { Panel } = Collapse;

export default function FeishuDetailPage() {
  const navigate = useNavigate();
  const { id, configId } = useParams<{ id: string; configId: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  const feishuConfigId = configId ? parseInt(configId) : undefined;

  const [loading, setLoading] = useState(false);
  const [pages, setPages] = useState<WikiFeishuPageItem[]>([]);
  const [isWorking, setIsWorking] = useState(false);
  const [workState, setWorkState] = useState<WorkerState | null>(null);
  const [workMessage, setWorkMessage] = useState<string>("");
  const [messageApi, contextHolder] = message.useMessage();
  const [starting, setStarting] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true); // 自动刷新开关，默认开启
  const refreshTimerRef = useRef<NodeJS.Timeout | null>(null);

  // 配置相关状态
  const [config, setConfig] = useState<WikiFeishuConfig | null>(null);
  const [configTitle, setConfigTitle] = useState<string>("");
  const [configLoading, setConfigLoading] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [startConfigModalVisible, setStartConfigModalVisible] = useState(false);

  // 获取配置信息
  const fetchConfig = useCallback(async () => {
    if (!wikiId || !feishuConfigId) {
      return;
    }

    setConfigLoading(true);
    try {
      const client = GetApiClient();
      const response: QueryWikiFeishuConfigCommandResponse | undefined =
        await client.api.wiki.plugin.feishu.config.get({
          queryParameters: {
            configId: feishuConfigId,
            wikiId: wikiId,
          },
        });

      if (response) {
        setConfig(response.config || null);
        setConfigTitle(response.title || "");
        setWorkState(response.workState || null);
        setWorkMessage(response.workMessage || "");
        // 根据 workState 判断是否正在工作
        // 在 Wait 或 Processing 状态下才认为是正在工作（用于自动刷新）
        setIsWorking(
          response.workState === WorkerStateObject.Wait ||
          response.workState === WorkerStateObject.Processing
        );
      } else {
        setConfig(null);
        setConfigTitle("");
        setWorkState(null);
        setWorkMessage("");
        setIsWorking(false);
      }
    } catch (error) {
      console.error("获取配置信息失败:", error);
      proxyRequestError(error, messageApi, "获取配置信息失败");
    } finally {
      setConfigLoading(false);
    }
  }, [wikiId, feishuConfigId, messageApi]);

  // 获取飞书状态和任务列表
  const fetchPageState = useCallback(async () => {
    if (!wikiId || !feishuConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setLoading(true);
    // 每次请求前先清空列表
    setPages([]);
    try {
      const client = GetApiClient();
      const response: QueryWikiFeishuPageTasksCommandResponse | undefined =
        await client.api.wiki.plugin.feishu.query_page_state.get({
          queryParameters: {
            configId: feishuConfigId,
            wikiId: wikiId,
          },
        });

      if (response) {
        setPages(response.items || []);
      } else {
        setPages([]);
      }

      // 请求后重新对齐状态，如果状态已经不是 Wait 或 Processing，自动刷新会停止
      await fetchConfig();
    } catch (error) {
      console.error("获取飞书状态失败:", error);
      proxyRequestError(error, messageApi, "获取飞书状态失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, feishuConfigId, messageApi, fetchConfig]);

  // 打开启动配置模态窗口
  const handleOpenStartConfig = () => {
    setStartConfigModalVisible(true);
  };

  // 启动飞书同步
  const handleStart = async (
    isAutoProcess: boolean,
    autoProcessConfig: WikiPluginAutoProcessConfig | null
  ) => {
    if (!wikiId || !feishuConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setStarting(true);
    try {
      const client = GetApiClient();
      const requestBody: StartWikiFeishuPluginTaskCommand = {
        configId: feishuConfigId,
        wikiId: wikiId,
        isStart: true, // true 表示启动任务
        isAutoProcess: isAutoProcess || undefined,
        autoProcessConfig: autoProcessConfig || undefined,
      };

      await client.api.wiki.plugin.feishu.lanuch_task.post(requestBody);
      messageApi.success("飞书同步已启动");
      // 延迟一下再刷新状态，给服务端一些时间
      setTimeout(() => {
        fetchConfig();
        fetchPageState();
      }, 1000);
    } catch (error) {
      console.error("启动飞书同步失败:", error);
      proxyRequestError(error, messageApi, "启动飞书同步失败");
    } finally {
      setStarting(false);
    }
  };

  // 停止飞书同步
  const handleStop = async () => {
    if (!wikiId || !feishuConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setStopping(true);
    try {
      const client = GetApiClient();
      const requestBody: StartWikiFeishuPluginTaskCommand = {
        configId: feishuConfigId,
        wikiId: wikiId,
        isStart: false, // false 表示停止任务
      };

      await client.api.wiki.plugin.feishu.lanuch_task.post(requestBody);
      messageApi.success("飞书同步已停止");
      // 延迟一下再刷新状态
      setTimeout(() => {
        fetchConfig();
        fetchPageState();
      }, 1000);
    } catch (error) {
      console.error("停止飞书同步失败:", error);
      proxyRequestError(error, messageApi, "停止飞书同步失败");
    } finally {
      setStopping(false);
    }
  };

  // 处理编辑配置
  const handleEdit = () => {
    if (!config) {
      messageApi.error("配置信息不存在");
      return;
    }

    form.resetFields();
    form.setFieldsValue({
      title: configTitle,
      appId: config.appId || "",
      appSecret: config.appSecret || "",
      spaceId: config.spaceId || "",
      parentNodeToken: config.parentNodeToken || "",
      isOverExistPage: config.isOverExistPage || false,
    });
    setEditModalVisible(true);
  };

  // 处理保存配置
  const handleSaveConfig = async () => {
    try {
      await form.validateFields();
      const values = form.getFieldsValue();

      setSaving(true);
      const client = GetApiClient();

      const updateBody: UpdateWikiFeishuConfigCommand = {
        configId: feishuConfigId,
        wikiId: wikiId || 0,
        title: values.title,
        appId: values.appId,
        appSecret: values.appSecret,
        spaceId: values.spaceId,
        parentNodeToken: values.parentNodeToken,
        isOverExistPage: values.isOverExistPage || false,
      };

      await client.api.wiki.plugin.feishu.update_config.post(updateBody);
      messageApi.success("配置已更新");

      setEditModalVisible(false);
      fetchConfig();
    } catch (error) {
      console.error("保存配置失败:", error);
      proxyFormRequestError(error, messageApi, form, "保存配置失败");
    } finally {
      setSaving(false);
    }
  };

  // 页面初始化
  useEffect(() => {
    if (wikiId && feishuConfigId) {
      fetchConfig();
      fetchPageState();
    }
  }, [wikiId, feishuConfigId, fetchConfig, fetchPageState]);

  // 自动刷新逻辑：当 workState 是 Wait 或 Processing 且 autoRefresh 为 true 时，每秒刷新页面列表
  useEffect(() => {
    // 清除之前的定时器
    if (refreshTimerRef.current) {
      clearInterval(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }

    // 只有 workState 是 Wait 或 Processing 且 autoRefresh 为 true 时，才启动自动刷新
    const shouldAutoRefresh =
      autoRefresh &&
      wikiId &&
      feishuConfigId &&
      (workState === WorkerStateObject.Wait ||
        workState === WorkerStateObject.Processing);

    if (shouldAutoRefresh) {
      refreshTimerRef.current = setInterval(() => {
        fetchPageState(); // 只刷新页面列表，不刷新配置
      }, 1000); // 每秒刷新
    }

    // 清理函数：组件卸载或依赖变化时清除定时器
    return () => {
      if (refreshTimerRef.current) {
        clearInterval(refreshTimerRef.current);
        refreshTimerRef.current = null;
      }
    };
  }, [workState, autoRefresh, fetchPageState, wikiId, feishuConfigId]);

  // 任务状态表格列定义
  const pageColumns = [
    {
      title: "NodeToken",
      dataIndex: "nodeToken",
      key: "nodeToken",
      width: 200,
      ellipsis: true,
      render: (text: string) => text || "-",
    },
    {
      title: "ObjToken",
      dataIndex: "objToken",
      key: "objToken",
      width: 200,
      ellipsis: true,
      render: (text: string) => text || "-",
    },
    {
      title: "状态",
      dataIndex: "state",
      key: "state",
      width: 120,
      render: (state: WorkerState) => {
        const stateMap: Record<string, { color: string; text: string }> = {
          [WorkerStateObject.None]: { color: "default", text: "未开始" },
          [WorkerStateObject.Wait]: { color: "default", text: "等待中" },
          [WorkerStateObject.Processing]: {
            color: "processing",
            text: "处理中",
          },
          [WorkerStateObject.Successful]: { color: "success", text: "成功" },
          [WorkerStateObject.Failed]: { color: "error", text: "失败" },
          [WorkerStateObject.Cancal]: { color: "default", text: "已取消" },
        };
        const stateInfo = stateMap[state || ""] || {
          color: "default",
          text: "未知",
        };
        return <Tag color={stateInfo.color}>{stateInfo.text}</Tag>;
      },
    },
    {
      title: "文件名",
      dataIndex: "fileName",
      key: "fileName",
      ellipsis: true,
      render: (text: string) => text || "-",
    },
    {
      title: "文件大小",
      dataIndex: "fileSize",
      key: "fileSize",
      width: 120,
      render: (size: number) => {
        if (!size) return "-";
        if (size < 1024) return `${size} B`;
        if (size < 1024 * 1024) return `${(size / 1024).toFixed(2)} KB`;
        return `${(size / (1024 * 1024)).toFixed(2)} MB`;
      },
    },
    {
      title: "是否向量化",
      dataIndex: "isEmbedding",
      key: "isEmbedding",
      width: 100,
      render: (isEmbedding: boolean) => (
        <Tag color={isEmbedding ? "success" : "default"}>
          {isEmbedding ? "是" : "否"}
        </Tag>
      ),
    },
    {
      title: "消息",
      dataIndex: "message",
      key: "message",
      ellipsis: true,
      render: (text: string) => text || "-",
    },
    {
      title: "更新时间",
      dataIndex: "updateTime",
      key: "updateTime",
      width: 180,
      render: (time: string) => (time ? new Date(time).toLocaleString() : "-"),
    },
  ];

  if (!wikiId || !feishuConfigId) {
    return (
      <Card>
        <Text type="secondary">缺少必要参数</Text>
      </Card>
    );
  }

  // 获取任务状态显示
  const getTaskStateDisplay = () => {
    if (!workState) return { color: "default", text: "未启动" };
    const stateMap: Record<string, { color: string; text: string }> = {
      [WorkerStateObject.None]: { color: "default", text: "未开始" },
      [WorkerStateObject.Wait]: { color: "default", text: "等待中" },
      [WorkerStateObject.Processing]: { color: "processing", text: "处理中" },
      [WorkerStateObject.Successful]: { color: "success", text: "成功" },
      [WorkerStateObject.Failed]: { color: "error", text: "失败" },
      [WorkerStateObject.Cancal]: { color: "default", text: "已取消" },
    };
    return stateMap[workState] || { color: "default", text: "未知" };
  };

  const taskStateDisplay = getTaskStateDisplay();

  return (
    <div>
      {contextHolder}

      {/* 头部操作区域 */}
      <Card>
        <Space
          style={{
            width: "100%",
            justifyContent: "space-between",
            marginBottom: 16,
          }}
        >
          <Button
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/feishu`)}
          >
            返回列表
          </Button>
          <Space>
            <Button
              type="primary"
              icon={<PlayCircleOutlined />}
              onClick={handleOpenStartConfig}
              loading={starting}
              disabled={isWorking}
            >
              启动
            </Button>
            <Button
              danger
              icon={<StopOutlined />}
              onClick={handleStop}
              loading={stopping}
              disabled={!isWorking}
            >
              停止
            </Button>
            <Button
              icon={<ReloadOutlined />}
              onClick={fetchPageState}
              loading={loading}
            >
              刷新
            </Button>
          </Space>
        </Space>

        {/* 任务状态信息 */}
        {workState && (
          <Card size="small" style={{ marginTop: 16 }}>
            <Space direction="vertical" style={{ width: "100%" }} size="small">
              <div>
                <Text strong>任务状态：</Text>
                <Tag color={taskStateDisplay.color} style={{ marginLeft: 8 }}>
                  {taskStateDisplay.text}
                </Tag>
              </div>
              {workMessage && (
                <div>
                  <Text strong>运行信息：</Text>
                  <Text>{workMessage}</Text>
                </div>
              )}
              <div>
                <Text strong>成功数量：</Text>
                <Text type="success">
                  {
                    pages.filter(
                      (page) => page.state === WorkerStateObject.Successful
                    ).length
                  }
                </Text>
                <Text strong style={{ marginLeft: 10 }}>失败数量：</Text>
                <Text type="danger">
                  {
                    pages.filter(
                      (page) => page.state === WorkerStateObject.Failed
                    ).length
                  }
                </Text>
                <Text strong style={{ marginLeft: 10 }}>同步中：</Text>
                <Text type="danger">
                  {
                    pages.filter(
                      (page) => page.state === WorkerStateObject.Wait || page.state === WorkerStateObject.Processing
                    ).length
                  }
                </Text>
              </div>
            </Space>
          </Card>
        )}
      </Card>

      {/* 配置信息显示块 */}
      <Card style={{ marginTop: 16 }}>
        <Collapse defaultActiveKey={[]}>
          <Panel
            header={
              <Space>
                <Text strong>配置信息</Text>
                {configTitle && <Text type="secondary">({configTitle})</Text>}
              </Space>
            }
            key="config"
            extra={
              <Button
                type="link"
                size="small"
                icon={<EditOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  handleEdit();
                }}
                disabled={isWorking}
                title={isWorking ? "同步运行中，无法编辑配置" : "编辑配置"}
              >
                编辑
              </Button>
            }
          >
            <Spin spinning={configLoading}>
              {config ? (
                <Space
                  direction="vertical"
                  style={{ width: "100%" }}
                  size="small"
                >
                  <div>
                    <Text strong>标题：</Text>
                    <Text>{configTitle || "-"}</Text>
                  </div>
                  <div>
                    <Text strong>飞书应用ID：</Text>
                    <Text>{config.appId || "-"}</Text>
                  </div>
                  <div>
                    <Text strong>飞书知识库 space_id：</Text>
                    <Text>{config.spaceId || "-"}</Text>
                  </div>
                  {config.parentNodeToken && (
                    <div>
                      <Text strong>目录 token：</Text>
                      <Text>{config.parentNodeToken}</Text>
                    </div>
                  )}
                  <div>
                    <Text strong>是否覆盖已存在的页面：</Text>
                    <Tag color={config.isOverExistPage ? "success" : "default"}>
                      {config.isOverExistPage ? "是" : "否"}
                    </Tag>
                  </div>
                </Space>
              ) : (
                <Text type="secondary">暂无配置信息</Text>
              )}
            </Spin>
          </Panel>
        </Collapse>
      </Card>

      {/* 任务列表 */}
      <Card style={{ marginTop: 16 }}>
        <Space
          style={{
            width: "100%",
            justifyContent: "space-between",
            marginBottom: 16,
          }}
        >
          <Title level={5} style={{ margin: 0 }}>
            页面列表
          </Title>
          {(workState === WorkerStateObject.Wait ||
            workState === WorkerStateObject.Processing) && (
            <Space>
              <Text type="secondary">自动刷新：</Text>
              <Switch
                checked={autoRefresh}
                onChange={setAutoRefresh}
                checkedChildren="开启"
                unCheckedChildren="关闭"
              />
            </Space>
          )}
        </Space>
        <Spin spinning={loading}>
          <Table
            columns={pageColumns}
            dataSource={pages}
            rowKey="pageId"
            pagination={false}
          />
        </Spin>
      </Card>

      {/* 编辑配置 Modal */}
      <Modal
        title="编辑配置"
        open={editModalVisible}
        onCancel={() => setEditModalVisible(false)}
        onOk={handleSaveConfig}
        confirmLoading={saving}
        width={800}
        destroyOnClose
      >
        <Form form={form} layout="vertical">
          <Form.Item
            name="title"
            label="标题"
            rules={[{ required: true, message: "请输入标题" }]}
          >
            <Input placeholder="请输入配置标题" />
          </Form.Item>

          <Form.Item
            name="appId"
            label="飞书应用ID"
            rules={[{ required: true, message: "请输入飞书应用ID" }]}
          >
            <Input placeholder="请输入飞书应用ID" />
          </Form.Item>

          <Form.Item
            name="appSecret"
            label="飞书应用密钥"
            rules={[{ required: true, message: "请输入飞书应用密钥" }]}
          >
            <Input.Password placeholder="请输入飞书应用密钥" />
          </Form.Item>

          <Form.Item
            name="spaceId"
            label="飞书知识库ID"
            rules={[{ required: true, message: "请输入飞书知识库ID" }]}
          >
            <Input placeholder="请输入飞书知识库ID" />
          </Form.Item>

          <Form.Item
            name="parentNodeToken"
            label="顶部文档Token"
            tooltip="可选：指定从哪个文档节点开始同步，如果不填则同步整个知识库"
          >
            <Input placeholder="可选：顶部文档Token" />
          </Form.Item>

          <Form.Item
            name="isOverExistPage"
            label="是否覆盖已存在的页面"
            tooltip="如果开启，将覆盖已经同步过的页面"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Form>
      </Modal>

      {/* 启动配置模态窗口 */}
      <StartTaskConfigModal
        open={startConfigModalVisible}
        onCancel={() => setStartConfigModalVisible(false)}
        onConfirm={handleStart}
        wikiId={wikiId || 0}
      />
    </div>
  );
}

