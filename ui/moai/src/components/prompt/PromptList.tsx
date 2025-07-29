import React, { useEffect, useState, useCallback } from "react";
import { useNavigate } from "react-router";
import {
  Button,
  Card,
  Space,
  message,
  Tag,
  Checkbox,
  Row,
  Col,
  Typography,
  Popconfirm,
  Tooltip,
  Empty,
  Layout,
  Avatar,
  Badge,
  Spin
} from "antd";
import {
  PlusOutlined,
  EditOutlined,
  DeleteOutlined,
  SettingOutlined,
  EyeOutlined,
  CopyOutlined,
  UserOutlined,
  CalendarOutlined,
  ReloadOutlined,
  LockOutlined,
  GlobalOutlined,
  FileTextOutlined,
  FolderOutlined
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { formatDateTime } from "../../helper/DateTimeHelper";

const { Title, Text, Paragraph } = Typography;
const { Content } = Layout;

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

// 提示词类型
interface PromptItem {
  id: number;
  name: string;
  description?: string;
  content?: string;
  createTime?: string;
  createUserName?: string;
  createUserId?: number;
  updateTime?: string;
  updateUserName?: string;
  isOwn?: boolean;
  isPublic?: boolean;
  promptClassId?: number;
}

// 权限检查 Hook
const usePermissionCheck = (currentUserId: number | null, isAdmin: boolean) => {
  const canEditPrompt = useCallback((prompt: PromptItem) => {
    if (!prompt || !currentUserId || !prompt.createUserId) return false;
    return currentUserId === prompt.createUserId;
  }, [currentUserId]);

  const canDeletePrompt = useCallback((prompt: PromptItem) => {
    if (!prompt || !currentUserId || !prompt.createUserId) return false;
    return currentUserId === prompt.createUserId;
  }, [currentUserId]);

  return { canEditPrompt, canDeletePrompt };
};

// 分类管理 Hook
const useClassManagement = () => {
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchClassList = useCallback(async () => {
    setClassLoading(true);
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
      setClassLoading(false);
    }
  }, [messageApi]);

  return {
    classList,
    classLoading,
    contextHolder,
    fetchClassList
  };
};

// 提示词管理 Hook
const usePromptManagement = () => {
  const [messageApi, contextHolder] = message.useMessage();

  return {
    contextHolder
  };
};

// 提示词查看 Hook
const usePromptView = () => {
  const [messageApi, contextHolder] = message.useMessage();

  const handleCopyContent = (content: string) => {
    navigator.clipboard.writeText(content).then(() => {
      messageApi.success("已复制到剪贴板");
    }).catch(() => {
      messageApi.error("复制失败");
    });
  };

  return {
    contextHolder,
    handleCopyContent
  };
};

export default function PromptList() {
  const navigate = useNavigate();
  // 用户信息
  const [isAdmin, setIsAdmin] = useState(false);
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  const [messageApi, contextHolder] = message.useMessage();

  // 分类相关
  const [selectedClassId, setSelectedClassId] = useState<number | null>(null);
  const [showOwnOnly, setShowOwnOnly] = useState(true);

  // 提示词相关
  const [promptList, setPromptList] = useState<PromptItem[]>([]);
  const [promptLoading, setPromptLoading] = useState(false);

  // 获取当前用户信息
  const fetchUserInfo = useCallback(async () => {
    try {
      const client = GetApiClient();
      const res = await client.api.common.userinfo.get();
      setIsAdmin(res?.isAdmin === true);
      setCurrentUserId(res?.userId || null);
    } catch (e) {
      setIsAdmin(false);
      setCurrentUserId(null);
    }
  }, []);

  // 根据分类ID获取分类名称
  const getClassNameById = useCallback((classId: number | undefined, classList: PromptClass[]) => {
    if (!classId) return '未分类';
    const classItem = classList.find(cls => cls.id === classId);
    return classItem ? classItem.name : '未分类';
  }, []);

  // 获取提示词列表
  const fetchPromptList = useCallback(async (classId?: number | null) => {
    setPromptLoading(true);
    try {
      const client = GetApiClient();
      const req: any = {
        isOwn: showOwnOnly
      };
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
            createUserId: item.createUserId || 0,
            updateTime: item.updateTime || '',
            updateUserName: item.updateUserName || '',
            isOwn: item.isOwn || false,
            isPublic: item.isPublic || false,
            promptClassId: item.promptClassId || 0,
          }))
      );
    } catch (e) {
      messageApi.error("获取提示词失败");
    } finally {
      setPromptLoading(false);
    }
  }, [showOwnOnly, messageApi]);

  // 分类管理
  const classManagement = useClassManagement();

  // 提示词查看
  const promptView = usePromptView();

  // 权限检查
  const { canEditPrompt, canDeletePrompt } = usePermissionCheck(currentUserId, isAdmin);

  // 页面初始化
  useEffect(() => {
    fetchUserInfo();
    classManagement.fetchClassList();
  }, [fetchUserInfo, classManagement.fetchClassList]);

  useEffect(() => {
    fetchPromptList(selectedClassId);
  }, [fetchPromptList, selectedClassId]);

  // 切换分类
  const handleClassSelect = (id: number | null) => {
    setSelectedClassId(id);
  };

  // 切换"我创建的"筛选
  const handleOwnFilterChange = (checked: boolean) => {
    setShowOwnOnly(checked);
  };

  // 刷新数据
  const handleRefresh = async () => {
    await Promise.all([
      classManagement.fetchClassList(),
      fetchPromptList(selectedClassId)
    ]);
  };

  // 删除提示词
  const handleDeletePrompt = async (prompt: PromptItem) => {
    try {
      const client = GetApiClient();
      await client.api.prompt.delete_prompt.delete({ promptId: prompt.id });
      messageApi.success("删除成功");
      fetchPromptList(selectedClassId);
    } catch (e) {
      console.error("删除失败:", e);
      messageApi.error("删除失败");
    }
  };









  // 渲染提示词卡片
  const renderPromptCard = (prompt: PromptItem) => (
    <Col xs={24} sm={12} md={8} lg={6} key={prompt.id}>
      <Card
        hoverable
        style={{ height: '100%', cursor: 'pointer', position: 'relative' }}
        onClick={() => navigate(`/app/prompt/${prompt.id}/content`)}
        actions={[
          <Tooltip title="查看详情">
            <EyeOutlined onClick={(e) => {
              e.stopPropagation();
              navigate(`/app/prompt/${prompt.id}/content`);
            }} />
          </Tooltip>,
          <Tooltip title="查看内容">
            <FileTextOutlined onClick={(e) => {
              e.stopPropagation();
              navigate(`/app/prompt/${prompt.id}/content`);
            }} />
          </Tooltip>,
          <Tooltip title={canEditPrompt(prompt) ? "编辑" : "无编辑权限"}>
            <EditOutlined 
              onClick={(e) => {
                e.stopPropagation();
                if (canEditPrompt(prompt)) {
                  navigate(`/app/prompt/${prompt.id}/edit`);
                } else {
                  messageApi.warning("您没有编辑此提示词的权限");
                }
              }}
              style={{ 
                color: canEditPrompt(prompt) ? undefined : '#d9d9d9',
                cursor: canEditPrompt(prompt) ? 'pointer' : 'not-allowed'
              }}
            />
          </Tooltip>,
          <Tooltip title="复制内容">
            <CopyOutlined onClick={async (e) => {
              e.stopPropagation();
              try {
                const client = GetApiClient();
                const res = await client.api.prompt.prompt_content.get({
                  queryParameters: { promptId: prompt.id }
                });
                if (res?.content) {
                  promptView.handleCopyContent(res.content);
                } else {
                  promptView.handleCopyContent(prompt.content || '');
                }
              } catch (e) {
                promptView.handleCopyContent(prompt.content || '');
              }
            }} />
          </Tooltip>,
          <Tooltip title={canDeletePrompt(prompt) ? "删除" : "无删除权限"}>
            <DeleteOutlined
              onClick={async (e) => {
                e.stopPropagation();
                if (canDeletePrompt(prompt)) {
                  await handleDeletePrompt(prompt);
                }
              }}
              style={{ 
                color: canDeletePrompt(prompt) ? '#ff4d4f' : '#d9d9d9',
                cursor: canDeletePrompt(prompt) ? 'pointer' : 'not-allowed'
              }}
            />
          </Tooltip>
        ]}

      >
        <Card.Meta
          title={
            <Space>
              <Text strong ellipsis style={{ flex: 1 }}>
                {prompt.name}
              </Text>
              {prompt.isOwn && (
                <Badge status="processing" text="我的" />
              )}
              {prompt.isPublic ? (
                <Tag color="green" icon={<GlobalOutlined />} style={{ fontSize: '12px' }}>公开</Tag>
              ) : (
                <Tag color="orange" icon={<LockOutlined />} style={{ fontSize: '12px' }}>私密</Tag>
              )}
            </Space>
          }
          description={
            <Space direction="vertical" size="small" style={{ width: '100%' }}>
              {prompt.description && (
                <Paragraph
                  ellipsis={{ rows: 2 }}
                  style={{ marginBottom: 0, fontSize: '12px' }}
                >
                  {prompt.description}
                </Paragraph>
              )}
              <Space size="small">
                <FolderOutlined style={{ fontSize: '12px', color: '#1890ff' }} />
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  {getClassNameById(prompt.promptClassId, classManagement.classList)}
                </Text>
              </Space>
              <Space size="small">
                <Avatar size="small" icon={<UserOutlined />} />
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  {prompt.createUserName}
                </Text>
              </Space>
              <Space size="small">
                <CalendarOutlined style={{ fontSize: '12px', color: '#999' }} />
                <Text type="secondary" style={{ fontSize: '12px' }}>
                  {formatDateTime(prompt.createTime)}
                </Text>
              </Space>
            </Space>
          }
        />
      </Card>
    </Col>
  );

  return (
    <>
      {contextHolder}
      {classManagement.contextHolder}
      {promptView.contextHolder}
      
      <Layout style={{ background: '#f5f5f5', minHeight: '100vh' }}>
        <Content>
          <Card style={{ margin: '16px 24px 0' }}>
            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
              <Title level={4} style={{ margin: 0 }}>提示词管理</Title>
              <Space>
                <Checkbox
                  checked={showOwnOnly}
                  onChange={(e) => handleOwnFilterChange(e.target.checked)}
                >
                  我创建的
                </Checkbox>
                <Button 
                  icon={<ReloadOutlined />} 
                  onClick={handleRefresh}
                  loading={classManagement.classLoading || promptLoading}
                  title="刷新"
                >
                  刷新
                </Button>
                {isAdmin && (
                  <Button icon={<SettingOutlined />} onClick={() => navigate("/app/prompt/class")}>
                    分类管理
                  </Button>
                )}
                <Button type="primary" icon={<PlusOutlined />} onClick={() => navigate("/app/prompt/create")}>
                  新增提示词
                </Button>
              </Space>
            </div>
          </Card>
          
          <div style={{ padding: '0 24px 24px' }}>
            <Card>
              <Space wrap style={{ marginBottom: 16 }}>
                <Button
                  type={!selectedClassId ? "primary" : "default"}
                  onClick={() => handleClassSelect(null)}
                  loading={classManagement.classLoading}
                >
                  全部
                </Button>
                {classManagement.classList.map((cls) => (
                  <Button
                    key={cls.id}
                    type={selectedClassId === cls.id ? "primary" : "default"}
                    onClick={() => handleClassSelect(cls.id)}
                    loading={classManagement.classLoading}
                  >
                    {cls.name}
                  </Button>
                ))}
              </Space>
              
              <Row gutter={[16, 16]}>
                {promptLoading ? (
                  <Col span={24}>
                    <div style={{ textAlign: 'center', padding: '40px' }}>
                      <Spin size="large" />
                    </div>
                  </Col>
                ) : promptList.length > 0 ? (
                  promptList.map(renderPromptCard)
                ) : (
                  <Col span={24}>
                    <Empty
                      description="暂无提示词"
                      style={{ margin: '40px 0' }}
                    >
                      <Button 
                        type="primary" 
                        icon={<PlusOutlined />} 
                        onClick={() => navigate("/app/prompt/create")}
                        disabled={classManagement.classList.length === 0}
                      >
                        新增提示词
                      </Button>
                    </Empty>
                  </Col>
                )}
              </Row>
            </Card>
          </div>
        </Content>
      </Layout>


    </>
  );
}