import { useEffect, useState, useMemo } from 'react';
import { useNavigate, useParams } from 'react-router';
import { Card, Typography, Space, Button, Tag, Spin, message, Descriptions, Anchor } from 'antd';
import { ArrowLeftOutlined, EditOutlined, CopyOutlined, UnorderedListOutlined } from '@ant-design/icons';
import ReactMarkdown from 'react-markdown';
import remarkGfm from 'remark-gfm';
import rehypeSlug from 'rehype-slug';
import { GetApiClient } from '../ServiceClient';
import { proxyRequestError } from '../../helper/RequestError';
import { PromptItem, PromptClassifyItem, QueryePromptClassCommandResponse } from '../../apiClient/models';
import useAppStore from '../../stateshare/store';
import './PromptViewPage.css';

const { Title, Text } = Typography;

interface TocItem {
  key: string;
  href: string;
  title: string;
  level: number;
  children?: TocItem[];
}

// 从 Markdown 内容提取标题生成大纲
function extractHeadings(markdown: string): TocItem[] {
  const headingRegex = /^(#{1,6})\s+(.+)$/gm;
  const headings: TocItem[] = [];
  let match;
  
  while ((match = headingRegex.exec(markdown)) !== null) {
    const level = match[1].length;
    // 去除 Markdown 格式符号（加粗、斜体、代码等）
    const rawTitle = match[2].trim();
    const title = rawTitle
      .replace(/\*\*(.+?)\*\*/g, '$1')  // 去除加粗 **text**
      .replace(/\*(.+?)\*/g, '$1')       // 去除斜体 *text*
      .replace(/__(.+?)__/g, '$1')       // 去除加粗 __text__
      .replace(/_(.+?)_/g, '$1')         // 去除斜体 _text_
      .replace(/`(.+?)`/g, '$1')         // 去除行内代码 `text`
      .replace(/~~(.+?)~~/g, '$1')       // 去除删除线 ~~text~~
      .replace(/\[(.+?)\]\(.+?\)/g, '$1') // 去除链接，保留文字
      .trim();
    
    const slug = title
      .toLowerCase()
      .replace(/[^\w\u4e00-\u9fa5]+/g, '-')
      .replace(/^-+|-+$/g, '');
    
    headings.push({
      key: slug,
      href: `#${slug}`,
      title,
      level,
    });
  }
  
  return headings;
}

// 将扁平标题列表转换为嵌套结构
function buildTocTree(headings: TocItem[]): TocItem[] {
  if (headings.length === 0) return [];
  
  const result: TocItem[] = [];
  const stack: TocItem[] = [];
  
  for (const heading of headings) {
    const item = { ...heading, children: [] };
    
    while (stack.length > 0 && stack[stack.length - 1].level >= heading.level) {
      stack.pop();
    }
    
    if (stack.length === 0) {
      result.push(item);
    } else {
      const parent = stack[stack.length - 1];
      if (!parent.children) parent.children = [];
      parent.children.push(item);
    }
    
    stack.push(item);
  }
  
  return result;
}

export default function PromptViewPage() {
  const navigate = useNavigate();
  const { promptId } = useParams<{ promptId: string }>();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);
  const currentUserId = userDetailInfo?.userId;
  
  const [loading, setLoading] = useState(true);
  const [prompt, setPrompt] = useState<PromptItem | null>(null);
  const [categories, setCategories] = useState<PromptClassifyItem[]>([]);
  const [messageApi, contextHolder] = message.useMessage();

  const fetchCategories = async () => {
    try {
      const client = GetApiClient();
      const response: QueryePromptClassCommandResponse | undefined = await client.api.prompt.class_list.get();
      if (response?.items) setCategories(response.items);
    } catch (error) {
      console.error('获取分类列表失败:', error);
    }
  };

  const fetchPromptDetail = async () => {
    if (!promptId) {
      messageApi.error('提示词ID不存在');
      navigate('/app/prompt/list');
      return;
    }

    setLoading(true);
    try {
      const client = GetApiClient();
      const res: PromptItem | undefined = await client.api.prompt.prompt_content.get({
        queryParameters: { promptId: parseInt(promptId, 10) }
      });
      if (res) {
        setPrompt(res);
      } else {
        messageApi.error('获取提示词详情失败');
        navigate('/app/prompt/list');
      }
    } catch (error) {
      console.error('获取提示词详情失败:', error);
      proxyRequestError(error, messageApi, '获取提示词详情失败');
      navigate('/app/prompt/list');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchCategories();
    fetchPromptDetail();
  }, [promptId]);

  // 生成大纲
  const tocItems = useMemo(() => {
    if (!prompt?.content) return [];
    const headings = extractHeadings(prompt.content);
    return buildTocTree(headings);
  }, [prompt?.content]);

  const getCategoryName = (classId: number) => 
    categories.find(c => c.classifyId === classId)?.name || '未分类';

  const isOwner = prompt?.createUserId === currentUserId;

  const handleCopyContent = () => {
    if (prompt?.content) {
      navigator.clipboard.writeText(prompt.content);
      messageApi.success('已复制到剪贴板');
    }
  };

  // 递归渲染 Anchor 项
  const renderAnchorItems = (items: TocItem[]): any[] => {
    return items.map(item => ({
      key: item.key,
      href: item.href,
      title: item.title,
      children: item.children && item.children.length > 0 
        ? renderAnchorItems(item.children) 
        : undefined,
    }));
  };

  return (
    <div className="page-container">
      {contextHolder}
      <Spin spinning={loading}>
        {prompt && (
          <div className="prompt-view-layout">
            {/* 左侧大纲 */}
            {tocItems.length > 0 && (
              <div className="prompt-view-outline">
                <div className="prompt-view-outline-header">
                  <UnorderedListOutlined /> 大纲
                </div>
                <Anchor
                  offsetTop={80}
                  items={renderAnchorItems(tocItems)}
                  getContainer={() => document.querySelector('.prompt-view-content') as HTMLElement || window}
                />
              </div>
            )}
            
            {/* 右侧内容 */}
            <Card className={`prompt-view-card ${tocItems.length > 0 ? 'has-outline' : ''}`}>
              <Card.Meta
                title={
                  <Space style={{ width: '100%', justifyContent: 'space-between', flexWrap: 'wrap' }}>
                    <Space direction="vertical" size={4}>
                      <Title level={4} style={{ margin: 0 }}>{prompt.name}</Title>
                      {prompt.description && (
                        <Text type="secondary">{prompt.description}</Text>
                      )}
                    </Space>
                    <Space>
                      <Button icon={<CopyOutlined />} onClick={handleCopyContent}>
                        复制内容
                      </Button>
                      {isOwner && (
                        <Button 
                          type="primary" 
                          icon={<EditOutlined />} 
                          onClick={() => navigate(`/app/prompt/${promptId}/edit`)}
                        >
                          编辑
                        </Button>
                      )}
                      <Button icon={<ArrowLeftOutlined />} onClick={() => navigate('/app/prompt/list')}>
                        返回列表
                      </Button>
                    </Space>
                  </Space>
                }
              />
              
              <div className="prompt-view-meta">
                <Descriptions size="small" column={{ xs: 1, sm: 2, md: 3 }}>
                  <Descriptions.Item label="分类">
                    <Tag color="blue">{getCategoryName(prompt.promptClassId || 0)}</Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="可见性">
                    <Tag color={prompt.isPublic ? 'green' : 'orange'}>
                      {prompt.isPublic ? '公开' : '私有'}
                    </Tag>
                  </Descriptions.Item>
                  <Descriptions.Item label="创建人">
                    {prompt.createUserName || '-'}
                  </Descriptions.Item>
                  <Descriptions.Item label="创建时间">
                    {prompt.createTime ? new Date(prompt.createTime).toLocaleString() : '-'}
                  </Descriptions.Item>
                  <Descriptions.Item label="最后修改人">
                    {prompt.updateUserName || '-'}
                  </Descriptions.Item>
                  <Descriptions.Item label="最后修改时间">
                    {prompt.updateTime ? new Date(prompt.updateTime).toLocaleString() : '-'}
                  </Descriptions.Item>
                </Descriptions>
              </div>

              <div className="prompt-view-content" id="prompt-content">
                <ReactMarkdown remarkPlugins={[remarkGfm]} rehypePlugins={[rehypeSlug]}>
                  {prompt.content || ''}
                </ReactMarkdown>
              </div>
            </Card>
          </div>
        )}
      </Spin>
    </div>
  );
}
