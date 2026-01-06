import { useState, useEffect, useCallback, useMemo } from "react";
import { Table, Button, message, Modal, Form, Input, Space, Typography, Popconfirm, Tooltip } from "antd";
import { ReloadOutlined, PlusOutlined, EditOutlined, DeleteOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import type { PluginClassifyItem, CreatePluginClassifyCommand, UpdatePluginClassifyCommand } from "../../../../apiClient/models";
import { proxyFormRequestError, proxyRequestError } from "../../../../helper/RequestError";
import "./PluginClassPage.css";

export default function PluginClassPage() {
  const [classList, setClassList] = useState<PluginClassifyItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [modalState, setModalState] = useState<{ type: "add" | "edit" | null; data?: PluginClassifyItem }>({ type: null });
  const [form] = Form.useForm();
  const [submitting, setSubmitting] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  // 获取分类列表
  const fetchClassList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      setClassList(response?.items || []);
    } catch (error) {
      proxyRequestError(error, messageApi, "获取分类列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 打开新增模态框
  const openAddModal = useCallback(() => {
    form.resetFields();
    setModalState({ type: "add" });
  }, [form]);

  // 打开编辑模态框
  const openEditModal = useCallback((item: PluginClassifyItem) => {
    form.setFieldsValue({
      classifyId: item.classifyId,
      name: item.name,
      description: item.description,
    });
    setModalState({ type: "edit", data: item });
  }, [form]);

  // 关闭模态框
  const closeModal = useCallback(() => {
    setModalState({ type: null });
    form.resetFields();
  }, [form]);

  // 删除分类
  const handleDelete = useCallback(async (item: PluginClassifyItem) => {
    try {
      const client = GetApiClient();
      await client.api.admin.pluginclassify.delete_classify.post({ classifyId: item.classifyId! });
      messageApi.success("分类删除成功");
      fetchClassList();
    } catch (error) {
      proxyRequestError(error, messageApi, "删除分类失败");
    }
  }, [messageApi, fetchClassList]);

  // 提交表单
  const handleSubmit = useCallback(async (values: CreatePluginClassifyCommand & { classifyId?: number }) => {
    setSubmitting(true);
    try {
      const client = GetApiClient();
      if (modalState.type === "add") {
        await client.api.admin.pluginclassify.add_classify.post({ name: values.name, description: values.description });
        messageApi.success("分类添加成功");
      } else {
        const requestBody: UpdatePluginClassifyCommand = {
          classifyId: values.classifyId!,
          name: values.name,
          description: values.description,
        };
        await client.api.admin.pluginclassify.update_classify.delete(requestBody);
        messageApi.success("分类更新成功");
      }
      closeModal();
      fetchClassList();
    } catch (error) {
      proxyFormRequestError(error, messageApi, form, modalState.type === "add" ? "添加分类失败" : "更新分类失败");
    } finally {
      setSubmitting(false);
    }
  }, [modalState.type, messageApi, form, closeModal, fetchClassList]);

  useEffect(() => {
    fetchClassList();
  }, []);

  // 表格列定义
  const columns = useMemo(() => [
    {
      title: "分类ID",
      dataIndex: "classifyId",
      key: "classifyId",
      width: 100,
      render: (id: number) => <Typography.Text className="classify-id">{id}</Typography.Text>,
    },
    {
      title: "分类名称",
      dataIndex: "name",
      key: "name",
      width: 200,
      render: (text: string) => <Typography.Text strong>{text}</Typography.Text>,
    },
    {
      title: "分类描述",
      dataIndex: "description",
      key: "description",
      ellipsis: true,
      render: (desc: string) => <Typography.Text type="secondary">{desc || "-"}</Typography.Text>,
    },
    {
      title: "操作",
      key: "action",
      width: 160,
      render: (_: unknown, record: PluginClassifyItem) => (
        <Space size={4}>
          <Tooltip title="编辑">
            <Button type="link" size="small" icon={<EditOutlined />} onClick={() => openEditModal(record)}>
              编辑
            </Button>
          </Tooltip>
          <Popconfirm
            title="删除分类"
            description="确定要删除这个分类吗？"
            onConfirm={() => handleDelete(record)}
            okText="确定"
            cancelText="取消"
          >
            <Tooltip title="删除">
              <Button type="link" size="small" danger icon={<DeleteOutlined />}>
                删除
              </Button>
            </Tooltip>
          </Popconfirm>
        </Space>
      ),
    },
  ], [openEditModal, handleDelete]);

  return (
    <>
      {contextHolder}
      <div className="plugin-classify-page">
        <div className="plugin-classify-header">
          <div className="plugin-classify-title">
            <Typography.Title level={4}>插件分类管理</Typography.Title>
            <Typography.Text type="secondary">管理插件的分类信息，便于插件归类和查找</Typography.Text>
          </div>
          <Space>
            <Button icon={<ReloadOutlined />} onClick={fetchClassList} loading={loading}>
              刷新
            </Button>
            <Button type="primary" icon={<PlusOutlined />} onClick={openAddModal}>
              新增分类
            </Button>
          </Space>
        </div>

        <div className="plugin-classify-table">
          <Table
            dataSource={classList}
            columns={columns}
            rowKey={(record) => record.classifyId?.toString() || ""}
            pagination={false}
            loading={loading}
          />
        </div>

        {/* 新增/编辑分类模态窗口 */}
        <Modal
          title={modalState.type === "add" ? "新增分类" : "编辑分类"}
          open={modalState.type !== null}
          onCancel={closeModal}
          footer={null}
          width={520}
          maskClosable={false}
          destroyOnClose
        >
          <Form form={form} layout="vertical" onFinish={handleSubmit} className="classify-form">
            <Form.Item name="classifyId" hidden>
              <Input />
            </Form.Item>
            <Form.Item
              name="name"
              label="分类名称"
              rules={[{ required: true, message: "请输入分类名称" }]}
            >
              <Input placeholder="请输入分类名称" maxLength={50} showCount />
            </Form.Item>
            <Form.Item
              name="description"
              label="分类描述"
              rules={[{ required: true, message: "请输入分类描述" }]}
            >
              <Input.TextArea placeholder="请输入分类描述" rows={4} maxLength={200} showCount />
            </Form.Item>
            <Form.Item className="classify-form-actions">
              <Space>
                <Button onClick={closeModal}>取消</Button>
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

