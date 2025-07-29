import React, { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router";
import {
  Card,
  Typography,
  Space,
  Button,
  message,
  Spin,
  List,
  Input,
  Popconfirm,
  Empty,
} from "antd";
import {
  ArrowLeftOutlined,
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  CheckOutlined,
  CloseOutlined,
  ReloadOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";

const { Title, Text } = Typography;

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

export default function PromptClassManage() {
  const navigate = useNavigate();
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 编辑相关状态
  const [editingClassId, setEditingClassId] = useState<number | null>(null);
  const [newClassName, setNewClassName] = useState('');
  const [editingClassName, setEditingClassName] = useState('');
  const [operationLoading, setOperationLoading] = useState(false);

  // 获取分类列表
  const fetchClassList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.class_list.get();
      setClassList(
        (res?.items || [])
          .filter((item: any) => typeof item.id === 'number' && item.id != null)
          .map((item: any) => ({
            id: item.id ?? 0,
            name: item.name || '',
          }))
      );
    } catch (e) {
      messageApi.error("获取分类失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 创建分类
  const handleCreateClass = async () => {
    if (!newClassName.trim()) {
      messageApi.error("请输入分类名称");
      return;
    }
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      await client.api.prompt.create_class.post({ name: newClassName.trim() });
      messageApi.success("创建成功");
      setNewClassName('');
      fetchClassList();
    } catch (e) {
      messageApi.error("创建失败");
    } finally {
      setOperationLoading(false);
    }
  };

  // 更新分类
  const handleUpdateClass = async () => {
    if (!editingClassName.trim()) {
      messageApi.error("请输入分类名称");
      return;
    }
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      await client.api.prompt.update_class.post({ 
        id: editingClassId, 
        name: editingClassName.trim() 
      });
      messageApi.success("修改成功");
      setEditingClassId(null);
      setEditingClassName('');
      fetchClassList();
    } catch (e) {
      messageApi.error("修改失败");
    } finally {
      setOperationLoading(false);
    }
  };

  // 删除分类
  const handleDeleteClass = async (cls: PromptClass) => {
    setOperationLoading(true);
    try {
      const client = GetApiClient();
      await client.api.prompt.delete_class.delete({ promptId: cls.id });
      messageApi.success("删除成功");
      fetchClassList();
    } catch (e) {
      messageApi.error("删除失败");
    } finally {
      setOperationLoading(false);
    }
  };

  // 开始编辑
  const handleStartEdit = (cls: PromptClass) => {
    setEditingClassId(cls.id);
    setEditingClassName(cls.name);
  };

  // 取消编辑
  const handleCancelEdit = () => {
    setEditingClassId(null);
    setEditingClassName('');
  };

  // 返回列表
  const handleBack = () => {
    navigate("/app/prompt/list");
  };

  useEffect(() => {
    fetchClassList();
  }, [fetchClassList]);

  return (
    <>
      {contextHolder}
      <Card>
        <Card.Meta
          title={
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Title level={4} style={{ margin: 0 }}>分类管理</Title>
              <Space>
                <Button 
                  icon={<ReloadOutlined />} 
                  onClick={fetchClassList}
                  loading={loading}
                  title="刷新"
                >
                  刷新
                </Button>
                <Button icon={<ArrowLeftOutlined />} onClick={handleBack}>
                  返回列表
                </Button>
              </Space>
            </Space>
          }
        />
        
        <div style={{ marginTop: 24 }}>
          {/* 新增分类 */}
          <Card size="small" style={{ marginBottom: 16 }}>
            <Space.Compact style={{ width: '100%' }}>
              <Input
                placeholder="输入分类名称"
                value={newClassName}
                onChange={(e) => setNewClassName(e.target.value)}
                onPressEnter={handleCreateClass}
                style={{ flex: 1 }}
              />
              <Button
                type="primary"
                icon={<CheckOutlined />}
                onClick={handleCreateClass}
                loading={operationLoading}
              >
                创建
              </Button>
              <Button
                icon={<CloseOutlined />}
                onClick={() => setNewClassName('')}
                disabled={operationLoading}
              >
                清空
              </Button>
            </Space.Compact>
          </Card>

          {/* 分类列表 */}
          {loading ? (
            <div style={{ textAlign: 'center', padding: '40px' }}>
              <Spin size="large" />
            </div>
          ) : classList.length > 0 ? (
            <List
              size="small"
              dataSource={classList}
              renderItem={(item) => {
                const isEditing = editingClassId === item.id;
                return (
                  <List.Item
                    actions={[
                      isEditing ? (
                        <Space key="edit-actions">
                          <Button
                            type="primary"
                            size="small"
                            icon={<CheckOutlined />}
                            onClick={handleUpdateClass}
                            loading={operationLoading}
                          >
                            保存
                          </Button>
                          <Button
                            size="small"
                            icon={<CloseOutlined />}
                            onClick={handleCancelEdit}
                            disabled={operationLoading}
                          >
                            取消
                          </Button>
                        </Space>
                      ) : (
                        <Space key="view-actions">
                          <Button
                            size="small"
                            icon={<EditOutlined />}
                            onClick={() => handleStartEdit(item)}
                            disabled={operationLoading}
                          >
                            编辑
                          </Button>
                          <Popconfirm
                            title={`确定删除分类"${item.name}"吗？`}
                            description="删除分类后，该分类下的提示词将变为未分类状态"
                            onConfirm={() => handleDeleteClass(item)}
                            okText="确定"
                            cancelText="取消"
                          >
                            <Button
                              size="small"
                              danger
                              icon={<DeleteOutlined />}
                              loading={operationLoading}
                            >
                              删除
                            </Button>
                          </Popconfirm>
                        </Space>
                      )
                    ]}
                  >
                    <List.Item.Meta
                      title={
                        isEditing ? (
                          <Input
                            value={editingClassName}
                            onChange={(e) => setEditingClassName(e.target.value)}
                            onPressEnter={handleUpdateClass}
                            autoFocus
                            style={{ width: '200px' }}
                          />
                        ) : (
                          <Text strong>{item.name}</Text>
                        )
                      }
                    />
                  </List.Item>
                );
              }}
            />
          ) : (
            <Empty
              description="暂无分类"
              style={{ margin: '40px 0' }}
            >
              <Text type="secondary">点击上方输入框创建第一个分类</Text>
            </Empty>
          )}
        </div>
      </Card>
    </>
  );
} 