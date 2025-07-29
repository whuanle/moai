import React, { useEffect, useState } from "react";
import { useParams, useNavigate } from "react-router";
import {
  Card,
  Typography,
  Space,
  Button,
  message,
  Spin,
  Tag,
  Divider,
  Tooltip,
} from "antd";
import {
  ArrowLeftOutlined,
  EditOutlined,
  CopyOutlined,
  UserOutlined,
  CalendarOutlined,
  LockOutlined,
  GlobalOutlined,
} from "@ant-design/icons";
import { Markdown } from '@lobehub/ui';
import { GetApiClient } from "../ServiceClient";
import { formatDateTime } from "../../helper/DateTimeHelper";

const { Title, Text, Paragraph } = Typography;

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

// 分类类型
interface PromptClass {
  id: number;
  name: string;
}

export default function PromptContent() {
  const { promptId } = useParams();
  const navigate = useNavigate();
  const [prompt, setPrompt] = useState<PromptItem | null>(null);
  const [loading, setLoading] = useState(false);
  const [messageApi, contextHolder] = message.useMessage();
  
  // 用户信息
  const [currentUserId, setCurrentUserId] = useState<number | null>(null);
  
  // 分类列表
  const [classList, setClassList] = useState<PromptClass[]>([]);
  const [classLoading, setClassLoading] = useState(false);

  // 获取当前用户信息
  const fetchUserInfo = async () => {
    try {
      const client = GetApiClient();
      const res = await client.api.common.userinfo.get();
      setCurrentUserId(res?.userId || null);
    } catch (e) {
      setCurrentUserId(null);
    }
  };

  // 获取分类列表
  const fetchClassList = async () => {
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
  };

  // 检查编辑权限
  const canEditPrompt = (prompt: PromptItem | null) => {
    if (!prompt || !currentUserId || !prompt.createUserId) return false;
    return currentUserId === prompt.createUserId;
  };

  // 获取提示词内容
  const fetchPromptContent = async () => {
    if (!promptId) {
      messageApi.error("缺少提示词ID");
      return;
    }

    try {
      setLoading(true);
      const client = GetApiClient();
      const res = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: parseInt(promptId) }
      });
      
      if (res) {
        const promptItem: PromptItem = {
          id: res.id || 0,
          name: res.name || '',
          description: res.description || '',
          content: res.content || '',
          createTime: res.createTime || '',
          createUserName: res.createUserName || '',
          createUserId: res.createUserId || 0,
          updateTime: res.updateTime || '',
          updateUserName: res.updateUserName || '',
          isOwn: false,
          isPublic: res.isPublic || false,
          promptClassId: (res as any).promptClassId || null
        };
        setPrompt(promptItem);
      } else {
        messageApi.error("获取提示词内容失败");
      }
    } catch (e) {
      console.error("获取提示词内容失败:", e);
      messageApi.error("获取提示词内容失败");
    } finally {
      setLoading(false);
    }
  };

  // 复制内容到剪贴板
  const handleCopyContent = (content: string) => {
    navigator.clipboard.writeText(content).then(() => {
      messageApi.success("已复制到剪贴板");
    }).catch(() => {
      messageApi.error("复制失败");
    });
  };

  // 编辑提示词
  const handleEdit = () => {
    if (prompt) {
      navigate(`/app/prompt/${prompt.id}/edit`);
    }
  };

  // 返回列表
  const handleBack = () => {
    navigate("/app/prompt/list");
  };

  useEffect(() => {
    fetchUserInfo();
    fetchClassList();
    fetchPromptContent();
  }, [promptId]);

  if (loading) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Spin size="large" />
        <div style={{ marginTop: 16 }}>加载中...</div>
      </div>
    );
  }

  if (!prompt) {
    return (
      <div style={{ textAlign: "center", padding: "50px" }}>
        <Text type="secondary">提示词不存在或已被删除</Text>
        <br />
        <Button type="primary" onClick={handleBack} style={{ marginTop: 16 }}>
          返回列表
        </Button>
      </div>
    );
  }

  return (
    <>
      {contextHolder}
      <Card>
        <Card.Meta
          title={
            <Space style={{ width: "100%", justifyContent: "space-between" }}>
              <Title level={4} style={{ margin: 0 }}>
                {prompt.name}
              </Title>
              <Space>
                <Tooltip title="复制内容">
                  <Button
                    icon={<CopyOutlined />}
                    onClick={() => handleCopyContent(prompt.content || '')}
                  >
                    复制内容
                  </Button>
                </Tooltip>
                {canEditPrompt(prompt) && (
                  <Button
                    type="primary"
                    icon={<EditOutlined />}
                    onClick={handleEdit}
                  >
                    编辑
                  </Button>
                )}
                <Button icon={<ArrowLeftOutlined />} onClick={handleBack}>
                  返回
                </Button>
              </Space>
            </Space>
          }
          description={
            <Space direction="vertical" style={{ width: "100%" }}>
              {prompt.description && (
                <Paragraph type="secondary" style={{ marginBottom: 0 }}>
                  {prompt.description}
                </Paragraph>
              )}
              <Space>
                <Text type="secondary">
                  <UserOutlined /> {prompt.createUserName}
                </Text>
                <Text type="secondary">
                  <CalendarOutlined /> {formatDateTime(prompt.createTime)}
                </Text>
                {prompt.promptClassId && (
                  <Text type="secondary">
                    分类: {classList.find(cls => cls.id === prompt.promptClassId)?.name || '未知分类'}
                  </Text>
                )}
                {prompt.isPublic ? (
                  <Tag color="green" icon={<GlobalOutlined />}>公开</Tag>
                ) : (
                  <Tag color="orange" icon={<LockOutlined />}>私密</Tag>
                )}
              </Space>
            </Space>
          }
        />
        
        <Divider />
        
        <div 
          style={{ 
            marginBottom: 16,
            background: '#1890ff',
            padding: '12px 16px',
            borderRadius: 8,
            color: 'white'
          }}
        >
          <Title level={4} style={{ margin: 0, color: 'white' }}>提示词内容</Title>
        </div>
        
        <div
          style={{
            padding: 16,
            borderRadius: 6,
            border: '1px solid #d9d9d9',
            minHeight: 200,
            maxHeight: 600,
            overflow: 'auto'
          }}
        >
          {prompt.content ? (
            <Markdown
              fontSize={14}
              lineHeight={1.6}
              marginMultiple={1.5}
              fullFeaturedCodeBlock={true}
            >
              {prompt.content}
            </Markdown>
          ) : (
            <Text type="secondary">暂无内容</Text>
          )}
        </div>
      </Card>
    </>
  );
}