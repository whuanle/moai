import { useEffect, useState, useCallback } from "react";
import {
  Card,
  Typography,
  Button,
  Space,
  Input,
  Tag,
  Empty,
  Spin,
  message,
  Modal,
  Form,
  Switch,
  Row,
  Col,
  Popconfirm,
  Tooltip,
  Radio,
} from "antd";
import {
  PlusOutlined,
  BookOutlined,
  DeleteOutlined,
  UserOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  FileTextOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { DeleteWikiCommand, QueryWikiBaseListCommand } from "../../apiClient/models";
import { useNavigate } from "react-router";
import { proxyFormRequestError } from "../../helper/RequestError";

const { Title, Text, Paragraph } = Typography;

// 类型定义
interface WikiItem {
  id: number;
  title: string;
  description: string;
  isPublic: boolean;
  createUserName?: string;
  createTime?: string;
  isUser?: boolean;
  documentCount?: number;
  createUserId?: number;
}

interface CreateWikiFormData {
  name: string;
  description: string;
  isPublic: boolean;
}

// 工具函数
const formatDateTime = (dateTimeString?: string): string => {
  if (!dateTimeString) return "";
  
  try {
    const date = new Date(dateTimeString);
    if (isNaN(date.getTime())) return dateTimeString;
    
    return date.toLocaleString('zh-CN', {
      year: 'numeric',
      month: '2-digit',
      day: '2-digit',
      hour: '2-digit',
      minute: '2-digit',
      second: '2-digit',
      hour12: false
    });
  } catch (error) {
    return dateTimeString;
  }
};

// 自定义Hook - 用户信息管理
const useCurrentUser = () => {
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);

  const fetchCurrentUser = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const response = await client.api.common.userinfo.get();
      if (response) {
        setCurrentUserId(response.userId || null);
      }
    } catch (error) {
      console.error("获取用户信息失败:", error);
    } finally {
      setLoading(false);
    }
  }, []);

  return { currentUserId, loading, fetchCurrentUser };
};

// 自定义Hook - Wiki列表管理
const useWikiList = () => {
  const [wikiList, setWikiList] = useState<WikiItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [filterType, setFilterType] = useState<'none' | 'own' | 'public' | 'user'>('none');

  const fetchWikiList = useCallback(async (filter: 'none' | 'own' | 'public' | 'user' = 'none') => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const command: QueryWikiBaseListCommand = {
        isOwn: filter === 'own' ? true : undefined,
        isPublic: filter === 'public' ? true : undefined,
        isUser: filter === 'user' ? true : undefined,
      };
      const response = await client.api.wiki.query_wiki_list.post(command);

      if (response) {
        const wikiItems: WikiItem[] = response.map(
          (item) => ({
            id: item.wikiId!,
            title: item.name!,
            description: item.description || "",
            isPublic: item.isPublic || false,
            createUserName: item.createUserName || undefined,
            createTime: item.createTime || undefined,
            isUser: item.isUser || false,
            documentCount: item.documentCount || 0,
            createUserId: item.createUserId || undefined,
          })
        );
        setWikiList(wikiItems);
      }
    } catch (error) {
      console.error("获取知识库列表失败:", error);
      messageApi.error("获取知识库列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  const deleteWiki = useCallback(async (id: number) => {
    try {
      const client = GetApiClient();
      const deleteCommand: DeleteWikiCommand = { wikiId: id };
      await client.api.wiki.delete_wiki.delete(deleteCommand);
      messageApi.success("删除成功");
      fetchWikiList(filterType);
    } catch (error) {
      console.error("删除知识库失败:", error);
      messageApi.error("删除失败，请重试");
    }
  }, [messageApi, fetchWikiList, filterType]);

  const handleFilterChange = useCallback((filter: 'none' | 'own' | 'public' | 'user') => {
    setFilterType(filter);
    fetchWikiList(filter);
  }, [fetchWikiList]);

  return { 
    wikiList, 
    loading, 
    contextHolder, 
    fetchWikiList, 
    deleteWiki, 
    filterType, 
    handleFilterChange 
  };
};

// 自定义Hook - 创建Wiki表单管理
const useCreateWikiForm = (onSuccess: () => void) => {
  const [modalOpen, setModalOpen] = useState(false);
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();

  const handleSubmit = useCallback(async (values: CreateWikiFormData) => {
    try {
      const client = GetApiClient();
      await client.api.wiki.create.post({
        name: values.name,
        description: values.description,
      });
      messageApi.success("创建成功");
      setModalOpen(false);
      form.resetFields();
      onSuccess();
    } catch (err: any) {
      proxyFormRequestError(err, messageApi, form, "创建失败");
    }
  }, [form, messageApi, onSuccess]);

  const handleCancel = useCallback(() => {
    setModalOpen(false);
    form.resetFields();
  }, [form]);

  return {
    modalOpen,
    setModalOpen,
    form,
    contextHolder,
    handleSubmit,
    handleCancel,
  };
};

// Wiki卡片组件
interface WikiCardProps {
  item: WikiItem;
  currentUserId: number | null;
  onDelete: (id: number) => void;
  onClick: (id: number) => void;
}

const WikiCard: React.FC<WikiCardProps> = ({ item, currentUserId, onDelete, onClick }) => {
  const canDelete = currentUserId === item.createUserId;

  const handleCardClick = useCallback((e: React.MouseEvent) => {
    const target = e.target as HTMLElement;
    if (target.closest('.wiki-delete-btn') || target.closest('.ant-popover') || target.closest('.ant-popconfirm')) {
      return;
    }
    onClick(item.id);
  }, [item.id, onClick]);

  const handleDelete = useCallback((e: React.MouseEvent) => {
    e.stopPropagation();
    e.preventDefault();
  }, []);

  return (
    <Col xs={24} sm={12} md={8} lg={6}>
      <Card
        hoverable
        onClick={handleCardClick}
        style={{ cursor: "pointer", height: "100%", position: "relative" }}
      >
        {canDelete && (
          <div style={{ 
            position: "absolute", 
            top: 8, 
            right: 8, 
            zIndex: 10,
            backgroundColor: "rgba(255, 255, 255, 0.9)",
            borderRadius: "50%",
            padding: 4
          }}>
            <Tooltip title="删除知识库">
              <Popconfirm
                title="删除知识库"
                description="确定要删除这个知识库吗？删除后无法恢复。"
                okText="确认删除"
                cancelText="取消"
                onConfirm={() => onDelete(item.id)}
              >
                <Button
                  className="wiki-delete-btn"
                  type="text"
                  icon={<DeleteOutlined style={{ fontSize: '14px' }} />}
                  danger
                  size="small"
                  onClick={handleDelete}
                  style={{ 
                    width: 24, 
                    height: 24, 
                    padding: 0,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center'
                  }}
                />
              </Popconfirm>
            </Tooltip>
          </div>
        )}
        <Card.Meta
          title={
            <Space align="center" style={{ width: "100%" }}>
              <Text strong style={{ fontSize: "16px", flex: 1 }}>
                {item.title}
              </Text>
              {item.isUser && (
                <Tag color="orange">成员</Tag>
              )}
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              <Paragraph 
                type="secondary" 
                ellipsis={{ rows: 2, tooltip: item.description }}
                style={{ margin: 0 }}
              >
                {item.description}
              </Paragraph>
              
              <Space size="small" wrap>
                {item.isPublic ? (
                  <Tag color="green">公开</Tag>
                ) : (
                  <Tag color="blue">私有</Tag>
                )}
                <Tag color="purple" icon={<FileTextOutlined />}>
                  {item.documentCount || 0} 文档
                </Tag>
              </Space>
              
              <Space direction="vertical" size="small" style={{ marginTop: 8 }}>
                <Space size="small">
                  <UserOutlined />
                  <Text type="secondary" style={{ fontSize: "12px" }}>
                    {item.createUserName || "未知用户"}
                  </Text>
                </Space>
                {item.createTime && (
                  <Space size="small">
                    <ClockCircleOutlined />
                    <Text type="secondary" style={{ fontSize: "12px" }}>
                      {formatDateTime(item.createTime)}
                    </Text>
                  </Space>
                )}
              </Space>
            </Space>
          }
        />
      </Card>
    </Col>
  );
};

// 创建Wiki表单组件
interface CreateWikiModalProps {
  open: boolean;
  onCancel: () => void;
  onSubmit: (values: CreateWikiFormData) => void;
  form: any;
}

const CreateWikiModal: React.FC<CreateWikiModalProps> = ({ open, onCancel, onSubmit, form }) => (
  <Modal
    title="新建知识库"
    open={open}
    onCancel={onCancel}
    onOk={() => form.submit()}
    maskClosable={false}
    okText="创建"
    cancelText="取消"
    destroyOnClose
  >
    <Form 
      form={form} 
      layout="vertical" 
      preserve={false}
      onFinish={onSubmit}
    >
      <Form.Item
        label="知识库名称"
        name="name"
        rules={[{ required: true, message: "请输入知识库名称" }]}
      >
        <Input placeholder="请输入知识库名称" maxLength={32} />
      </Form.Item>
      <Form.Item label="描述" name="description">
        <Input.TextArea placeholder="请输入描述" maxLength={200} rows={3} />
      </Form.Item>
      <Form.Item label="是否公开" name="isPublic" valuePropName="checked" initialValue={false}>
        <Switch checkedChildren="公开" unCheckedChildren="私有" />
      </Form.Item>
    </Form>
  </Modal>
);

// 主组件
export default function WikiPage() {
  const navigate = useNavigate();
  const { currentUserId, fetchCurrentUser } = useCurrentUser();
  const { 
    wikiList, 
    loading, 
    contextHolder: listContextHolder, 
    fetchWikiList, 
    deleteWiki, 
    filterType, 
    handleFilterChange 
  } = useWikiList();
  const { 
    modalOpen, 
    setModalOpen, 
    form, 
    contextHolder: formContextHolder, 
    handleSubmit, 
    handleCancel 
  } = useCreateWikiForm(() => fetchWikiList(filterType));

  useEffect(() => {
    fetchCurrentUser();
    fetchWikiList();
  }, [fetchCurrentUser, fetchWikiList]);

  const handleAddWiki = useCallback(() => {
    setModalOpen(true);
  }, [setModalOpen]);

  const handleRefresh = useCallback(() => {
    fetchWikiList(filterType);
  }, [fetchWikiList, filterType]);

  const handleCardClick = useCallback((id: number) => {
    // 找到对应的知识库项
    const wikiItem = wikiList.find(item => item.id === id);
    if (!wikiItem) return;
    
    // 检查用户是否为该知识库的成员
    if (!wikiItem.isUser && currentUserId !== wikiItem.createUserId) {
      message.error("您不是该知识库成员");
      return;
    }
    
    navigate(`/app/wiki/${id}`);
  }, [navigate, wikiList, currentUserId]);

  const handleDeleteWiki = useCallback((id: number) => {
    deleteWiki(id);
  }, [deleteWiki]);

  return (
    <>
      {listContextHolder}
      {formContextHolder}
      
      <CreateWikiModal
        open={modalOpen}
        onCancel={handleCancel}
        onSubmit={handleSubmit}
        form={form}
      />

      <Card style={{ margin: "24px 24px 0" }}>
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Space size="large">
            <Space>
              <BookOutlined />
              <Title level={3} style={{ margin: 0 }}>知识库管理</Title>
            </Space>
            <Space>
            <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={handleAddWiki}
              >
                新建知识库
              </Button>
              <Button
                icon={<ReloadOutlined />}
                onClick={handleRefresh}
                loading={loading}
              >
                刷新
              </Button>
              <Radio.Group 
                value={filterType} 
                onChange={(e) => handleFilterChange(e.target.value)}
              >
                <Radio.Button value="none">全部</Radio.Button>
                <Radio.Button value="own">只看自己创建</Radio.Button>
                <Radio.Button value="public">只看公开</Radio.Button>
                <Radio.Button value="user">只看成员</Radio.Button>
              </Radio.Group>
            </Space>
          </Space>
        </div>
      </Card>

      <div style={{ padding: "0 24px 24px" }}>
        <Card>
          <Spin spinning={loading}>
            {wikiList.length > 0 ? (
              <Row gutter={[16, 16]}>
                {wikiList.map((item) => (
                  <WikiCard
                    key={item.id}
                    item={item}
                    currentUserId={currentUserId}
                    onDelete={handleDeleteWiki}
                    onClick={handleCardClick}
                  />
                ))}
              </Row>
            ) : (
              <Empty
                description="暂无知识库数据"
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            )}
          </Spin>
        </Card>
      </div>
    </>
  );
}
