import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Card,
  Button,
  Modal,
  message,
  Space,
  Typography,
  Table,
  Tooltip,
  Popconfirm,
  Form,
  Input,
  Switch,
  InputNumber,
  Empty,
  Row,
  Col,
  Descriptions,
  Tag,
  Alert,
} from "antd";
import {
  ReloadOutlined,
  PlusOutlined,
  EyeOutlined,
  DeleteOutlined,
  LoadingOutlined,
  LinkOutlined,
  SettingOutlined,
  UserOutlined,
  ClockCircleOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useNavigate } from "react-router";
import {
  QueryWikiWebConfigListCommandResponse,
  WeikiWebConfigSimpleItem,
  AddWebDocumentConfigCommand,
  DeleteWebDocumentConfigCommand,
} from "../../apiClient/models";
import { proxyFormRequestError } from "../../helper/RequestError";
import { formatDateTime } from "../../helper/DateTimeHelper";

const { Text, Title, Paragraph } = Typography;

// 类型定义
interface CrawleConfigItem {
  id: number;
  title: string;
  address: string;
  wikiId: number;
  createUserName?: string;
  updateUserName?: string;
  createTime?: string;
  updateTime?: string;
}

interface AddCrawleConfigForm {
  title: string;
  address: string;
  isAutoEmbedding: boolean;
  isCrawlOther: boolean;
  isWaitJs: boolean;
  limitAddress?: string;
  limitMaxCount?: number;
}

// 自定义Hook - 爬虫配置列表管理
const useCrawleConfigList = (wikiId: string | undefined) => {
  const [crawleConfigs, setCrawleConfigs] = useState<CrawleConfigItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const apiClient = GetApiClient();

  const fetchCrawleConfigs = useCallback(async () => {
    if (!wikiId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const response = await apiClient.api.wiki.web.query_crawle_list.get({
        queryParameters: {
          wikiId: parseInt(wikiId),
        },
      });

      if (response?.items) {
        setCrawleConfigs(
          response.items.map((item: WeikiWebConfigSimpleItem) => ({
            id: item.id!,
            title: item.title!,
            address: item.address!,
            wikiId: item.wikiId!,
            createUserName: item.createUserName || undefined,
            updateUserName: item.updateUserName || undefined,
            createTime: item.createTime || undefined,
            updateTime: item.updateTime || undefined,
          }))
        );
      }
    } catch (error) {
      console.error("Failed to fetch crawle configs:", error);
      messageApi.error("获取爬虫配置列表失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, apiClient]);

  const deleteCrawleConfig = useCallback(async (configId: number) => {
    if (!wikiId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      const deleteCommand: DeleteWebDocumentConfigCommand = {
        wikiId: parseInt(wikiId),
        wikiWebConfigId: configId,
        isDeleteWebDocuments: false,
      };

      await apiClient.api.wiki.web.delete_crawle_config.delete(deleteCommand);
      messageApi.success("删除成功");
      fetchCrawleConfigs();
    } catch (error) {
      console.error("Failed to delete crawle config:", error);
      messageApi.error("删除失败");
    }
  }, [wikiId, apiClient, fetchCrawleConfigs]);

  useEffect(() => {
    fetchCrawleConfigs();
  }, []);

  return {
    crawleConfigs,
    loading,
    contextHolder,
    fetchCrawleConfigs,
    deleteCrawleConfig,
  };
};

// 自定义Hook - 新增爬虫配置表单管理
const useAddCrawleConfigForm = (
  wikiId: string | undefined,
  onSuccess: () => void
) => {
  const [modalVisible, setModalVisible] = useState(false);
  const [loading, setLoading] = useState(false);
  const [form] = Form.useForm<AddCrawleConfigForm>();
  const [messageApi, contextHolder] = message.useMessage();
  const apiClient = GetApiClient();

  const handleSubmit = useCallback(async (values: AddCrawleConfigForm) => {
    if (!wikiId) {
      messageApi.error("缺少必要的参数");
      return;
    }

    try {
      setLoading(true);
      const addCommand: AddWebDocumentConfigCommand = {
        wikiId: parseInt(wikiId),
        title: values.title,
        address: values.address,
        isAutoEmbedding: values.isAutoEmbedding,
        isCrawlOther: values.isCrawlOther,
        isWaitJs: values.isWaitJs,
        limitAddress: values.limitAddress || undefined,
        limitMaxCount: values.limitMaxCount || undefined,
      };

      await apiClient.api.wiki.web.create_crawle_config.post(addCommand);
      messageApi.success("创建成功");
      setModalVisible(false);
      form.resetFields();
      onSuccess();
    } catch (error) {
      console.error("Failed to create crawle config:", error);
      proxyFormRequestError(error, messageApi, form, "创建失败");
    } finally {
      setLoading(false);
    }
  }, [wikiId, apiClient, messageApi, form, onSuccess]);

  const handleCancel = useCallback(() => {
    setModalVisible(false);
    form.resetFields();
  }, [form]);

  const showModal = useCallback(() => {
    setModalVisible(true);
  }, []);

  return {
    modalVisible,
    loading,
    contextHolder,
    form,
    handleSubmit,
    handleCancel,
    showModal,
  };
};

// 表格列配置组件
const useTableColumns = (
  wikiId: string | undefined,
  navigate: any,
  deleteCrawleConfig: (id: number) => Promise<void>
) => {
  const [deleteLoading, setDeleteLoading] = useState<number | null>(null);

  const handleDelete = useCallback(async (configId: number) => {
    setDeleteLoading(configId);
    try {
      await deleteCrawleConfig(configId);
    } finally {
      setDeleteLoading(null);
    }
  }, [deleteCrawleConfig]);

  const handleView = useCallback((record: CrawleConfigItem) => {
    navigate(`/app/wiki/${wikiId}/crawle/${record.id}`);
  }, [navigate, wikiId]);

  const columns = useMemo(() => [
    {
      title: "标题",
      dataIndex: "title",
      key: "title",
      width: 200,
      render: (text: string) => (
        <Text ellipsis={{ tooltip: text }} style={{ maxWidth: 180 }}>
          {text}
        </Text>
      ),
    },
    {
      title: "地址",
      dataIndex: "address",
      key: "address",
      width: 300,
      render: (text: string) => (
        <Space>
          <LinkOutlined style={{ color: '#1890ff' }} />
          <Text ellipsis={{ tooltip: text }} style={{ maxWidth: 280 }}>
            {text}
          </Text>
        </Space>
      ),
    },
    {
      title: "创建人",
      dataIndex: "createUserName",
      key: "createUserName",
      width: 120,
      render: (text: string) => (
        <Space>
          <UserOutlined />
          <Text>{text || "-"}</Text>
        </Space>
      ),
    },
    {
      title: "更新人",
      dataIndex: "updateUserName",
      key: "updateUserName",
      width: 120,
      render: (text: string) => (
        <Space>
          <UserOutlined />
          <Text>{text || "-"}</Text>
        </Space>
      ),
    },
    {
      title: "最后更新时间",
      dataIndex: "updateTime",
      key: "updateTime",
      width: 180,
      render: (text: string) => (
        <Space>
          <ClockCircleOutlined />
          <Text>{text ? formatDateTime(text) : "-"}</Text>
        </Space>
      ),
    },
    {
      title: "操作",
      key: "action",
      width: 150,
      fixed: "right" as const,
      render: (_: unknown, record: CrawleConfigItem) => (
        <Space>
          <Tooltip title="查看详情">
            <Button
              type="link"
              size="small"
              icon={<EyeOutlined />}
              onClick={() => handleView(record)}
            >
              查看
            </Button>
          </Tooltip>
          <Popconfirm
            title="删除爬虫配置"
            description="确定要删除这个爬虫配置吗？删除后无法恢复。"
            okText="确认删除"
            cancelText="取消"
            onConfirm={() => handleDelete(record.id)}
          >
            <Tooltip title="删除配置">
              <Button
                type="link"
                size="small"
                danger
                icon={
                  deleteLoading === record.id ? (
                    <LoadingOutlined />
                  ) : (
                    <DeleteOutlined />
                  )
                }
                loading={deleteLoading === record.id}
              >
                删除
              </Button>
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ], [handleView, handleDelete, deleteLoading]);

  return columns;
};

// 新增爬虫配置表单组件
const AddCrawleConfigForm: React.FC<{
  form: any;
  loading: boolean;
  onSubmit: (values: AddCrawleConfigForm) => void;
  onCancel: () => void;
}> = ({ form, loading, onSubmit, onCancel }) => {
  return (
    <Form
      form={form}
      layout="vertical"
      onFinish={onSubmit}
      initialValues={{
        isAutoEmbedding: true,
        isCrawlOther: false,
        isWaitJs: false,
      }}
    >
      <Row gutter={16}>
        <Col span={24}>
          <Form.Item
            label="标题"
            name="title"
            rules={[{ required: true, message: "请输入标题" }]}
          >
            <Input placeholder="请输入爬虫配置标题" />
          </Form.Item>
        </Col>
      </Row>

      <Row gutter={16}>
        <Col span={24}>
          <Form.Item
            label="页面地址"
            name="address"
            rules={[
              { required: true, message: "请输入页面地址" },
              { type: "url", message: "请输入有效的URL地址" },
            ]}
          >
            <Input placeholder="请输入要爬取的页面地址" />
          </Form.Item>
        </Col>
      </Row>

      <Row gutter={16}>
        <Col span={12}>
          <Form.Item
            label="是否抓取其它页面"
            name="isCrawlOther"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item
            label="是否自动向量化"
            name="isAutoEmbedding"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Col>
      </Row>

      <Row gutter={16}>
        <Col span={12}>
          <Form.Item
            label="限制地址"
            name="limitAddress"
          >
            <Input placeholder="可选：限制爬取范围" />
          </Form.Item>
        </Col>
        <Col span={12}>
          <Form.Item
            label="最大抓取数量"
            name="limitMaxCount"
          >
            <InputNumber
              min={1}
              max={1000}
              placeholder="可选：最大抓取数量"
              style={{ width: "100%" }}
            />
          </Form.Item>
        </Col>
      </Row>

      <Row gutter={16}>
        <Col span={24}>
          <Form.Item
            label="等待JS加载完成"
            name="isWaitJs"
            valuePropName="checked"
          >
            <Switch />
          </Form.Item>
        </Col>
      </Row>

      <Alert
        message="配置说明"
        description={
          <Space direction="vertical" size="small">
            <Text>• 抓取其它页面：开启后会自动爬取页面中的链接，加入到待爬取列表中</Text>
            <Text>• 限制地址：限制自动爬取的网页都在该路径之下，必须与页面地址具有相同域名</Text>
            <Text>• 自动向量化：爬取完成后自动进行文档向量化处理</Text>
            <Text>• 等待JS加载：开启此功能不一定可以抓取到完整内容，只有在抓取效果不满意时尝试开启</Text>
          </Space>
        }
        type="info"
        showIcon
        style={{ marginBottom: 16 }}
      />

      <Form.Item style={{ marginBottom: 0, textAlign: "right" }}>
        <Space>
          <Button onClick={onCancel}>
            取消
          </Button>
          <Button type="primary" htmlType="submit" loading={loading}>
            确定
          </Button>
        </Space>
      </Form.Item>
    </Form>
  );
};

// 主组件
export default function WikiCrawle() {
  const { id: wikiId } = useParams();
  const navigate = useNavigate();

  // 使用自定义Hooks
  const {
    crawleConfigs,
    loading,
    contextHolder: listContextHolder,
    fetchCrawleConfigs,
    deleteCrawleConfig,
  } = useCrawleConfigList(wikiId);

  const {
    modalVisible,
    loading: addLoading,
    contextHolder: formContextHolder,
    form,
    handleSubmit,
    handleCancel,
    showModal,
  } = useAddCrawleConfigForm(wikiId, fetchCrawleConfigs);

  const columns = useTableColumns(wikiId, navigate, deleteCrawleConfig);

  return (
    <>
      {listContextHolder}
      {formContextHolder}
      
      <Card>
        <Card.Meta
          title={
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Title level={4} style={{ margin: 0 }}>
                <SettingOutlined style={{ marginRight: 8 }} />
                爬虫配置列表
              </Title>
              <Space>
                <Button
                  icon={<ReloadOutlined />}
                  onClick={fetchCrawleConfigs}
                  loading={loading}
                >
                  刷新
                </Button>
                <Button
                  type="primary"
                  icon={<PlusOutlined />}
                  onClick={showModal}
                >
                  新增配置
                </Button>
              </Space>
            </Space>
          }
          description={
            <Paragraph type="secondary" style={{ marginBottom: 0 }}>
              管理网页爬虫配置，支持自动抓取和向量化处理
            </Paragraph>
          }
        />

        <Table
          columns={columns}
          dataSource={crawleConfigs}
          rowKey="id"
          loading={loading}
          pagination={{
            pageSize: 20,
            showSizeChanger: true,
            showQuickJumper: true,
            showTotal: (total) => `共 ${total} 条记录`,
          }}
          locale={{
            emptyText: (
              <Empty
                description="暂无爬虫配置"
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            ),
          }}
          scroll={{ x: 1200 }}
        />

        <Modal
          title={
            <Space>
              <PlusOutlined />
              新增爬虫配置
            </Space>
          }
          open={modalVisible}
          onCancel={handleCancel}
          footer={null}
          width={700}
          destroyOnClose
        >
          <AddCrawleConfigForm
            form={form}
            loading={addLoading}
            onSubmit={handleSubmit}
            onCancel={handleCancel}
          />
        </Modal>
      </Card>
    </>
  );
}
