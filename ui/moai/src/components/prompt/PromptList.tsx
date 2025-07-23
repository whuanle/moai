import React, { useEffect, useState, useCallback } from "react";
import { Button, Table, Modal, Form, Input, Space, message, Tag } from "antd";
import { PlusOutlined, EditOutlined, DeleteOutlined, SettingOutlined } from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";

// 分类类型
interface PromptClass {
  id: number;
  name: string;
  description?: string;
}
// 提示词类型
interface PromptItem {
  id: number;
  name: string;
  description?: string;
  content?: string;
  createTime?: string;
  createUserName?: string;
  updateTime?: string;
  updateUserName?: string;
}

export default function PromptList() {
  // 分类相关
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);
  const [selectedClassId, setSelectedClassId] = useState<number | null>(null);
  // 管理员相关
  const [isAdmin, setIsAdmin] = useState(false);
  // 分类管理弹窗
  const [classModalOpen, setClassModalOpen] = useState(false);
  const [editingClass, setEditingClass] = useState<PromptClass | null>(null);
  const [classForm] = Form.useForm();
  const [classModalLoading, setClassModalLoading] = useState(false);
  // 提示词相关
  const [promptList, setPromptList] = useState<PromptItem[]>([]);
  const [promptLoading, setPromptLoading] = useState(false);

  // 获取分类列表
  const fetchClassList = useCallback(async () => {
    setClassLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.class_list.get();
      // 类型兼容：过滤掉无效id并转换为number
      setClassList(
        (res?.items || [])
          .filter((item: any) => typeof item.id === 'number' && item.id != null)
          .map((item: any) => ({
            id: item.id ?? 0,
            name: item.name || '',
            description: item.description || '',
          }))
      );
    } catch (e) {
      message.error("获取分类失败");
    } finally {
      setClassLoading(false);
    }
  }, []);

  // 获取当前用户信息
  const fetchUserInfo = useCallback(async () => {
    try {
      const client = GetApiClient();
      const res = await client.api.common.userinfo.get();
      setIsAdmin(res?.isAdmin === true);
    } catch (e) {
      setIsAdmin(false);
    }
  }, []);

  // 获取提示词列表
  const fetchPromptList = useCallback(async (classId?: number | null) => {
    setPromptLoading(true);
    try {
      const client = GetApiClient();
      const req: any = {};
      if (classId) req.classId = classId;
      const res = await client.api.prompt.prompt_list.post(req);
      setPromptList(
        (res?.items || [])
          .filter((item: any) => typeof item.id === 'number' && item.id != null)
          .map((item: any) => ({
            id: item.id ?? 0,
            name: item.name || '',
            description: item.description || '',
            content: item.content || '',
            createTime: item.createTime || '',
            createUserName: item.createUserName || '',
            updateTime: item.updateTime || '',
            updateUserName: item.updateUserName || '',
          }))
      );
    } catch (e) {
      message.error("获取提示词失败");
    } finally {
      setPromptLoading(false);
    }
  }, []);

  // 页面初始化
  useEffect(() => {
    fetchUserInfo();
    fetchClassList();
    fetchPromptList();
  }, [fetchUserInfo, fetchClassList, fetchPromptList]);

  // 切换分类
  const handleClassSelect = (id: number | null) => {
    setSelectedClassId(id);
    fetchPromptList(id);
  };

  // 分类管理弹窗相关
  const openClassModal = () => {
    setEditingClass(null);
    setClassModalOpen(true);
    setTimeout(() => {
      classForm.setFieldsValue({ name: '', description: '' });
    }, 0);
  };
  const handleEditClass = (cls: PromptClass) => {
    setEditingClass(cls);
    classForm.setFieldsValue(cls);
    setClassModalOpen(true);
  };
  const handleDeleteClass = async (cls: PromptClass) => {
    Modal.confirm({
      title: `确定删除分类“${cls.name}”吗？`,
      onOk: async () => {
        setClassModalLoading(true);
        try {
          const client = GetApiClient();
          // delete_class 需要 promptId 字段
          await client.api.prompt.delete_class.delete({ promptId: cls.id });
          message.success("删除成功");
          fetchClassList();
          if (selectedClassId === cls.id) {
            setSelectedClassId(null);
            fetchPromptList();
          }
        } catch (e) {
          message.error("删除失败");
        } finally {
          setClassModalLoading(false);
        }
      },
    });
  };
  const handleClassModalOk = async () => {
    try {
      await classForm.validateFields();
      setClassModalLoading(true);
      const values = classForm.getFieldsValue();
      const client = GetApiClient();
      if (editingClass) {
        // 修改
        await client.api.prompt.update_class.post({ ...values, id: editingClass.id });
        message.success("修改成功");
      } else {
        // 新增
        await client.api.prompt.create_class.post(values);
        message.success("新增成功");
      }
      setClassModalOpen(false);
      fetchClassList();
    } catch (e) {
      // 校验失败或接口异常
    } finally {
      setClassModalLoading(false);
    }
  };

  // 分类管理弹窗内容
  const renderClassModal = () => (
    <Modal
      open={classModalOpen}
      title={editingClass ? "编辑分类" : "新增分类"}
      onCancel={() => setClassModalOpen(false)}
      onOk={handleClassModalOk}
      confirmLoading={classModalLoading}
      footer={null}
      destroyOnClose
    >
      <Form
        form={classForm}
        layout="vertical"
        initialValues={editingClass || { name: '', description: '' }}
        key={editingClass ? editingClass.id : 'new'}
      >
        <Form.Item name="name" label="分类名称" rules={[{ required: true, message: "请输入分类名称" }]}> <Input /> </Form.Item>
        <Form.Item name="description" label="描述"> <Input.TextArea rows={2} /> </Form.Item>
        <Form.Item>
          <Space>
            <Button type="primary" onClick={handleClassModalOk} loading={classModalLoading}>保存</Button>
            <Button onClick={() => setClassModalOpen(false)}>取消</Button>
          </Space>
        </Form.Item>
      </Form>
      {/* 分类列表（管理模式） */}
      {!editingClass && (
        <div style={{ marginTop: 24 }}>
          <div style={{ fontWeight: 500, marginBottom: 8 }}>所有分类</div>
          {classList.map((cls) => (
            <div key={cls.id} style={{ display: "flex", alignItems: "center", marginBottom: 8 }}>
              <div style={{ flex: 1 }}>
                <b>{cls.name}</b>
                <span style={{ color: '#888', marginLeft: 8 }}>{cls.description}</span>
              </div>
              <Button icon={<EditOutlined />} size="small" onClick={() => handleEditClass(cls)} style={{ marginRight: 8 }} />
              <Button icon={<DeleteOutlined />} size="small" danger onClick={() => handleDeleteClass(cls)} />
            </div>
          ))}
          <Button type="dashed" block icon={<PlusOutlined />} style={{ marginTop: 12 }} onClick={openClassModal}>新增分类</Button>
        </div>
      )}
    </Modal>
  );

  // 分类头部渲染
  const renderClassHeader = () => (
    <div style={{ display: "flex", alignItems: "center", marginBottom: 24 }}>
      <div style={{ flex: 1 }}>
        <Space>
          <Button
            type={!selectedClassId ? "primary" : "default"}
            onClick={() => handleClassSelect(null)}
            loading={classLoading}
          >全部</Button>
          {classList.map((cls) => (
            <Button
              key={cls.id}
              type={selectedClassId === cls.id ? "primary" : "default"}
              onClick={() => handleClassSelect(cls.id)}
              loading={classLoading}
            >{cls.name}</Button>
          ))}
        </Space>
      </div>
      {isAdmin && (
        <Button icon={<SettingOutlined />} onClick={openClassModal} style={{ marginLeft: 16 }}>分类管理</Button>
      )}
      {renderClassModal()}
    </div>
  );

  // 提示词表格列
  const columns = [
    { title: "名称", dataIndex: "name", key: "name", width: 180 },
    { title: "描述", dataIndex: "description", key: "description", width: 220 },
    { title: "内容", dataIndex: "content", key: "content", ellipsis: true },
    { title: "创建人", dataIndex: "createUserName", key: "createUserName", width: 120 },
    { title: "创建时间", dataIndex: "createTime", key: "createTime", width: 160 },
    { title: "更新人", dataIndex: "updateUserName", key: "updateUserName", width: 120 },
    { title: "更新时间", dataIndex: "updateTime", key: "updateTime", width: 160 },
  ];

  return (
    <div>
      {renderClassHeader()}
      <Table
        rowKey="id"
        columns={columns}
        dataSource={promptList}
        loading={promptLoading}
        bordered
        pagination={{ pageSize: 20 }}
      />
    </div>
  );
}