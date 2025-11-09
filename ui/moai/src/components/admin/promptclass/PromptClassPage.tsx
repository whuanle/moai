import React, { useState, useEffect } from "react";
import {
  Table,
  Button,
  message,
  Modal,
  Form,
  Input,
  Space,
  Typography,
  Popconfirm,
} from "antd";
import { ReloadOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import {
  PromptClassifyItem,
  CreatePromptClassifyCommand,
  UpdatePromptClassifyCommand,
} from "../../../apiClient/models";
import { proxyFormRequestError, proxyRequestError } from "../../../helper/RequestError";

const { Title, Text } = Typography;

export default function PromptClassPage() {
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
      proxyRequestError(error, messageApi,"获取分类列表失败");
    } finally {
      setLoading(false);
    }
  };

  // 处理新增按钮点击
  const handleAddClick = () => {
    form.resetFields();
    form.setFieldsValue({
      name: "",
      description: "",
    });
    setAddModalVisible(true);
  };

  // 处理编辑按钮点击
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

  // 处理删除按钮点击
  const handleDeleteClick = async (item: PromptClassifyItem) => {
    try {
      const client = GetApiClient();
      await client.api.admin_prompt.delete_class.delete({
        promptId: item.classifyId!,
      });
      messageApi.success("分类删除成功");

      // 刷新分类列表
      await fetchClassList();
    } catch (error) {
      console.error("删除分类失败:", error);
      messageApi.error("删除分类失败");
    }
  };

  // 处理新增分类提交
  const handleAddSubmit = async (values: CreatePromptClassifyCommand) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();
      await client.api.admin_prompt.create_class.post(values);

      messageApi.success("分类添加成功");
      setAddModalVisible(false);

      // 刷新分类列表
      await fetchClassList();
    } catch (error) {
      console.error("添加分类失败:", error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setSubmitting(false);
    }
  };

  // 处理编辑分类提交
  const handleEditSubmit = async (values: any) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();

      // 构建请求参数
      const requestBody: UpdatePromptClassifyCommand = {
        classifyId: values.classifyId,
        name: values.name,
        description: values.description,
      };

      await client.api.admin_prompt.update_class.post(requestBody);

      messageApi.success("分类更新成功");
      setEditModalVisible(false);

      // 刷新分类列表
      await fetchClassList();
    } catch (error) {
      console.error("更新分类失败:", error);
      proxyFormRequestError(error, messageApi, editForm);
    } finally {
      setSubmitting(false);
    }
  };

  useEffect(() => {
    fetchClassList();
  }, []);

  // 分类列表表格列定义
  const classColumns = [
    {
      title: "分类ID",
      dataIndex: "classifyId",
      key: "classifyId",
      width: 100,
      render: (id: number) => <Text strong>{id}</Text>,
    },
    {
      title: "分类名称",
      dataIndex: "name",
      key: "name",
      render: (text: string) => <Text strong>{text}</Text>,
    },
    {
      title: "分类描述",
      dataIndex: "description",
      key: "description",
      ellipsis: true,
    },
    {
      title: "操作",
      key: "action",
      width: 150,
      render: (_: any, record: PromptClassifyItem) => (
        <Space size="small">
          <Button
            type="link"
            size="small"
            onClick={() => handleEditClick(record)}
          >
            修改
          </Button>
          <Popconfirm
            title="确定要删除这个分类吗？"
            onConfirm={() => handleDeleteClick(record)}
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
    <>
      {contextHolder}
      <div style={{ paddingLeft: "24px", paddingRight: "24px" }}>
        <Title level={2}>提示词分类管理</Title>

        <div style={{ marginBottom: "16px", textAlign: "right" }}>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={() => fetchClassList()}>
              刷新
            </Button>
            <Button type="primary" onClick={() => handleAddClick()}>
              新增分类
            </Button>
          </Space>
        </div>

        <Table
          dataSource={classList}
          columns={classColumns}
          rowKey={(record) => record.classifyId?.toString() || ""}
          pagination={{ pageSize: 20 }}
          loading={loading}
        />

        {/* 新增分类模态窗口 */}
        <Modal
          title="新增分类"
          open={addModalVisible}
          onCancel={() => setAddModalVisible(false)}
          footer={null}
          width={600}
          maskClosable={false}
          keyboard={false}
        >
          <Form form={form} layout="vertical" onFinish={handleAddSubmit}>
            <Form.Item
              name="name"
              label="分类名称"
              rules={[{ required: true, message: "请输入分类名称" }]}
            >
              <Input placeholder="请输入分类名称" />
            </Form.Item>

            <Form.Item
              name="description"
              label="分类描述"
              rules={[{ required: true, message: "请输入分类描述" }]}
            >
              <Input.TextArea
                placeholder="请输入分类描述"
                rows={4}
              />
            </Form.Item>

            <Form.Item style={{ marginTop: "24px", textAlign: "right" }}>
              <Space>
                <Button onClick={() => setAddModalVisible(false)}>取消</Button>
                <Button type="primary" htmlType="submit" loading={submitting}>
                  确定
                </Button>
              </Space>
            </Form.Item>
          </Form>
        </Modal>

        {/* 编辑分类模态窗口 */}
        <Modal
          title="编辑分类"
          open={editModalVisible}
          onCancel={() => setEditModalVisible(false)}
          footer={null}
          width={600}
          maskClosable={false}
          keyboard={false}
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
              <Input placeholder="请输入分类名称" />
            </Form.Item>

            <Form.Item
              name="description"
              label="分类描述"
              rules={[{ required: true, message: "请输入分类描述" }]}
            >
              <Input.TextArea
                placeholder="请输入分类描述"
                rows={4}
              />
            </Form.Item>

            <Form.Item style={{ marginTop: "24px", textAlign: "right" }}>
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
    </>
  );
}

