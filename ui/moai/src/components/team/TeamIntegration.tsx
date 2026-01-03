import { useState, useEffect, useCallback } from "react";
import {
  Card,
  Table,
  Button,
  message,
  Modal,
  Form,
  Input,
  Space,
  Tag,
  Popconfirm,
  Typography,
  Descriptions,
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  StopOutlined,
  CheckCircleOutlined,
  KeyOutlined,
  CopyOutlined,
  ExclamationCircleOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { useParams, useOutletContext } from "react-router";
import { proxyRequestError, proxyFormRequestError } from "../../helper/RequestError";
import type {
  QueryExternalAppInfoCommandResponse,
  TeamRole,
} from "../../apiClient/models";
import { TeamRoleObject } from "../../apiClient/models";

const { TextArea } = Input;
const { Text, Paragraph } = Typography;

interface TeamContext {
  teamInfo: { id?: number; name?: string } | null;
  myRole: TeamRole;
  refreshTeamInfo: () => void;
}

interface ExternalAppItem extends QueryExternalAppInfoCommandResponse {
  // 扩展类型以便表格使用
}

export default function TeamIntegration() {
  const { id } = useParams();
  const { teamInfo, myRole } = useOutletContext<TeamContext>();
  const teamId = parseInt(id!);

  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [externalApp, setExternalApp] = useState<ExternalAppItem | null>(null);

  // 创建系统接入弹窗
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [createForm] = Form.useForm();
  const [createdKey, setCreatedKey] = useState<string | null>(null);

  // 编辑系统接入弹窗
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editLoading, setEditLoading] = useState(false);
  const [editForm] = Form.useForm();

  // 重置密钥弹窗
  const [resetKeyModalOpen, setResetKeyModalOpen] = useState(false);
  const [newKey, setNewKey] = useState<string | null>(null);
  const [resetLoading, setResetLoading] = useState(false);

  const canManage = myRole === TeamRoleObject.Owner || myRole === TeamRoleObject.Admin;

  // 加载系统接入信息
  const fetchExternalApp = useCallback(async () => {
    try {
      setLoading(true);
      const client = GetApiClient();
      const response = await client.api.app.external.info.get({
        queryParameters: { teamId },
      });
      setExternalApp(response || null);
    } catch (error) {
      console.log("获取系统接入信息:", error);
      setExternalApp(null);
    } finally {
      setLoading(false);
    }
  }, [teamId]);

  useEffect(() => {
    fetchExternalApp();
  }, [fetchExternalApp]);

  // 创建系统接入
  const handleCreate = async (values: { name: string; description?: string }) => {
    try {
      setCreateLoading(true);
      const client = GetApiClient();
      const response = await client.api.app.external.create.post({
        teamId,
        name: values.name,
        description: values.description,
      });
      
      if (response?.key) {
        setCreatedKey(response.key);
        messageApi.success("创建系统接入成功");
        fetchExternalApp();
      }
    } catch (error) {
      console.error("创建系统接入失败:", error);
      proxyFormRequestError(error, messageApi, createForm, "创建系统接入失败");
    } finally {
      setCreateLoading(false);
    }
  };

  // 关闭创建弹窗
  const handleCloseCreateModal = () => {
    setCreateModalOpen(false);
    setCreatedKey(null);
    createForm.resetFields();
  };

  // 打开编辑弹窗
  const handleOpenEditModal = () => {
    if (externalApp) {
      editForm.setFieldsValue({
        name: externalApp.name,
        description: externalApp.description,
      });
      setEditModalOpen(true);
    }
  };

  // 更新系统接入
  const handleUpdate = async (values: { name: string; description?: string }) => {
    try {
      setEditLoading(true);
      const client = GetApiClient();
      await client.api.app.external.update.post({
        teamId,
        name: values.name,
        description: values.description,
      });
      messageApi.success("更新系统接入成功");
      setEditModalOpen(false);
      fetchExternalApp();
    } catch (error) {
      console.error("更新系统接入失败:", error);
      proxyFormRequestError(error, messageApi, editForm, "更新系统接入失败");
    } finally {
      setEditLoading(false);
    }
  };

  // 启用/禁用系统接入
  const handleToggleDisable = async (isDisable: boolean) => {
    try {
      const client = GetApiClient();
      await client.api.app.external.set_disable.post({
        teamId,
        isDisable,
      });
      messageApi.success(isDisable ? "已禁用系统接入" : "已启用系统接入");
      fetchExternalApp();
    } catch (error) {
      console.error("操作失败:", error);
      proxyRequestError(error, messageApi, "操作失败");
    }
  };

  // 重置密钥
  const handleResetKey = async () => {
    try {
      setResetLoading(true);
      const client = GetApiClient();
      const response = await client.api.app.external.post({
        teamId,
      });
      
      if (response?.key) {
        setNewKey(response.key);
        messageApi.success("重置密钥成功");
      }
    } catch (error) {
      console.error("重置密钥失败:", error);
      proxyRequestError(error, messageApi, "重置密钥失败");
    } finally {
      setResetLoading(false);
    }
  };

  // 表格数据
  const tableData = externalApp ? [externalApp] : [];

  const columns = [
    {
      title: "名称",
      dataIndex: "name",
      key: "name",
    },
    {
      title: "描述",
      dataIndex: "description",
      key: "description",
      render: (text: string) => text || "-",
    },
    {
      title: "状态",
      key: "status",
      render: (_: unknown, record: ExternalAppItem) => (
        <Tag color={record.isDisable ? "red" : "green"}>
          {record.isDisable ? "已禁用" : "已启用"}
        </Tag>
      ),
    },
    {
      title: "密钥",
      dataIndex: "key",
      key: "key",
      render: (key: string) => (
        <Space>
          <Text copyable={{ text: key }}>
            {key ? `${key.substring(0, 8)}...${key.substring(key.length - 4)}` : "-"}
          </Text>
        </Space>
      ),
    },
    {
      title: "创建时间",
      dataIndex: "createTime",
      key: "createTime",
    },
    {
      title: "操作",
      key: "action",
      render: (_: unknown, record: ExternalAppItem) => {
        if (!canManage) return null;
        return (
          <Space>
            <Button
              type="link"
              size="small"
              icon={<EditOutlined />}
              onClick={handleOpenEditModal}
            >
              编辑
            </Button>
            <Button
              type="link"
              size="small"
              icon={record.isDisable ? <CheckCircleOutlined /> : <StopOutlined />}
              onClick={() => handleToggleDisable(!record.isDisable)}
            >
              {record.isDisable ? "启用" : "禁用"}
            </Button>
            <Popconfirm
              title="确认重置密钥"
              description="重置后原密钥将失效，确定要重置吗？"
              icon={<ExclamationCircleOutlined style={{ color: "red" }} />}
              onConfirm={() => setResetKeyModalOpen(true)}
              okText="确定"
              cancelText="取消"
            >
              <Button type="link" size="small" icon={<KeyOutlined />}>
                重置密钥
              </Button>
            </Popconfirm>
          </Space>
        );
      },
    },
  ];

  return (
    <>
      {contextHolder}
      <Card
        title={`${teamInfo?.name || ""} - 系统接入`}
        extra={
          canManage && !externalApp && (
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={() => setCreateModalOpen(true)}
            >
              创建系统接入
            </Button>
          )
        }
      >
        <Table
          rowKey="appId"
          columns={columns}
          dataSource={tableData}
          loading={loading}
          pagination={false}
          locale={{ emptyText: "暂未配置系统接入" }}
        />
      </Card>

      {/* 创建系统接入弹窗 */}
      <Modal
        title="创建系统接入"
        open={createModalOpen}
        onCancel={handleCloseCreateModal}
        footer={createdKey ? (
          <Button type="primary" onClick={handleCloseCreateModal}>
            我已保存密钥
          </Button>
        ) : null}
        destroyOnClose
        maskClosable={false}
      >
        {createdKey ? (
          <div>
            <Descriptions column={1} bordered>
              <Descriptions.Item label="应用密钥">
                <Space direction="vertical" style={{ width: "100%" }}>
                  <Paragraph
                    copyable={{
                      text: createdKey,
                      icon: <CopyOutlined />,
                      tooltips: ["复制", "已复制"],
                    }}
                    style={{ marginBottom: 0 }}
                  >
                    <Text code style={{ wordBreak: "break-all" }}>{createdKey}</Text>
                  </Paragraph>
                  <Text type="danger">
                    请妥善保存此密钥，关闭后将无法再次查看完整密钥！
                  </Text>
                </Space>
              </Descriptions.Item>
            </Descriptions>
          </div>
        ) : (
          <Form form={createForm} layout="vertical" onFinish={handleCreate}>
            <Form.Item
              name="name"
              label="接入名称"
              rules={[{ required: true, message: "请输入接入名称" }]}
            >
              <Input placeholder="请输入接入名称" maxLength={50} />
            </Form.Item>
            <Form.Item name="description" label="描述">
              <TextArea placeholder="请输入描述" rows={3} maxLength={200} />
            </Form.Item>
            <Form.Item>
              <Space>
                <Button type="primary" htmlType="submit" loading={createLoading}>
                  创建
                </Button>
                <Button onClick={handleCloseCreateModal}>取消</Button>
              </Space>
            </Form.Item>
          </Form>
        )}
      </Modal>

      {/* 编辑系统接入弹窗 */}
      <Modal
        title="编辑系统接入"
        open={editModalOpen}
        onCancel={() => setEditModalOpen(false)}
        footer={null}
        destroyOnClose
        maskClosable={false}
      >
        <Form form={editForm} layout="vertical" onFinish={handleUpdate}>
          <Form.Item
            name="name"
            label="接入名称"
            rules={[{ required: true, message: "请输入接入名称" }]}
          >
            <Input placeholder="请输入接入名称" maxLength={50} />
          </Form.Item>
          <Form.Item name="description" label="描述">
            <TextArea placeholder="请输入描述" rows={3} maxLength={200} />
          </Form.Item>
          <Form.Item>
            <Space>
              <Button type="primary" htmlType="submit" loading={editLoading}>
                保存
              </Button>
              <Button onClick={() => setEditModalOpen(false)}>取消</Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 重置密钥弹窗 */}
      <Modal
        title="重置密钥"
        open={resetKeyModalOpen}
        onCancel={() => {
          setResetKeyModalOpen(false);
          setNewKey(null);
        }}
        footer={
          newKey ? (
            <Button
              type="primary"
              onClick={() => {
                setResetKeyModalOpen(false);
                setNewKey(null);
              }}
            >
              我已保存新密钥
            </Button>
          ) : (
            <Space>
              <Button onClick={() => setResetKeyModalOpen(false)}>取消</Button>
              <Button type="primary" danger loading={resetLoading} onClick={handleResetKey}>
                确认重置
              </Button>
            </Space>
          )
        }
        destroyOnClose
        maskClosable={false}
      >
        {newKey ? (
          <Descriptions column={1} bordered>
            <Descriptions.Item label="新密钥">
              <Space direction="vertical" style={{ width: "100%" }}>
                <Paragraph
                  copyable={{
                    text: newKey,
                    icon: <CopyOutlined />,
                    tooltips: ["复制", "已复制"],
                  }}
                  style={{ marginBottom: 0 }}
                >
                  <Text code style={{ wordBreak: "break-all" }}>{newKey}</Text>
                </Paragraph>
                <Text type="danger">
                  请妥善保存此密钥，关闭后将无法再次查看完整密钥！
                </Text>
              </Space>
            </Descriptions.Item>
          </Descriptions>
        ) : (
          <div>
            <Text type="warning">
              <ExclamationCircleOutlined style={{ marginRight: 8 }} />
              重置密钥后，原密钥将立即失效，使用原密钥的所有接入都将无法访问。
            </Text>
          </div>
        )}
      </Modal>
    </>
  );
}
