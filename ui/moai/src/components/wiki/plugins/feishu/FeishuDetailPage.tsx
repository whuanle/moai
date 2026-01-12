import { useState, useEffect, useRef, useCallback, useMemo } from "react";
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
  Modal,
  Form,
  Input,
  Row,
  Col,
  Descriptions,
} from "antd";
import {
  PlayCircleOutlined,
  StopOutlined,
  ReloadOutlined,
  ArrowLeftOutlined,
  EditOutlined,
  SettingOutlined,
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
import { FileSizeHelper } from "../../../../helper/FileSizeHelper";
import StartTaskConfigModal from "../common/StartTaskConfigModal";
import "../../../../styles/theme.css";
import "./FeishuDetailPage.css";

const { Title, Text } = Typography;

// 状态映射配置
const STATE_MAP: Record<string, { color: string; text: string }> = {
  [WorkerStateObject.None]: { color: "default", text: "未开始" },
  [WorkerStateObject.Wait]: { color: "gold", text: "等待中" },
  [WorkerStateObject.Processing]: { color: "processing", text: "处理中" },
  [WorkerStateObject.Successful]: { color: "success", text: "成功" },
  [WorkerStateObject.Failed]: { color: "error", text: "失败" },
  [WorkerStateObject.Cancal]: { color: "default", text: "已取消" },
};

// 获取状态显示信息
const getStateInfo = (state: WorkerState | null | undefined) => {
  if (!state) return { color: "default", text: "未启动" };
  return STATE_MAP[state] || { color: "default", text: "未知" };
};


export default function FeishuDetailPage() {
  const navigate = useNavigate();
  const { id, configId } = useParams<{ id: string; configId: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  const feishuConfigId = configId ? parseInt(configId) : undefined;

  const [loading, setLoading] = useState(false);
  const [pages, setPages] = useState<WikiFeishuPageItem[]>([]);
  const [workState, setWorkState] = useState<WorkerState | null>(null);
  const [workMessage, setWorkMessage] = useState<string>("");
  const [messageApi, contextHolder] = message.useMessage();
  const [starting, setStarting] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const refreshTimerRef = useRef<NodeJS.Timeout | null>(null);

  // 配置相关状态
  const [config, setConfig] = useState<WikiFeishuConfig | null>(null);
  const [configTitle, setConfigTitle] = useState<string>("");
  const [configLoading, setConfigLoading] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [startConfigModalVisible, setStartConfigModalVisible] = useState(false);

  // 计算是否正在工作
  const isWorking = useMemo(
    () =>
      workState === WorkerStateObject.Wait ||
      workState === WorkerStateObject.Processing,
    [workState]
  );

  // 统计数据
  const stats = useMemo(() => {
    const successCount = pages.filter(
      (p) => p.state === WorkerStateObject.Successful
    ).length;
    const failedCount = pages.filter(
      (p) => p.state === WorkerStateObject.Failed
    ).length;
    const processingCount = pages.filter(
      (p) =>
        p.state === WorkerStateObject.Wait ||
        p.state === WorkerStateObject.Processing
    ).length;
    return { successCount, failedCount, processingCount, total: pages.length };
  }, [pages]);

  // 获取配置信息
  const fetchConfig = useCallback(async () => {
    if (!wikiId || !feishuConfigId) return;

    setConfigLoading(true);
    try {
      const client = GetApiClient();
      const response: QueryWikiFeishuConfigCommandResponse | undefined =
        await client.api.wiki.plugin.feishu.config.get({
          queryParameters: { configId: feishuConfigId, wikiId },
        });

      if (response) {
        setConfig(response.config || null);
        setConfigTitle(response.title || "");
        setWorkState(response.workState || null);
        setWorkMessage(response.workMessage || "");
      } else {
        setConfig(null);
        setConfigTitle("");
        setWorkState(null);
        setWorkMessage("");
      }
    } catch (error) {
      console.error("获取配置信息失败:", error);
      proxyRequestError(error, messageApi, "获取配置信息失败");
    } finally {
      setConfigLoading(false);
    }
  }, [wikiId, feishuConfigId, messageApi]);

  // 获取页面任务列表（refreshConfig 控制是否同时刷新配置）
  const fetchPageState = useCallback(async (refreshConfig = true) => {
    if (!wikiId || !feishuConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setLoading(true);
    setPages([]);
    try {
      const client = GetApiClient();
      const response: QueryWikiFeishuPageTasksCommandResponse | undefined =
        await client.api.wiki.plugin.feishu.query_page_state.get({
          queryParameters: { configId: feishuConfigId, wikiId },
        });

      setPages(response?.items || []);
      if (refreshConfig) {
        await fetchConfig();
      }
    } catch (error) {
      console.error("获取飞书状态失败:", error);
      proxyRequestError(error, messageApi, "获取飞书状态失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, feishuConfigId, messageApi, fetchConfig]);

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
        wikiId,
        isStart: true,
        isAutoProcess: isAutoProcess || undefined,
        autoProcessConfig: autoProcessConfig || undefined,
      };

      await client.api.wiki.plugin.feishu.lanuch_task.post(requestBody);
      messageApi.success("飞书同步已启动");
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
        wikiId,
        isStart: false,
      };

      await client.api.wiki.plugin.feishu.lanuch_task.post(requestBody);
      messageApi.success("飞书同步已停止");
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

  // 打开编辑配置模态窗口
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

  // 保存配置
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

  // 自动刷新逻辑
  useEffect(() => {
    if (refreshTimerRef.current) {
      clearInterval(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }

    if (isWorking && autoRefresh && wikiId && feishuConfigId) {
      refreshTimerRef.current = setInterval(() => fetchPageState(false), 1000);
    }

    return () => {
      if (refreshTimerRef.current) {
        clearInterval(refreshTimerRef.current);
        refreshTimerRef.current = null;
      }
    };
  }, [isWorking, autoRefresh, fetchPageState, wikiId, feishuConfigId]);

  // 表格列定义
  const columns = [
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
      width: 100,
      render: (state: WorkerState) => {
        const info = getStateInfo(state);
        return <Tag color={info.color}>{info.text}</Tag>;
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
      width: 100,
      render: (size: number) => size ? FileSizeHelper.formatFileSize(size) : "-",
    },
    {
      title: "向量化",
      dataIndex: "isEmbedding",
      key: "isEmbedding",
      width: 80,
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
      width: 170,
      render: (time: string) => (time ? new Date(time).toLocaleString() : "-"),
    },
  ];

  // 参数校验
  if (!wikiId || !feishuConfigId) {
    return (
      <div className="page-container">
        <div className="moai-empty">
          <Text type="secondary">缺少必要参数</Text>
        </div>
      </div>
    );
  }

  const stateInfo = getStateInfo(workState);

  return (
    <div className="page-container">
      {contextHolder}

      {/* 页面标题区域 */}
      <div className="moai-page-header">
        <Space style={{ marginBottom: 8 }}>
          <Button
            type="text"
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/feishu`)}
          >
            返回列表
          </Button>
        </Space>
        <h1 className="moai-page-title">{configTitle || "飞书同步详情"}</h1>
        <p className="moai-page-subtitle">
          查看和管理飞书知识库同步任务的运行状态
        </p>
      </div>

      {/* 操作工具栏 */}
      <div className="moai-toolbar">
        <div className="moai-toolbar-left">
          <Button
            type="primary"
            icon={<PlayCircleOutlined />}
            onClick={() => setStartConfigModalVisible(true)}
            loading={starting}
            disabled={isWorking}
          >
            启动任务
          </Button>
          <Button
            danger
            icon={<StopOutlined />}
            onClick={handleStop}
            loading={stopping}
            disabled={!isWorking}
          >
            停止任务
          </Button>
          <Button
            icon={<EditOutlined />}
            onClick={handleEdit}
            disabled={isWorking}
          >
            编辑配置
          </Button>
        </div>
        <div className="moai-toolbar-right">
          <Button
            icon={<ReloadOutlined />}
            onClick={() => fetchPageState()}
            loading={loading}
          >
            刷新
          </Button>
        </div>
      </div>

      {/* 任务状态卡片 */}
      <Card style={{ marginBottom: 16 }}>
        <Row gutter={[24, 16]}>
          <Col xs={24} sm={12} md={6}>
            <div className="feishu-stat-item">
              <Text type="secondary">任务状态</Text>
              <div className="feishu-stat-value">
                <Tag color={stateInfo.color} className="feishu-status-tag">
                  {stateInfo.text}
                </Tag>
              </div>
            </div>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <div className="feishu-stat-item">
              <Text type="secondary">成功</Text>
              <div className="feishu-stat-value">
                <span className="feishu-stat-value-success">{stats.successCount}</span>
              </div>
            </div>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <div className="feishu-stat-item">
              <Text type="secondary">失败</Text>
              <div className="feishu-stat-value">
                <span className="feishu-stat-value-error">{stats.failedCount}</span>
              </div>
            </div>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <div className="feishu-stat-item">
              <Text type="secondary">处理中</Text>
              <div className="feishu-stat-value">
                <span className="feishu-stat-value-processing">{stats.processingCount}</span>
              </div>
            </div>
          </Col>
        </Row>
        {workMessage && (
          <div className="feishu-work-message">
            <Text type="secondary">运行信息：</Text>
            <Text>{workMessage}</Text>
          </div>
        )}
      </Card>

      {/* 配置信息卡片 */}
      <Card
        title={
          <Space>
            <SettingOutlined />
            <span>配置信息</span>
          </Space>
        }
        style={{ marginBottom: 16 }}
        loading={configLoading}
      >
        {config ? (
          <Descriptions column={{ xs: 1, sm: 2, md: 3 }} size="small">
            <Descriptions.Item label="配置名称">{configTitle || "-"}</Descriptions.Item>
            <Descriptions.Item label="飞书应用ID">{config.appId || "-"}</Descriptions.Item>
            <Descriptions.Item label="知识库ID">{config.spaceId || "-"}</Descriptions.Item>
            <Descriptions.Item label="目录Token">
              {config.parentNodeToken || "-"}
            </Descriptions.Item>
            <Descriptions.Item label="覆盖已有页面">
              <Tag color={config.isOverExistPage ? "success" : "default"}>
                {config.isOverExistPage ? "是" : "否"}
              </Tag>
            </Descriptions.Item>
          </Descriptions>
        ) : (
          <div className="moai-empty">
            <Text type="secondary">暂无配置信息</Text>
          </div>
        )}
      </Card>

      {/* 页面列表 */}
      <Card
        title={
          <Space>
            <Title level={5} style={{ margin: 0 }}>
              页面列表
            </Title>
            <Tag>{stats.total} 条</Tag>
          </Space>
        }
        extra={
          isWorking && (
            <Space>
              <Text type="secondary">自动刷新</Text>
              <Switch
                checked={autoRefresh}
                onChange={setAutoRefresh}
                checkedChildren="开"
                unCheckedChildren="关"
                size="small"
              />
            </Space>
          )
        }
      >
        <Spin spinning={loading}>
          <Table
            columns={columns}
            dataSource={pages}
            rowKey="pageId"
            pagination={false}
            scroll={{ x: 1000 }}
            size="small"
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
        width={720}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={form} layout="vertical" style={{ marginTop: 16 }}>
          <Form.Item
            name="title"
            label="配置名称"
            rules={[{ required: true, message: "请输入配置名称" }]}
          >
            <Input placeholder="请输入配置名称" />
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="appId"
                label="飞书应用ID"
                rules={[{ required: true, message: "请输入飞书应用ID" }]}
              >
                <Input placeholder="请输入飞书应用ID" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="appSecret"
                label="飞书应用密钥"
                rules={[{ required: true, message: "请输入飞书应用密钥" }]}
              >
                <Input.Password placeholder="请输入飞书应用密钥" />
              </Form.Item>
            </Col>
          </Row>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="spaceId"
                label="飞书知识库ID"
                rules={[{ required: true, message: "请输入飞书知识库ID" }]}
              >
                <Input placeholder="请输入飞书知识库ID" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="parentNodeToken"
                label="顶部文档Token"
                tooltip="可选：指定从哪个文档节点开始同步"
              >
                <Input placeholder="可选：顶部文档Token" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="isOverExistPage"
            label="覆盖已有页面"
            tooltip="开启后将覆盖已经同步过的页面"
            valuePropName="checked"
          >
            <Switch checkedChildren="开启" unCheckedChildren="关闭" />
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
