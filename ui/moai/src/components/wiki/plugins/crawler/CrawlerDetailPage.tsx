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
  InputNumber,
  Row,
  Col,
  Descriptions,
  Tooltip,
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
  QueryWikiCrawlerPageTasksCommandResponse,
  WikiCrawlerPageItem,
  StartWikiCrawlerPluginTaskCommand,
  WorkerState,
  WorkerStateObject,
  QueryWikiCrawlerConfigCommandResponse,
  WikiCrawlerConfig,
  UpdateWikiCrawlerConfigCommand,
  WikiPluginAutoProcessConfig,
} from "../../../../apiClient/models";
import {
  proxyRequestError,
  proxyFormRequestError,
} from "../../../../helper/RequestError";
import { FileSizeHelper } from "../../../../helper/FileSizeHelper";
import StartTaskConfigModal from "../common/StartTaskConfigModal";
import ScheduledTaskConfigModal from "../common/ScheduledTaskConfigModal";
import "../../../../styles/theme.css";
import "./CrawlerDetailPage.css";
import { formatRelativeTime } from "../../../../helper/DateTimeHelper";

const { Title, Text } = Typography;
const { TextArea } = Input;

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

export default function CrawlerDetailPage() {
  const navigate = useNavigate();
  const { id, configId } = useParams<{ id: string; configId: string }>();
  const wikiId = id ? parseInt(id) : undefined;
  const crawlerConfigId = configId ? parseInt(configId) : undefined;

  const [loading, setLoading] = useState(false);
  const [pages, setPages] = useState<WikiCrawlerPageItem[]>([]);
  const [isWorking, setIsWorking] = useState(false);
  const [workState, setWorkState] = useState<WorkerState | null>(null);
  const [workMessage, setWorkMessage] = useState<string>("");
  const [messageApi, contextHolder] = message.useMessage();
  const [starting, setStarting] = useState(false);
  const [stopping, setStopping] = useState(false);
  const [autoRefresh, setAutoRefresh] = useState(true);
  const refreshTimerRef = useRef<NodeJS.Timeout | null>(null);

  // 配置相关状态
  const [config, setConfig] = useState<WikiCrawlerConfig | null>(null);
  const [configTitle, setConfigTitle] = useState<string>("");
  const [configLoading, setConfigLoading] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [startConfigModalVisible, setStartConfigModalVisible] = useState(false);

  // 定时任务相关状态
  const [jobRunning, setJobRunning] = useState(false);
  const [jobLoading, setJobLoading] = useState(false);
  const [jobModalVisible, setJobModalVisible] = useState(false);
  const [jobCron, setJobCron] = useState<string>("");
  const [jobNextExecution, setJobNextExecution] = useState<string>("");

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

  // 查询定时任务状态
  const fetchJobStatus = useCallback(async () => {
    if (!wikiId || !crawlerConfigId) return;

    try {
      const client = GetApiClient();
      const response = await client.api.wiki.plugin.query_job.post({
        configId: crawlerConfigId, wikiId,
      });

      setJobRunning(response?.isExist || false);
      setJobCron(response?.cron || "");
      setJobNextExecution(response?.nextExecution || "");
    } catch (error) {
      console.error("查询定时任务状态失败:", error);
      // 静默失败，不显示错误提示
    }
  }, [wikiId, crawlerConfigId]);

  // 获取配置信息
  const fetchConfig = useCallback(async () => {
    if (!wikiId || !crawlerConfigId) return;

    setConfigLoading(true);
    try {
      const client = GetApiClient();
      const response: QueryWikiCrawlerConfigCommandResponse | undefined =
        await client.api.wiki.plugin.crawler.config.get({
          queryParameters: { configId: crawlerConfigId, wikiId },
        });

      if (response) {
        setConfig(response.config || null);
        setConfigTitle(response.title || "");
        setWorkState(response.workState || null);
        setWorkMessage(response.workMessage || "");
        setIsWorking(response.workState === WorkerStateObject.Processing);
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
  }, [wikiId, crawlerConfigId, messageApi]);

  // 获取页面任务列表（refreshConfig 控制是否同时刷新配置）
  const fetchPageState = useCallback(async (refreshConfig = true) => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setLoading(true);
    setPages([]);
    try {
      const client = GetApiClient();
      const response: QueryWikiCrawlerPageTasksCommandResponse | undefined =
        await client.api.wiki.plugin.crawler.query_page_state.get({
          queryParameters: { configId: crawlerConfigId, wikiId },
        });

      setPages(response?.items || []);
      if (refreshConfig) {
        await fetchConfig();
      }
    } catch (error) {
      console.error("获取爬虫状态失败:", error);
      proxyRequestError(error, messageApi, "获取爬虫状态失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, crawlerConfigId, messageApi, fetchConfig]);

  // 启动爬虫
  const handleStart = async (
    isAutoProcess: boolean,
    autoProcessConfig: WikiPluginAutoProcessConfig | null
  ) => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setStarting(true);
    try {
      const client = GetApiClient();
      const requestBody: StartWikiCrawlerPluginTaskCommand = {
        configId: crawlerConfigId,
        wikiId,
        isStart: true,
      };

      // 只有开启自动处理时才传递相关配置
      if (isAutoProcess && autoProcessConfig) {
        requestBody.isAutoProcess = true;
        requestBody.autoProcessConfig = autoProcessConfig;
      }

      await client.api.wiki.plugin.crawler.lanuch_task.post(requestBody);
      messageApi.success("爬虫已启动");
      setAutoRefresh(true); // 启动任务后自动开启刷新
      setTimeout(() => {
        fetchConfig();
        fetchPageState();
      }, 1000);
    } catch (error) {
      console.error("启动爬虫失败:", error);
      proxyRequestError(error, messageApi, "启动爬虫失败");
    } finally {
      setStarting(false);
    }
  };

  // 停止爬虫
  const handleStop = async () => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setStopping(true);
    try {
      const client = GetApiClient();
      const requestBody: StartWikiCrawlerPluginTaskCommand = {
        configId: crawlerConfigId,
        wikiId,
        isStart: false,
      };

      await client.api.wiki.plugin.crawler.lanuch_task.post(requestBody);
      messageApi.success("爬虫已停止");
      setTimeout(() => {
        fetchConfig();
        fetchPageState();
      }, 1000);
    } catch (error) {
      console.error("停止爬虫失败:", error);
      proxyRequestError(error, messageApi, "停止爬虫失败");
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
      address: config.address || "",
      isCrawlOther: config.isCrawlOther || false,
      isOverExistPage: config.isOverExistPage || false,
      limitAddress: config.limitAddress || "",
      limitMaxCount: config.limitMaxCount || 100,
      limitMaxNewCount: config.limitMaxNewCount || 0,
      selector: config.selector || "",
      timeOutSecond: config.timeOutSecond || 30,
      userAgent:
        config.userAgent ||
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)",
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
      const updateBody: UpdateWikiCrawlerConfigCommand = {
        configId: crawlerConfigId,
        wikiId: wikiId || 0,
        title: values.title,
        address: values.address,
        isCrawlOther: values.isCrawlOther || false,
        limitAddress: values.limitAddress,
        limitMaxCount: values.limitMaxCount,
        limitMaxNewCount: values.limitMaxNewCount || 0,
        selector: values.selector,
        timeOutSecond: values.timeOutSecond,
        userAgent: values.userAgent,
        isOverExistPage: values.isOverExistPage || false,
      };

      await client.api.wiki.plugin.crawler.update_config.post(updateBody);
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

  // 切换定时任务状态
  const handleToggleJob = async () => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    // 如果当前正在运行，直接关闭
    if (jobRunning) {
      setJobLoading(true);
      try {
        const client = GetApiClient();
        await client.api.wiki.plugin.start_job.post({
          configId: crawlerConfigId,
          wikiId,
          isStart: false,
          cron: "*" // 关闭定时任务时，可以随便填
        });

        setJobRunning(false);
        messageApi.success("定时任务已关闭");
      } catch (error) {
        console.error("关闭定时任务失败:", error);
        proxyRequestError(error, messageApi, "关闭定时任务失败");
      } finally {
        setJobLoading(false);
      }
    } else {
      // 如果未运行，打开配置模态窗口
      setJobModalVisible(true);
    }
  };

  // 启动定时任务
  const handleStartJob = async (
    cron: string,
    isAutoProcess: boolean,
    autoProcessConfig: WikiPluginAutoProcessConfig | null
  ) => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setJobLoading(true);
    try {
      const client = GetApiClient();
      const requestBody: any = {
        configId: crawlerConfigId,
        wikiId,
        isStart: true,
        cron: cron,
        isAutoProcess: isAutoProcess,
        autoProcessConfig: autoProcessConfig,
      };

      await client.api.wiki.plugin.start_job.post(requestBody);

      setJobRunning(true);
      messageApi.success("定时任务已启动");
      // 重新查询定时任务状态以获取最新的 cron 和 nextExecution
      setTimeout(() => {
        fetchJobStatus();
      }, 500);
    } catch (error) {
      console.error("启动定时任务失败:", error);
      proxyRequestError(error, messageApi, "启动定时任务失败");
    } finally {
      setJobLoading(false);
    }
  };

  // 页面初始化
  useEffect(() => {
    if (wikiId && crawlerConfigId) {
      fetchConfig();
      fetchPageState();
      fetchJobStatus();
    }
  }, [wikiId, crawlerConfigId, fetchConfig, fetchPageState, fetchJobStatus]);

  // 自动刷新逻辑
  useEffect(() => {
    if (refreshTimerRef.current) {
      clearInterval(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }

    if (isWorking && autoRefresh && wikiId && crawlerConfigId) {
      refreshTimerRef.current = setInterval(() => {
        fetchPageState(false);
        fetchConfig(); // 每次刷新时检测爬虫状态
      }, 1000);
    }

    return () => {
      if (refreshTimerRef.current) {
        clearInterval(refreshTimerRef.current);
        refreshTimerRef.current = null;
      }
    };
  }, [isWorking, autoRefresh, fetchPageState, fetchConfig, wikiId, crawlerConfigId]);

  // 表格列定义
  const columns = [
    {
      title: "URL",
      dataIndex: "url",
      key: "url",
      width: 360,
      ellipsis: true,
      render: (text: string) => (
        <a href={text} target="_blank" rel="noopener noreferrer">
          {text || "-"}
        </a>
      ),
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
      width: 200,
      ellipsis: true,
      render: (text: string, record: WikiCrawlerPageItem) => {
        if (!text) return "-";
        
        // 如果 wikiDocumentId 不为 0，显示为可点击链接
        if (record.wikiDocumentId && record.wikiDocumentId !== 0) {
          return (
            <a
              onClick={() => navigate(`/app/wiki/${wikiId}/document/${record.wikiDocumentId}/embedding`)}
              style={{ cursor: "pointer" }}
            >
              {text}
            </a>
          );
        }
        
        return text;
      },
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
      width: 250,
      ellipsis: true,
      render: (text: string) => text || "-",
    },
    {
      title: "更新时间",
      dataIndex: "updateTime",
      key: "updateTime",
      width: 170,
      render: (time: string) => formatRelativeTime(time),
    },
  ];

  // 参数校验
  if (!wikiId || !crawlerConfigId) {
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
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/crawler`)}
          >
            返回列表
          </Button>
        </Space>
        <h1 className="moai-page-title">{configTitle || "爬虫任务详情"}</h1>
        <p className="moai-page-subtitle">
          查看和管理爬虫任务的运行状态和抓取结果
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
          <Tooltip
            title={
              jobRunning && jobCron && jobNextExecution
                ? `Cron: ${jobCron}\n下次执行: ${jobNextExecution}`
                : undefined
            }
          >
            <Button
              type={jobRunning ? "default" : "dashed"}
              icon={<SettingOutlined />}
              onClick={handleToggleJob}
              loading={jobLoading}
              danger={jobRunning}
            >
              {jobRunning ? "关闭定时任务" : "启动定时任务"}
            </Button>
          </Tooltip>
        </div>
        <div className="moai-toolbar-right">
          <Button
            icon={<ReloadOutlined />}
            onClick={() => {
              fetchPageState();
              fetchJobStatus();
            }}
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
            <div className="crawler-stat-item">
              <Text type="secondary">任务状态</Text>
              <div className="crawler-stat-value">
                <Tag color={stateInfo.color} className="crawler-status-tag">
                  {stateInfo.text}
                </Tag>
              </div>
            </div>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <div className="crawler-stat-item">
              <Text type="secondary">成功</Text>
              <div className="crawler-stat-value">
                <span className="crawler-stat-value-success">
                  {stats.successCount}
                </span>
              </div>
            </div>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <div className="crawler-stat-item">
              <Text type="secondary">失败</Text>
              <div className="crawler-stat-value">
                <span className="crawler-stat-value-error">
                  {stats.failedCount}
                </span>
              </div>
            </div>
          </Col>
          <Col xs={24} sm={12} md={6}>
            <div className="crawler-stat-item">
              <Text type="secondary">处理中</Text>
              <div className="crawler-stat-value">
                <span className="crawler-stat-value-processing">
                  {stats.processingCount}
                </span>
              </div>
            </div>
          </Col>
        </Row>
        {workMessage && (
          <div className="crawler-work-message">
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
            <Descriptions.Item label="目标地址" span={3}>
              <a href={config.address || "#"} target="_blank" rel="noopener noreferrer">
                {config.address || "-"}
              </a>
            </Descriptions.Item>
            <Descriptions.Item label="抓取关联页面">
              <Tag color={config.isCrawlOther ? "success" : "default"}>
                {config.isCrawlOther ? "是" : "否"}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="覆盖已有页面">
              <Tag color={config.isOverExistPage ? "success" : "default"}>
                {config.isOverExistPage ? "是" : "否"}
              </Tag>
            </Descriptions.Item>
            <Descriptions.Item label="最大抓取数量">
              {config.limitMaxCount || "-"}
            </Descriptions.Item>
            <Descriptions.Item label="最大新增数量">
              {config.limitMaxNewCount || "不限制"}
            </Descriptions.Item>
            <Descriptions.Item label="限制地址范围" span={config.limitAddress ? 3 : 1}>
              {config.limitAddress || "-"}
            </Descriptions.Item>
            <Descriptions.Item label="CSS 选择器">
              {config.selector ? <Text code>{config.selector}</Text> : "-"}
            </Descriptions.Item>
            <Descriptions.Item label="超时时间">
              {config.timeOutSecond ? `${config.timeOutSecond} 秒` : "-"}
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
          workState === WorkerStateObject.Processing && (
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
            scroll={{ x: 1260 }}
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

          <Form.Item
            name="address"
            label="目标网址"
            rules={[{ required: true, message: "请输入目标网址" }]}
          >
            <Input placeholder="请输入要爬取的网页地址" />
          </Form.Item>

          <Row gutter={16}>
            <Col span={12}>
              <Form.Item
                name="isCrawlOther"
                label="抓取关联页面"
                tooltip="开启后会自动查找并抓取该页面链接的其他页面"
                valuePropName="checked"
              >
                <Switch checkedChildren="开启" unCheckedChildren="关闭" />
              </Form.Item>
            </Col>
            <Col span={12}>
              <Form.Item
                name="isOverExistPage"
                label="覆盖已有页面"
                tooltip="开启后会覆盖已经爬取过的相同页面"
                valuePropName="checked"
              >
                <Switch checkedChildren="开启" unCheckedChildren="关闭" />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="limitAddress"
            label="限制地址范围"
            tooltip="限制自动爬取的网页都在该路径之下，需与目标网址具有相同域名"
          >
            <Input placeholder="可选，如 https://example.com/docs/" />
          </Form.Item>

          <Row gutter={16}>
            <Col span={8}>
              <Form.Item
                name="limitMaxCount"
                label="最大抓取数量"
                rules={[{ required: true, message: "请输入最大抓取数量" }]}
              >
                <InputNumber
                  min={1}
                  max={10000}
                  placeholder="最大抓取页面数"
                  style={{ width: "100%" }}
                />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                name="limitMaxNewCount"
                label="最大新增数量"
                tooltip="限制每次任务新抓取的页面数量上限，为 0 则不限制"
              >
                <InputNumber
                  min={0}
                  max={10000}
                  placeholder="可选，0 表示不限制"
                  style={{ width: "100%" }}
                />
              </Form.Item>
            </Col>
            <Col span={8}>
              <Form.Item
                name="timeOutSecond"
                label="超时时间（秒）"
                rules={[{ required: true, message: "请输入超时时间" }]}
              >
                <InputNumber
                  min={5}
                  max={300}
                  placeholder="单页超时时间"
                  style={{ width: "100%" }}
                />
              </Form.Item>
            </Col>
          </Row>

          <Form.Item
            name="selector"
            label="CSS 选择器"
            tooltip="用于定位要抓取的内容区域"
          >
            <Input placeholder="可选，如 article、.main-content" />
          </Form.Item>

          <Form.Item
            name="userAgent"
            label="User Agent"
            tooltip="自定义请求的 User Agent 标识"
          >
            <TextArea rows={2} placeholder="可选，自定义 User Agent" />
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

      {/* 定时任务配置模态窗口 */}
      <ScheduledTaskConfigModal
        open={jobModalVisible}
        onCancel={() => setJobModalVisible(false)}
        onConfirm={handleStartJob}
        wikiId={wikiId || 0}
        loading={jobLoading}
      />
    </div>
  );
}
