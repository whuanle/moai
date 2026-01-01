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
import { GetApiClient } from "../ServiceClient";
import { useOutletContext, useNavigate } from "react-router";
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";
import type { 
  QueryTeamListQueryResponseItem, 
  TeamRole,
  QueryWikiBaseListCommand,
  DeleteWikiCommand,
} from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";

const { Title, Text, Paragraph } = Typography;

interface TeamContext {
  teamInfo: QueryTeamListQueryResponseItem | null;
  myRole: TeamRole | null;
  refreshTeamInfo: () => void;
}

interface WikiItem {
  id: number;
  title: string;
  description: string;
  createTime?: string;
  documentCount?: number;
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

export default function TeamWiki() {
  const navigate = useNavigate();
  const { teamInfo, myRole } = useOutletContext<TeamContext>();
  const [wikiList, setWikiList] = useState<WikiItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalOpen, setModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  const fetchWikiList = useCallback(async () => {
    if (!teamInfo?.id) return;
    try {
      setLoading(true);
      const client = GetApiClient();
      const command: QueryWikiBaseListCommand = {
        teamId: teamInfo.id,
      };
      const response = await client.api.wiki.query_wiki_list.post(command);
      if (response) {
        const items: WikiItem[] = response.map((item) => ({
          id: item.wikiId!,
          title: item.name!,
          description: item.description || "",
          createTime: item.createTime || undefined,
          documentCount: item.documentCount || 0,
        }));
        setWikiList(items);
      }
    } catch (error) {
      console.error("获取知识库列表失败:", error);
      proxyRequestError(error, messageApi, "获取知识库列表失败");
    } finally {
      setLoading(false);
    }
  }, [teamInfo?.id, messageApi]);

  useEffect(() => {
    fetchWikiList();
  }, [fetchWikiList]);

  const handleCreate = async (values: { name: string; description?: string }) => {
    if (!teamInfo?.id) return;
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      await client.api.wiki.create.post({
        name: values.name,
        description: values.description,
        teamId: teamInfo.id,
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
      const command: DeleteWikiCommand = { wikiId: id };
      await client.api.wiki.manager.delete_wiki.delete(command);
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
      <Modal
        title="新建团队知识库"
        open={modalOpen}
        onCancel={() => {
          setModalOpen(false);
          form.resetFields();
        }}
        onOk={() => form.submit()}
        confirmLoading={createLoading}
        maskClosable={false}
        okText="创建"
        cancelText="取消"
        destroyOnClose
      >
        <Form form={form} layout="vertical" preserve={false} onFinish={handleCreate}>
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
                <Col xs={24} sm={12} md={8} key={item.id}>
                  <Card
                    hoverable
                    onClick={() => handleCardClick(item.id)}
                    style={{ cursor: "pointer", height: "100%", position: "relative" }}
                  >
                    {canManage && (
                      <div style={{ position: "absolute", top: 8, right: 8, zIndex: 10 }}>
                        <Tooltip title="删除知识库">
                          <Popconfirm
                            title="删除知识库"
                            description="确定要删除这个知识库吗？"
                            okText="确认"
                            cancelText="取消"
                            onConfirm={(e) => {
                              e?.stopPropagation();
                              handleDelete(item.id);
                            }}
                          >
                            <Button
                              type="text"
                              icon={<DeleteOutlined />}
                              danger
                              size="small"
                              onClick={(e) => e.stopPropagation()}
                            />
                          </Popconfirm>
                        </Tooltip>
                      </div>
                    )}
                    <Card.Meta
                      title={
                        <Text strong style={{ fontSize: "16px" }}>
                          {item.title}
                        </Text>
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
                            <Space size="small">
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
              ))}
            </Row>
          ) : (
            <Empty description="暂无知识库" image={Empty.PRESENTED_IMAGE_SIMPLE} />
          )}
        </Spin>
      </Card>
    </>
  );
}
