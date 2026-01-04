import { useState, useEffect } from "react";
import {
  Table,
  Button,
  message,
  Form,
  Input,
  Modal,
  Space,
  Popconfirm,
} from "antd";
import {
  ReloadOutlined,
  PlusOutlined,
  ArrowLeftOutlined,
} from "@ant-design/icons";
import { useNavigate } from "react-router";
import { GetApiClient } from "../ServiceClient";
import {
  PromptClassifyItem,
  CreatePromptClassifyCommand,
  UpdatePromptClassifyCommand,
  DeletePromptClassifyCommand,
} from "../../apiClient/models";
import { proxyFormRequestError, proxyRequestError } from "../../helper/RequestError";

export default function PromptClassPage() {
  const navigate = useNavigate();
  const [classList, setClassList] = useState<PromptClassifyItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [addModalVisible, setAddModalVisible] = useState(false);
  const [editModalVisible, setEditModalVisible] = useState(false);
  const [currentClass, setCurrentClass] = useState<PromptClassifyItem | null>(null);
  const [form] = Form.useForm();
  const [editForm] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 获取分类列表
  const fetchClassList = async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.prompt.class_list.get();
      if (response?.items) {
        setClassList(response.items || []);
      }
    } catch (error) {
      proxyRequestError(error, messageApi, "获取分类列表失败");
    } finally {
      setLoading(false);
    }
  };

  // 打开新增弹窗
  const handleAddClick = () => {
    form.resetFields();
    setAddModalVisible(true);
  };

  // 打开编辑弹窗
  const handleEditClick = (item: PromptClassifyItem) => {
    setCurrentClass(item);
    editForm.resetFields();
    editForm.setFieldsValue({
      classifyId: item.classifyId,
      name: item.name,
      description: item.description,
    });
    setEditModalVisible(true);
  };

  // 新增分类提交
  const handleAddSubmit = async (values: { name: string; description: string }) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();
      const requestBody: CreatePromptClassifyCommand = {
        name: values.name,
        description: values.description,
      };
      await client.api.admin.promptclassify.create_class.post(requestBody);
      messageApi.success("分类添加成功");
      setAddModalVisible(false);
      form.resetFields();
      await fetchClassList();
    } catch (error) {
      console.error("添加分类失败:", error);
      proxyFormRequestError(error, messageApi, form, "添加分类失败");
    } finally {
      setSubmitting(false);
    }
  };

  // 编辑分类提交
  const handleEditSubmit = async (values: { classifyId: number; name: string; description: string }) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();
      const requestBody: UpdatePromptClassifyCommand = {
        classifyId: values.classifyId,
        name: values.name,
        description: values.description,
      };
      await client.api.admin.promptclassify.update_class.post(requestBody);
      messageApi.success("分类更新成功");
      setEditModalVisible(false);
      editForm.resetFields();
      setCurrentClass(null);
      await fetchClassList();
    } catch (error) {
      console.error("更新分类失败:", error);
      proxyFormRequestError(error, messageApi, editForm, "更新分类失败");
    } finally {
      setSubmitting(false);
    }
  };

  // 删除分类
  const handleDelete = async (item: PromptClassifyItem) => {
    try {
      const client = GetApiClient();
      const requestBody: DeletePromptClassifyCommand = {
        classifyId: item.classifyId!,
      };
      await client.api.admin.promptclassify.delete_class.delete(requestBody);
      messageApi.success("分类删除成功");
      await fetchClassList();
    } catch (error) {
      console.error("删除分类失败:", error);
      proxyRequestError(error, messageApi, "删除分类失败");
    }
  };

  useEffect(() => {
    fetchClassList();
  }, []);

  const columns = [
    {
      title: "ID",
      dataIndex: "classifyId",
      key: "classifyId",
      width: 80,
    },
    {
      title: "分类名称",
      dataIndex: "name",
      key: "name",
      width: 200,
    },
    {
      title: "分类描述",
      dataIndex: "description",
      key: "description",
      ellipsis: true,
      render: (text: string) => text || "-",
    },
    {
      title: "操作",
      key: "action",
      width: 150,
      render: (_: unknown, record: PromptClassifyItem) => (
        <Space size="small">
          <Button type="link" size="small" onClick={() => handleEditClick(record)}>
            编辑
          </Button>
          <Popconfirm
            title="确定要删除这个分类吗？"
            description="删除后无法恢复"
            onConfirm={() => handleDelete(record)}
            okText="确定"
            cancelText="取消"
          >
            <Button type="link" danger size="small">
              删除
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div className="page-container">
      {contextHolder}

      <div className="moai-page-header">
        <div>
          <Space align="center">
            <Button icon={<ArrowLeftOutlined />} onClick={() => navigate("/app/prompt/list")}>
              返回
            </Button>
            <h1 className="moai-page-title" style={{ margin: 0 }}>提示词分类管理</h1>
          </Space>
          <p className="moai-page-subtitle">管理和组织你的提示词分类</p>
        </div>
      </div>

      <div className="moai-toolbar">
        <div className="moai-toolbar-left" />
        <div className="moai-toolbar-right">
          <Button icon={<ReloadOutlined />} onClick={fetchClassList} loading={loading}>
            刷新
          </Button>
          <Button type="primary" icon={<PlusOutlined />} onClick={handleAddClick}>
            新增分类
          </Button>
        </div>
      </div>

      <Table
        dataSource={classList}
        columns={columns}
        rowKey={(record) => record.classifyId?.toString() || ""}
        pagination={{ pageSize: 20 }}
        loading={loading}
      />

      {/* 新增分类弹窗 */}
      <Modal
        title="新增分类"
        open={addModalVisible}
        onCancel={() => setAddModalVisible(false)}
        footer={null}
        width={480}
        maskClosable={false}
        keyboard={false}
        destroyOnClose
      >
        <Form form={form} layout="vertical" onFinish={handleAddSubmit}>
          <Form.Item
            name="name"
            label="分类名称"
            rules={[{ required: true, message: "请输入分类名称" }]}
          >
            <Input placeholder="请输入分类名称" maxLength={50} />
          </Form.Item>

          <Form.Item
            name="description"
            label="分类描述"
            rules={[{ required: true, message: "请输入分类描述" }]}
          >
            <Input.TextArea placeholder="请输入分类描述" rows={3} maxLength={200} showCount />
          </Form.Item>

          <Form.Item style={{ marginBottom: 0, marginTop: 24, textAlign: "right" }}>
            <Space>
              <Button onClick={() => setAddModalVisible(false)}>取消</Button>
              <Button type="primary" htmlType="submit" loading={submitting}>
                确定
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>

      {/* 编辑分类弹窗 */}
      <Modal
        title="编辑分类"
        open={editModalVisible}
        onCancel={() => setEditModalVisible(false)}
        footer={null}
        width={480}
        maskClosable={false}
        keyboard={false}
        destroyOnClose
      >
        <Form form={editForm} layout="vertical" onFinish={handleEditSubmit}>
          <Form.Item name="classifyId" hidden>
            <Input />
          </Form.Item>

          <Form.Item
            name="name"
            label="分类名称"
            rules={[{ required: true, message: "请输入分类名称" }]}
          >
            <Input placeholder="请输入分类名称" maxLength={50} />
          </Form.Item>

          <Form.Item
            name="description"
            label="分类描述"
            rules={[{ required: true, message: "请输入分类描述" }]}
          >
            <Input.TextArea placeholder="请输入分类描述" rows={3} maxLength={200} showCount />
          </Form.Item>

          <Form.Item style={{ marginBottom: 0, marginTop: 24, textAlign: "right" }}>
            <Space>
              <Button onClick={() => setEditModalVisible(false)}>取消</Button>
              <Button type="primary" htmlType="submit" loading={submitting}>
                确定
              </Button>
            </Space>
          </Form.Item>
        </Form>
      </Modal>
    </div>
  );
}
