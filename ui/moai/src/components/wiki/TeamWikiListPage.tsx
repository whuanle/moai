import { useEffect, useState, useCallback } from "react";
import {
  Card,
  Typography,
  Button,
  Space,
  Tag,
  Empty,
  Spin,
  message,
  Modal,
  Form,
  Input,
  Row,
  Col,
  Popconfirm,
  Tooltip,
} from "antd";
import {
  PlusOutlined,
  BookOutlined,
  DeleteOutlined,
  ClockCircleOutlined,
  ReloadOutlined,
  FileTextOutlined,
} from "@ant-design/icons";
import { Avatar } from "antd";
import { GetApiClient } from "../ServiceClient";
import type { 
  DeleteWikiCommand, 
  QueryWikiBaseListCommand,
} from "../../apiClient/models";
import { useNavigate } from "react-router";
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";

const { Title, Text, Paragraph } = Typography;

interface WikiItem {
  id: number;
  title: string;
  description: string;
  createTime?: string;
  documentCount?: number;
  avatar?: string;
}

interface CreateWikiFormData {
  name: string;
  description: string;
}

interface TeamWikiListPageProps {
  teamId: number;
  canManage?: boolean;
}

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
      hour12: false
    });
  } catch {
    return dateTimeString;
  }
};

// Wiki卡片组件
interface WikiCardProps {
  item: WikiItem;
  canManage: boolean;
  onDelete: (id: number) => void;
  onClick: (id: number) => void;
}

const WikiCard: React.FC<WikiCardProps> = ({ item, canManage, onDelete, onClick }) => {
  const handleCardClick = (e: React.MouseEvent) => {
    const target = e.target as HTMLElement;
    if (target.closest('.wiki-delete-btn') || target.closest('.ant-popover') || target.closest('.ant-popconfirm')) {
      return;
    }
    onClick(item.id);
  };

  return (
    <Col xs={24} sm={12} md={8} lg={6}>
      <Card
        hoverable
        onClick={handleCardClick}
        style={{ cursor: "pointer", height: "100%", position: "relative" }}
      >
        {canManage && (
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
                  onClick={(e) => e.stopPropagation()}
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
          avatar={
            <Avatar 
              size={48} 
              src={item.avatar} 
              icon={<BookOutlined />}
            />
          }
          title={
            <Space align="center" style={{ width: "100%" }}>
              <Text strong style={{ fontSize: "16px", flex: 1 }}>
                {item.title}
              </Text>
              <Tag color="orange">团队</Tag>
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: "100%" }}>
              <Paragraph 
                type="secondary" 
                ellipsis={{ rows: 2, tooltip: item.description }}
                style={{ margin: 0 }}
              >
                {item.description || "暂无描述"}
              </Paragraph>
              
              <Space size="small" wrap>
                <Tag color="purple" icon={<FileTextOutlined />}>
                  {item.documentCount || 0} 文档
                </Tag>
              </Space>
              
              {item.createTime && (
                <Space size="small" style={{ marginTop: 8 }}>
                  <ClockCircleOutlined />
                  <Text type="secondary" style={{ fontSize: "12px" }}>
                    {formatDateTime(item.createTime)}
                  </Text>
                </Space>
              )}
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
  loading: boolean;
  onCancel: () => void;
  onSubmit: (values: CreateWikiFormData) => void;
  form: any;
}

const CreateWikiModal: React.FC<CreateWikiModalProps> = ({ open, loading, onCancel, onSubmit, form }) => (
  <Modal
    title="新建团队知识库"
    open={open}
    onCancel={onCancel}
    onOk={() => form.submit()}
    confirmLoading={loading}
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
    </Form>
  </Modal>
);

// 主组件
export default function TeamWikiListPage({ teamId, canManage = false }: TeamWikiListPageProps) {
  const navigate = useNavigate();
  const [wikiList, setWikiList] = useState<WikiItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  const fetchWikiList = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const command: QueryWikiBaseListCommand = {
        teamId: teamId,
      };
      const response = await client.api.wiki.query_wiki_list.post(command);

      if (response) {
        const wikiItems: WikiItem[] = response.map((item) => ({
          id: item.wikiId!,
          title: item.name!,
          description: item.description || "",
          createTime: item.createTime || undefined,
          documentCount: item.documentCount || 0,
          avatar: item.avatar || undefined,
        }));
        setWikiList(wikiItems);
      }
    } catch (error) {
      console.error("获取团队知识库列表失败:", error);
      proxyRequestError(error, messageApi, "获取团队知识库列表失败");
    } finally {
      setLoading(false);
    }
  }, [teamId, messageApi]);

  useEffect(() => {
    if (teamId) {
      fetchWikiList();
    }
  }, [teamId, fetchWikiList]);

  const handleCreate = async (values: CreateWikiFormData) => {
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      await client.api.wiki.create.post({
        name: values.name,
        description: values.description,
        teamId: teamId,
      });
      messageApi.success("创建成功");
      setModalOpen(false);
      form.resetFields();
      fetchWikiList();
    } catch (error) {
      proxyFormRequestError(error, messageApi, form, "创建失败");
    } finally {
      setCreateLoading(false);
    }
  };

  const handleDelete = async (id: number) => {
    try {
      const client = GetApiClient();
      const deleteCommand: DeleteWikiCommand = { wikiId: id };
      await client.api.wiki.manager.delete_wiki.delete(deleteCommand);
      messageApi.success("删除成功");
      fetchWikiList();
    } catch (error) {
      console.error("删除知识库失败:", error);
      proxyRequestError(error, messageApi, "删除失败");
    }
  };

  const handleCardClick = (id: number) => {
    navigate(`/app/wiki/${id}`);
  };

  return (
    <>
      {contextHolder}
      
      <CreateWikiModal
        open={modalOpen}
        loading={createLoading}
        onCancel={() => {
          setModalOpen(false);
          form.resetFields();
        }}
        onSubmit={handleCreate}
        form={form}
      />

      <Card>
        <Space style={{ marginBottom: 16, width: "100%", justifyContent: "space-between" }}>
          <Space>
            <BookOutlined />
            <Title level={4} style={{ margin: 0 }}>团队知识库</Title>
          </Space>
          <Space>
            {canManage && (
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={() => setModalOpen(true)}
              >
                新建知识库
              </Button>
            )}
            <Button
              icon={<ReloadOutlined />}
              onClick={fetchWikiList}
              loading={loading}
            >
              刷新
            </Button>
          </Space>
        </Space>
          <Spin spinning={loading}>
            {wikiList.length > 0 ? (
              <Row gutter={[16, 16]}>
                {wikiList.map((item) => (
                  <WikiCard
                    key={item.id}
                    item={item}
                    canManage={canManage}
                    onDelete={handleDelete}
                    onClick={handleCardClick}
                  />
                ))}
              </Row>
            ) : (
              <Empty
                description="暂无团队知识库"
                image={Empty.PRESENTED_IMAGE_SIMPLE}
              />
            )}
          </Spin>
        </Card>
    </>
  );
}
