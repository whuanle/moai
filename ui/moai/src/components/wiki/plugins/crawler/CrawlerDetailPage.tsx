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
  InputNumber,
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
import StartTaskConfigModal from "../common/StartTaskConfigModal";

const { Title, Text } = Typography;
const { TextArea } = Input;
const { Panel } = Collapse;

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
  const [autoRefresh, setAutoRefresh] = useState(true); // 自动刷新开关，默认开启
  const refreshTimerRef = useRef<NodeJS.Timeout | null>(null);

  // 配置相关状态
  const [config, setConfig] = useState<WikiCrawlerConfig | null>(null);
  const [configTitle, setConfigTitle] = useState<string>("");
  const [configLoading, setConfigLoading] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [form] = Form.useForm();
  const [saving, setSaving] = useState(false);
  const [startConfigModalVisible, setStartConfigModalVisible] = useState(false);

  // 获取配置信息
  const fetchConfig = useCallback(async () => {
    if (!wikiId || !crawlerConfigId) {
      return;
    }

    setConfigLoading(true);
    try {
      const client = GetApiClient();
      const response: QueryWikiCrawlerConfigCommandResponse | undefined =
        await client.api.wiki.plugin.crawler.config.get({
          queryParameters: {
            configId: crawlerConfigId,
            wikiId: wikiId,
          },
        });

      if (response) {
        setConfig(response.config || null);
        setConfigTitle(response.title || "");
        setWorkState(response.workState || null);
        setWorkMessage(response.workMessage || "");
        // 根据 workState 判断是否正在工作
        // 只在 Processing 状态下才认为是正在工作（用于自动刷新）
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

  // 获取爬虫状态和任务列表
  const fetchPageState = useCallback(async () => {
    if (!wikiId || !crawlerConfigId) {
      messageApi.error("缺少必要参数");
      return;
    }

    setLoading(true);
    // 每次请求前先清空列表
    setPages([]);
    try {
      const client = GetApiClient();
      const response: QueryWikiCrawlerPageTasksCommandResponse | undefined =
        await client.api.wiki.plugin.crawler.query_page_state.get({
          queryParameters: {
            configId: crawlerConfigId,
            wikiId: wikiId,
          },
        });

      if (response) {
        setPages(response.items || []);
      } else {
        setPages([]);
      }

      // 同时刷新配置信息以获取最新的工作状态
      await fetchConfig();
    } catch (error) {
      console.error("获取爬虫状态失败:", error);
      proxyRequestError(error, messageApi, "获取爬虫状态失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, crawlerConfigId, messageApi, fetchConfig]);

  // 打开启动配置模态窗口
  const handleOpenStartConfig = () => {
    setStartConfigModalVisible(true);
  };

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
        wikiId: wikiId,
        isStart: true, // true 表示启动任务
        isAutoProcess: isAutoProcess || undefined,
        autoProcessConfig: autoProcessConfig || undefined,
      };

      await client.api.wiki.plugin.crawler.lanuch_task.post(requestBody);
      messageApi.success("爬虫已启动");
      // 延迟一下再刷新状态，给服务端一些时间
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
        wikiId: wikiId,
        isStart: false, // false 表示停止任务
      };

      await client.api.wiki.plugin.crawler.lanuch_task.post(requestBody);
      messageApi.success("爬虫已停止");
      // 延迟一下再刷新状态
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

  // 处理编辑配置
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
      selector: config.selector || "",
      timeOutSecond: config.timeOutSecond || 30,
      userAgent:
        config.userAgent ||
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)",
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

      const updateBody: UpdateWikiCrawlerConfigCommand = {
        configId: crawlerConfigId,
        wikiId: wikiId || 0,
        title: values.title,
        address: values.address,
        isCrawlOther: values.isCrawlOther || false,
        limitAddress: values.limitAddress,
        limitMaxCount: values.limitMaxCount,
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

  // 页面初始化
  useEffect(() => {
    if (wikiId && crawlerConfigId) {
      fetchConfig();
      fetchPageState();
    }
  }, [wikiId, crawlerConfigId, fetchConfig, fetchPageState]);

  // 自动刷新逻辑：当 isWorking 为 true 且 autoRefresh 为 true 时，每秒刷新
  useEffect(() => {
    // 清除之前的定时器
    if (refreshTimerRef.current) {
      clearInterval(refreshTimerRef.current);
      refreshTimerRef.current = null;
    }

    // 如果 isWorking 为 true 且 autoRefresh 为 true，则启动自动刷新
    if (isWorking && autoRefresh && wikiId && crawlerConfigId) {
      refreshTimerRef.current = setInterval(() => {
        fetchPageState();
      }, 1000); // 每秒刷新
    }

    // 清理函数：组件卸载或依赖变化时清除定时器
    return () => {
      if (refreshTimerRef.current) {
        clearInterval(refreshTimerRef.current);
        refreshTimerRef.current = null;
      }
    };
  }, [isWorking, autoRefresh, fetchPageState, wikiId, crawlerConfigId]);

  // 任务状态表格列定义
  const pageColumns = [
    {
      title: "URL",
      dataIndex: "url",
      key: "url",
      width: 400, 
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

  if (!wikiId || !crawlerConfigId) {
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
            onClick={() => navigate(`/app/wiki/${wikiId}/plugin/crawler`)}
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
                <Text strong style={{ marginLeft: 10 }}>爬取中：</Text>
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
                title={isWorking ? "爬虫运行中，无法编辑配置" : "编辑配置"}
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
                    <Text strong>页面地址：</Text>
                    <Text>{config.address || "-"}</Text>
                  </div>
                  <div>
                    <Text strong>是否抓取其他页面：</Text>
                    <Tag color={config.isCrawlOther ? "success" : "default"}>
                      {config.isCrawlOther ? "是" : "否"}
                    </Tag>
                  </div>
                  <div>
                    <Text strong>是否覆盖已爬取页面：</Text>
                    <Tag color={config.isOverExistPage ? "success" : "default"}>
                      {config.isOverExistPage ? "是" : "否"}
                    </Tag>
                  </div>
                  {config.limitAddress && (
                    <div>
                      <Text strong>限制地址：</Text>
                      <Text>{config.limitAddress}</Text>
                    </div>
                  )}
                  <div>
                    <Text strong>最大抓取数量：</Text>
                    <Text>{config.limitMaxCount || "-"}</Text>
                  </div>
                  {config.selector && (
                    <div>
                      <Text strong>选择器：</Text>
                      <Text code>{config.selector}</Text>
                    </div>
                  )}
                  <div>
                    <Text strong>超时时间（秒）：</Text>
                    <Text>{config.timeOutSecond || "-"}</Text>
                  </div>
                  {config.userAgent && (
                    <div>
                      <Text strong>User Agent：</Text>
                      <Text code style={{ fontSize: "12px" }}>
                        {config.userAgent}
                      </Text>
                    </div>
                  )}
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
          {workState === WorkerStateObject.Processing && (
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
            name="address"
            label="页面地址"
            rules={[{ required: true, message: "请输入页面地址" }]}
          >
            <Input placeholder="请输入要爬取的页面地址" />
          </Form.Item>

          <Form.Item
            name="isCrawlOther"
            label="是否抓取其他页面"
            tooltip="会自动查找这个页面或对应目录下的其它页面"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="isOverExistPage"
            label="是否覆盖已爬取页面"
            tooltip="如果开启，将覆盖会已经爬取过的页面"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>

          <Form.Item
            name="limitAddress"
            label="限制地址"
            tooltip="限制自动爬取的网页都在该路径之下，limitAddress跟address必须具有相同域名"
          >
            <Input placeholder="可选：限制爬取的地址范围" />
          </Form.Item>

          <Form.Item
            name="limitMaxCount"
            label="最大抓取数量"
            rules={[{ required: true, message: "请输入最大抓取数量" }]}
          >
            <InputNumber
              min={1}
              placeholder="最大抓取数量"
              style={{ width: "100%" }}
            />
          </Form.Item>

          <Form.Item
            name="selector"
            label="选择器"
            tooltip="CSS选择器，用于定位要抓取的内容"
          >
            <Input placeholder="可选：CSS选择器" />
          </Form.Item>

          <Form.Item
            name="timeOutSecond"
            label="超时时间（秒）"
            rules={[{ required: true, message: "请输入超时时间" }]}
          >
            <InputNumber
              min={1}
              placeholder="超时时间（秒）"
              style={{ width: "100%" }}
            />
          </Form.Item>

          <Form.Item
            name="userAgent"
            label="User Agent"
            tooltip="可选：自定义User Agent"
          >
            <TextArea rows={2} placeholder="可选：自定义User Agent" />
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
