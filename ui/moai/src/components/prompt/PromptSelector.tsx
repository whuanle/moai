import React, { useEffect, useState, useCallback } from "react";
import {
  Modal,
  Input,
  Card,
  Space,
  message,
  Tag,
  Row,
  Col,
  Typography,
  Tooltip,
  Empty,
  Spin,
  Checkbox,
  Button,
  Avatar,
  Badge,
  Divider
} from "antd";
import {
  SearchOutlined,
  UserOutlined,
  CalendarOutlined,
  LockOutlined,
  GlobalOutlined,
  FolderOutlined,
  EyeOutlined,
  CopyOutlined,
  ReloadOutlined
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { formatDateTime } from "../../helper/DateTimeHelper";

const { Title, Text, Paragraph } = Typography;
const { Search } = Input;

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

interface PromptSelectorProps {
  visible: boolean;
  onCancel: () => void;
  onChange: (content: string) => void;
}

export default function PromptSelector({ visible, onCancel, onChange }: PromptSelectorProps) {
  const [messageApi, contextHolder] = message.useMessage();
  
  // 用户信息
  const [isAdmin, setIsAdmin] = useState(false);
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  
  // 分类相关
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [selectedClassId, setSelectedClassId] = useState<number | null>(null);
  const [showOwnOnly, setShowOwnOnly] = useState(false);
  
  // 提示词相关
  const [promptList, setPromptList] = useState<PromptItem[]>([]);
  const [filteredPromptList, setFilteredPromptList] = useState<PromptItem[]>([]);
  const [promptLoading, setPromptLoading] = useState(false);
  const [searchText, setSearchText] = useState("");
  
  // 预览相关
  const [previewVisible, setPreviewVisible] = useState(false);
  const [previewContent, setPreviewContent] = useState("");
  const [previewTitle, setPreviewTitle] = useState("");

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

  // 获取分类列表
  const fetchClassList = useCallback(async () => {
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
    }
  }, [messageApi]);

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
      const prompts = (res?.items || [])
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
        }));
      setPromptList(prompts);
    } catch (e) {
      messageApi.error("获取提示词失败");
    } finally {
      setPromptLoading(false);
    }
  }, [showOwnOnly, messageApi]);

  // 根据分类ID获取分类名称
  const getClassNameById = useCallback((classId: number | undefined, classList: PromptClass[]) => {
    if (!classId) return '未分类';
    const classItem = classList.find(cls => cls.id === classId);
    return classItem ? classItem.name : '未分类';
  }, []);

  // 搜索过滤
  useEffect(() => {
    if (!searchText.trim()) {
      setFilteredPromptList(promptList);
    } else {
      const filtered = promptList.filter(prompt => 
        prompt.name.toLowerCase().includes(searchText.toLowerCase()) ||
        (prompt.description && prompt.description.toLowerCase().includes(searchText.toLowerCase()))
      );
      setFilteredPromptList(filtered);
    }
  }, [promptList, searchText]);

  // 页面初始化
  useEffect(() => {
    if (visible) {
      fetchUserInfo();
      fetchClassList();
      fetchPromptList(selectedClassId);
    }
  }, [visible, fetchUserInfo, fetchClassList, fetchPromptList, selectedClassId]);

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
      fetchClassList(),
      fetchPromptList(selectedClassId)
    ]);
  };

  // 预览提示词内容
  const handlePreviewContent = async (prompt: PromptItem) => {
    try {
      const client = GetApiClient();
      const res = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: prompt.id }
      });
      setPreviewContent(res?.content || prompt.content || '');
      setPreviewTitle(prompt.name);
      setPreviewVisible(true);
    } catch (e) {
      setPreviewContent(prompt.content || '');
      setPreviewTitle(prompt.name);
      setPreviewVisible(true);
    }
  };

  // 选择提示词
  const handleSelectPrompt = (prompt: PromptItem) => {
    onChange(prompt.content || '');
    onCancel();
  };

  // 复制内容
  const handleCopyContent = (content: string) => {
    navigator.clipboard.writeText(content).then(() => {
      messageApi.success("已复制到剪贴板");
    }).catch(() => {
      messageApi.error("复制失败");
    });
  };

  return (
    <>
      {contextHolder}
      
      <Modal
        title="选择提示词"
        open={visible}
        onCancel={onCancel}
        width={1200}
        footer={null}
        destroyOnClose
      >
        <div style={{ marginBottom: 16 }}>
          <Space direction="vertical" size="middle" style={{ width: '100%' }}>
            {/* 搜索和筛选 */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <Search
                placeholder="搜索提示词名称或描述"
                allowClear
                style={{ width: 300 }}
                value={searchText}
                onChange={(e) => setSearchText(e.target.value)}
                onSearch={(value) => setSearchText(value)}
              />
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
                  loading={promptLoading}
                  title="刷新"
                >
                  刷新
                </Button>
              </Space>
            </div>

            {/* 分类筛选 */}
            <div>
              <Space wrap>
                <Button
                  type={!selectedClassId ? "primary" : "default"}
                  onClick={() => handleClassSelect(null)}
                >
                  全部
                </Button>
                {classList.map((cls) => (
                  <Button
                    key={cls.id}
                    type={selectedClassId === cls.id ? "primary" : "default"}
                    onClick={() => handleClassSelect(cls.id)}
                  >
                    {cls.name}
                  </Button>
                ))}
              </Space>
            </div>
          </Space>
        </div>

        <Divider />

        {/* 提示词列表 */}
        <Row gutter={[16, 16]}>
          {promptLoading ? (
            <Col span={24}>
              <div style={{ textAlign: 'center', padding: '40px' }}>
                <Spin size="large" />
              </div>
            </Col>
          ) : filteredPromptList.length > 0 ? (
            filteredPromptList.map((prompt) => (
              <Col xs={24} sm={12} md={8} lg={6} key={prompt.id}>
                <Card
                  hoverable
                  style={{ height: '100%', cursor: 'pointer', position: 'relative' }}
                  onClick={() => handleSelectPrompt(prompt)}
                  actions={[
                    <Tooltip title="预览内容">
                      <EyeOutlined onClick={(e) => {
                        e.stopPropagation();
                        handlePreviewContent(prompt);
                      }} />
                    </Tooltip>,
                    <Tooltip title="复制内容">
                      <CopyOutlined onClick={(e) => {
                        e.stopPropagation();
                        handleCopyContent(prompt.content || '');
                      }} />
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
                            {getClassNameById(prompt.promptClassId, classList)}
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
            ))
          ) : (
            <Col span={24}>
              <Empty
                description={searchText ? "未找到匹配的提示词" : "暂无提示词"}
                style={{ margin: '40px 0' }}
              />
            </Col>
          )}
        </Row>
      </Modal>

      {/* 内容预览模态框 */}
      <Modal
        title={`预览 - ${previewTitle}`}
        open={previewVisible}
        onCancel={() => setPreviewVisible(false)}
        width={800}
        footer={[
          <Button key="copy" icon={<CopyOutlined />} onClick={() => handleCopyContent(previewContent)}>
            复制内容
          </Button>,
          <Button key="select" type="primary" onClick={() => {
            onChange(previewContent);
            setPreviewVisible(false);
            onCancel();
          }}>
            选择此提示词
          </Button>
        ]}
      >
        <div style={{ maxHeight: '400px', overflow: 'auto' }}>
          <pre style={{ 
            whiteSpace: 'pre-wrap', 
            wordWrap: 'break-word',
            fontFamily: 'inherit',
            fontSize: '14px',
            lineHeight: '1.6',
            margin: 0,
            padding: '16px',
            backgroundColor: '#f5f5f5',
            borderRadius: '6px'
          }}>
            {previewContent}
          </pre>
        </div>
      </Modal>
    </>
  );
} 