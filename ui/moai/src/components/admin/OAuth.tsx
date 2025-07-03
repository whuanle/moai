import { useEffect, useState } from "react";
import {
  Table,
  Button,
  Modal,
  Form,
  Input,
  Select,
  Space,
  Popconfirm,
  message,
  Card,
  Row,
  Col,
  Typography,
  Avatar,
  Tag,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SettingOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import {
  QueryAllOAuthPrividerDetailCommandResponseItem,
  CreateOAuthConnectionCommand,
  UpdateOAuthConnectionCommand,
  DeleteOAuthConnectionCommand,
  OAuthPrividerObject,
} from "../../apiClient/models";
import { proxyFormRequestError } from "../../helper/RequestError";

const { Title } = Typography;

function OAuth() {
  const [data, setData] = useState<
    QueryAllOAuthPrividerDetailCommandResponseItem[]
  >([]);
  const [loading, setLoading] = useState(false);
  const [submitLoading, setSubmitLoading] = useState(false);
  const [modalVisible, setModalVisible] = useState(false);
  const [editingItem, setEditingItem] =
    useState<QueryAllOAuthPrividerDetailCommandResponseItem | null>(null);
  const [form] = Form.useForm();
  const [messageApi, contextHolder] = message.useMessage();

  // 调试信息
  console.log("OAuthPrividerObject:", OAuthPrividerObject);
  console.log("OAuthPrividerObject entries:", Object.entries(OAuthPrividerObject));

  const fetchOAuthList = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.admin.oauth.detail_list.get();
      if (response?.items) {
        setData(response.items);
      }
    } catch (error) {
      messageApi.error("获取OAuth列表失败");
    } finally {
      setLoading(false);
    }
  };

  const handleCreate = async (values: any) => {
    setSubmitLoading(true);
    try {
      const client = GetApiClient();
      await client.api.admin.oauth.create.post(values);
      messageApi.success("新增成功");
      setModalVisible(false);
      form.resetFields();
      fetchOAuthList();
    } catch (error) {
      console.log("Create OAuth error:", error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitLoading(false);
    }
  };

  const handleEdit = async (values: any) => {
    if (!editingItem) return;

    setSubmitLoading(true);
    try {
      const client = GetApiClient();
      const updateData = { ...values, oAuthConnectionId: editingItem.id };
      await client.api.admin.oauth.update.put(updateData);
      message.success("编辑成功");
      setModalVisible(false);
      setEditingItem(null);
      form.resetFields();
      fetchOAuthList();
    } catch (error) {
      console.log("Edit OAuth error:", error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitLoading(false);
    }
  };

  const handleDelete = async (id: number) => {
    try {
      const client = GetApiClient();
      await client.api.admin.oauth.deletePath.delete({ oAuthConnectionId: id });
      messageApi.success("删除成功");
      fetchOAuthList();
    } catch (error) {
      messageApi.error("删除失败");
    }
  };

  const showCreateModal = () => {
    setEditingItem(null);
    form.resetFields();
    setModalVisible(true);
  };

  const showEditModal = (
    record: QueryAllOAuthPrividerDetailCommandResponseItem
  ) => {
    setEditingItem(record);
    form.setFieldsValue(record);
    setModalVisible(true);
  };

  const handleCancel = () => {
    setModalVisible(false);
    setEditingItem(null);
    form.resetFields();
    setSubmitLoading(false);
  };

  const handleSubmit = () => {
    form.validateFields().then((values) => {
      console.log("Form values:", values);
      if (editingItem) {
        handleEdit(values);
      } else {
        handleCreate(values);
      }
    });
  };

  useEffect(() => {
    fetchOAuthList();
  }, []);

  const columns = [
    {
      title: "图标",
      dataIndex: "iconUrl",
      key: "iconUrl",
      width: 80,
      render: (iconUrl: string) => (
        <Avatar src={iconUrl} size={40} icon={<SettingOutlined />} />
      ),
    },
    {
      title: "认证名称",
      dataIndex: "name",
      key: "name",
      render: (name: string) => (
        <Typography.Text strong>{name}</Typography.Text>
      ),
    },
    {
      title: "提供商",
      dataIndex: "provider",
      key: "provider",
      render: (provider: string) => <Tag color="blue">{provider}</Tag>,
    },
    {
      title: "应用Key",
      dataIndex: "key",
      key: "key",
      render: (key: string) => <Typography.Text code>{key}</Typography.Text>,
    },
    {
      title: "发现端口",
      dataIndex: "wellKnown",
      key: "wellKnown",
      render: (wellKnown: string) => (
        <Typography.Text type="secondary" style={{ fontSize: "12px" }}>
          {wellKnown || "-"}
        </Typography.Text>
      ),
    },    {
      title: "回调地址",
      dataIndex: "redirectUri",
      key: "redirectUri",
      render: (redirectUri: string) => (
        <Typography.Text type="secondary" style={{ fontSize: "12px" }}>
          {redirectUri || "-"}
        </Typography.Text>
      ),
    },
    {
      title: "操作",
      key: "action",
      width: 150,
      render: (
        _: any,
        record: QueryAllOAuthPrividerDetailCommandResponseItem
      ) => (
        <Space size="small">
          <Button
            type="link"
            icon={<EditOutlined />}
            onClick={() => showEditModal(record)}
            size="small"
          >
            编辑
          </Button>
          <Popconfirm
            title="确定要删除这个OAuth登录方式吗？"
            onConfirm={() => handleDelete(record.id!)}
            okText="确定"
            cancelText="取消"
          >
            <Button type="link" danger icon={<DeleteOutlined />} size="small">
              删除
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <>
      {contextHolder}

      <div style={{ padding: "24px" }}>
        <Card>
          <div
            style={{
              marginBottom: "16px",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <Title level={3} style={{ margin: 0 }}>
              <SettingOutlined style={{ marginRight: "8px" }} />
              OAuth登录方式管理
            </Title>
            <Space>
              <Button
                icon={<ReloadOutlined />}
                onClick={fetchOAuthList}
                loading={loading}
                size="large"
              >
                刷新
              </Button>
              <Button
                type="primary"
                icon={<PlusOutlined />}
                onClick={showCreateModal}
                size="large"
              >
                新增登录方式
              </Button>
            </Space>
          </div>

          <Table
            columns={columns}
            dataSource={data}
            rowKey="oAuthConnectionId"
            loading={loading}
            pagination={false}
          />
        </Card>

        <Modal
          title={editingItem ? "编辑OAuth登录方式" : "新增OAuth登录方式"}
          open={modalVisible}
          onOk={handleSubmit}
          onCancel={handleCancel}
          width={600}
          okText="确定"
          cancelText="取消"
          destroyOnClose
          confirmLoading={submitLoading}
        >
          <Form form={form} layout="vertical" style={{ marginTop: "16px" }}>
            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="name"
                  label="认证名称"
                  rules={[{ required: true, message: "请输入认证名称" }]}
                >
                  <Input placeholder="请输入认证名称" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="provider"
                  label="提供商"
                  rules={[{ required: true, message: "请选择提供商" }]}
                >
                  <Select placeholder="请选择提供商">
                    {Object.entries(OAuthPrividerObject).map(([key, value]) => (
                      <Select.Option key={key} value={value}>
                        {value}
                      </Select.Option>
                    ))}
                  </Select>
                </Form.Item>
              </Col>
            </Row>

            <Row gutter={16}>
              <Col span={12}>
                <Form.Item
                  name="key"
                  label="应用Key"
                  rules={[{ required: true, message: "请输入应用Key" }]}
                >
                  <Input placeholder="请输入应用Key" />
                </Form.Item>
              </Col>
              <Col span={12}>
                <Form.Item
                  name="secret"
                  label="应用Secret"
                  rules={[
                    {
                      required: !editingItem,
                      message: "请输入应用Secret"
                    }
                  ]}
                >
                  <Input.Password placeholder="请输入应用Secret" />
                </Form.Item>
              </Col>
            </Row>

            <Form.Item
              name="iconUrl"
              label="图标地址"
              rules={[{ required: true, message: "请输入图标地址" }]}
            >
              <Input placeholder="请输入图标URL地址" />
            </Form.Item>

            <Form.Item
              name="wellKnown"
              label="发现端口"
              rules={[{ required: true, message: "请输入发现端口" }]}
            >
              <Input placeholder="请输入OAuth发现端口地址" />
            </Form.Item>
          </Form>
        </Modal>
      </div>
    </>
  );
}

export default OAuth;
