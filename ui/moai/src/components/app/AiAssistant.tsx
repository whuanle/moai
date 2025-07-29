import React, { useState, useRef, useEffect, useCallback } from "react";
import { 
  Typography, 
  Input, 
  Button, 
  List, 
  Popconfirm, 
  message, 
  Spin, 
  Empty, 
  Layout,
  Card,
  Space,
  Avatar,
  Tag,
  Divider,
  Tooltip,
  Badge
} from "antd";
import type { InputRef } from "antd";
import {
  ChatList,
  ChatInputArea,
  ChatSendButton,
  ChatInputActionBar,
  TokenTag,
  ActionsBar,
} from "@lobehub/ui/chat";
import { ActionIcon, ThemeProvider } from "@lobehub/ui";
import { 
  Eraser, 
  Languages, 
  Plus, 
  MessageSquare, 
  Trash2, 
  Clock,
  Bot,
  User,
  Send,
  Edit3,
  MoreHorizontal
} from "lucide-react";
import { Flexbox } from "react-layout-kit";
import { GetApiClient } from "../ServiceClient";
import { EnvOptions } from "../../Env";
import { 
  ProcessingAiAssistantChatCommand,
  DeleteAiAssistantChatCommand,
  QueryAiAssistantChatTopicListCommandResponse,
  QueryAiAssistantChatHistoryCommandResponse,
  AiAssistantChatTopic
} from "../../apiClient/models";
import "./AiAssistant.css";

const { Sider, Content } = Layout;
const { Title, Text } = Typography;

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  createAt: number;
  updateAt?: number;
  extra?: any;
  meta?: {
    avatar?: string;
    title?: string;
    backgroundColor?: string;
  };
}

type ChatTopic = AiAssistantChatTopic;

const DEFAULT_TITLE = "新对话";

const AiAssistant: React.FC = () => {
  // 状态管理
  const [topics, setTopics] = useState<ChatTopic[]>([]);
  const [currentChatId, setCurrentChatId] = useState<string | null>(null);
  const [currentTitle, setCurrentTitle] = useState<string>(DEFAULT_TITLE);
  const [editing, setEditing] = useState(false);
  const [editValue, setEditValue] = useState<string>(DEFAULT_TITLE);
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [topicsLoading, setTopicsLoading] = useState(false);
  const [sending, setSending] = useState(false);

  // 引用
  const inputRef = useRef<InputRef>(null);
  const chatListRef = useRef<HTMLDivElement>(null);
  const [messageApi, contextHolder] = message.useMessage();

  // 自动滚动到底部
  useEffect(() => {
    if (chatListRef.current) {
      chatListRef.current.scrollTop = chatListRef.current.scrollHeight;
    }
  }, [messages]);

  // 加载话题列表
  const loadTopics = useCallback(async () => {
    setTopicsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.topic_list.get();
      
      if (response?.items) {
        const validTopics = response.items.filter(topic => topic?.chatId);
        setTopics(validTopics);
      } else {
        setTopics([]);
      }
    } catch (error) {
      console.error("加载话题列表失败:", error);
      messageApi.error("加载话题列表失败");
    } finally {
      setTopicsLoading(false);
    }
  }, [messageApi]);

  // 加载对话历史
  const loadChatHistory = useCallback(async (chatId: string) => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.chat_history.get({
        queryParameters: { chatId }
      });
      
      if (response?.chatHistory) {
        const historyMessages: ChatMessage[] = [];
        const chatHistoryArray = response.chatHistory as unknown as any[];
        
        if (Array.isArray(chatHistoryArray)) {
          chatHistoryArray.forEach((msg: any, index: number) => {
            if (msg && typeof msg === 'object') {
                          const createTime = Date.now() - (chatHistoryArray.length - index) * 1000;
            historyMessages.push({
              id: `${chatId}-${index}`,
              role: msg.role || "user",
              content: msg.content || "",
              createAt: createTime,
              updateAt: createTime,
              extra: {},
              meta: {
                avatar: (msg.role || "user") === 'user' ? "https://avatars.githubusercontent.com/u/17870709?v=4" : "😎",
                backgroundColor: (msg.role || "user") === 'user' ? undefined : "#E8DA5A",
                title: (msg.role || "user") === 'user' ? "用户" : "AI 助手",
              },
            });
            }
          });
        }
        
        setMessages(historyMessages);
        const title = response?.title || DEFAULT_TITLE;
        setCurrentTitle(title);
        setEditValue(title);
      } else {
        setMessages([]);
        setCurrentTitle(DEFAULT_TITLE);
        setEditValue(DEFAULT_TITLE);
      }
    } catch (error) {
      console.error("加载对话历史失败:", error);
      messageApi.error("加载对话历史失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 删除对话
  const deleteChat = useCallback(async (chatId: string) => {
    try {
      const client = GetApiClient();
      const deleteCommand: DeleteAiAssistantChatCommand = { chatId };
      
      await client.api.app.assistant.delete_chat.delete(deleteCommand);
      messageApi.success("删除成功");
      
      if (currentChatId === chatId) {
        setCurrentChatId(null);
        setMessages([]);
        setCurrentTitle(DEFAULT_TITLE);
        setEditValue(DEFAULT_TITLE);
      }
      loadTopics();
    } catch (error) {
      console.error("删除对话失败:", error);
      messageApi.error("删除失败");
    }
  }, [currentChatId, loadTopics, messageApi]);

  // 创建新对话
  const createNewChat = useCallback(() => {
    setCurrentChatId(null);
    setMessages([]);
    setCurrentTitle(DEFAULT_TITLE);
    setEditValue(DEFAULT_TITLE);
    setEditing(false);
  }, []);

  // 选择对话
  const selectChat = useCallback((chatId: string) => {
    setCurrentChatId(chatId);
    loadChatHistory(chatId);
  }, [loadChatHistory]);

  // 发送消息
  const handleSend = useCallback(async () => {
    if (!input.trim() || sending) return;
    
    const userMsg: ChatMessage = {
      id: Date.now().toString(),
      role: "user",
      content: input.trim(),
      createAt: Date.now(),
      updateAt: Date.now(),
      extra: {},
      meta: {
        avatar: "https://avatars.githubusercontent.com/u/17870709?v=4",
        title: "用户",
      },
    };
    
    setMessages(prev => [...prev, userMsg]);
    setInput("");
    setSending(true);

    const aiMsgId = (Date.now() + 1).toString();
    setMessages(prev => [
      ...prev,
      {
        id: aiMsgId,
        role: "assistant",
        content: "",
        createAt: Date.now(),
        updateAt: Date.now(),
        extra: {},
        meta: {
          avatar: "😎",
          backgroundColor: "#E8DA5A",
          title: "AI 助手",
        },
      } as ChatMessage,
    ]);

    const payload: ProcessingAiAssistantChatCommand = {
      chatId: currentChatId,
      content: input,
      title: (currentTitle === DEFAULT_TITLE ? input.substring(0, 30) : currentTitle) || DEFAULT_TITLE,
      modelId: 7,
    };

    try {
      const token = localStorage.getItem("userinfo.accessToken");
      
      const response = await fetch(`${EnvOptions.ServerUrl}/api/app/assistant/completions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Accept: "text/event-stream",
          ...(token && { Authorization: `Bearer ${token}` }),
        },
        body: JSON.stringify(payload),
      });
      
      if (!response.body) throw new Error("No response body");
      
      const reader = response.body.getReader();
      let aiContent = "";
      let done = false;
      let buffer = "";
      
      while (!done) {
        const { value, done: doneReading } = await reader.read();
        done = doneReading;
        
        if (value) {
          buffer += new TextDecoder().decode(value, { stream: true });
          const lines = buffer.split("\n");
          buffer = lines.pop() || "";
          
          for (const line of lines) {
            if (line.startsWith("data:")) {
              const data = line.replace(/^data:/, "").trim();
              if (data === "[DONE]") {
                done = true;
                break;
              }
              
              try {
                const delta = JSON.parse(data);
                let content = "";
                if (delta.choices?.[0]?.delta?.content) {
                  content = delta.choices[0].delta.content;
                } else if (delta.content) {
                  content = delta.content;
                } else if (delta.text) {
                  content = delta.text;
                }
                
                if (content) {
                  aiContent += content;
                  setMessages(prevMsgs =>
                    prevMsgs.map(msg =>
                      msg?.id === aiMsgId ? { ...msg, content: aiContent || "" } : msg
                    ).filter(Boolean)
                  );
                }
              } catch (e) {
                // 忽略解析错误
              }
            }
          }
        }
      }
      
      if (!currentChatId) {
        loadTopics();
      }
      
    } catch (err) {
      console.error("发送消息失败:", err);
      setMessages(prevMsgs =>
        prevMsgs.map(msg =>
          msg?.id === aiMsgId ? { ...msg, content: "AI 回复失败，请重试" } : msg
        ).filter(Boolean)
      );
      messageApi.error("发送消息失败");
    } finally {
      setSending(false);
    }
  }, [input, sending, currentChatId, currentTitle, loadTopics, messageApi]);

  // 保存标题
  const handleTitleSave = useCallback(() => {
    const newTitle = editValue.trim() || DEFAULT_TITLE;
    setCurrentTitle(newTitle);
    setEditValue(newTitle);
    setEditing(false);
  }, [editValue]);

  // 格式化时间
  const formatTime = useCallback((timeStr: string) => {
    if (!timeStr) return '未知时间';
    
    try {
      const date = new Date(timeStr);
      if (isNaN(date.getTime())) return '无效时间';
      
      const now = new Date();
      const diff = now.getTime() - date.getTime();
      const days = Math.floor(diff / (1000 * 60 * 60 * 24));
      
      if (days === 0) {
        return date.toLocaleTimeString('zh-CN', { hour: '2-digit', minute: '2-digit' });
      } else if (days === 1) {
        return '昨天';
      } else if (days < 7) {
        return `${days}天前`;
      } else {
        return date.toLocaleDateString('zh-CN');
      }
    } catch (error) {
      return '时间格式错误';
    }
  }, []);

  // 组件挂载时加载话题列表
  useEffect(() => {
    loadTopics();
  }, [loadTopics]);

  // 渲染话题列表项
  const renderTopicItem = useCallback((topic: ChatTopic) => {
    if (!topic?.chatId) return null;
    
    const isActive = currentChatId === topic.chatId;
    
    return (
      <List.Item
        className={`topic-item ${isActive ? 'active' : ''}`}
        onClick={() => selectChat(topic.chatId!)}
        actions={[
          <Popconfirm
            title="确定要删除这个对话吗？"
            description="删除后无法恢复"
            onConfirm={() => deleteChat(topic.chatId!)}
            okText="确定"
            cancelText="取消"
            placement="left"
          >
            <Button
              type="text"
              size="small"
              icon={<Trash2 size={14} />}
              className="delete-btn"
              onClick={(e) => e.stopPropagation()}
            />
          </Popconfirm>
        ]}
      >
        <List.Item.Meta
          avatar={
            <Badge dot={isActive}>
              <Avatar 
                size="small" 
                icon={<MessageSquare size={14} />}
                className={isActive ? 'active-avatar' : ''}
              />
            </Badge>
          }
          title={
            <Text
              ellipsis
              className={`topic-title ${isActive ? 'active' : ''}`}
              style={{ fontWeight: isActive ? 600 : 400 }}
            >
              {topic.title || "未命名对话"}
            </Text>
          }
          description={
            <Space size="small" className="topic-time">
              <Clock size={12} />
              <Text type="secondary" style={{ fontSize: '12px' }}>
                {formatTime(topic.createTime || '')}
              </Text>
            </Space>
          }
        />
      </List.Item>
    );
  }, [currentChatId, selectChat, deleteChat, formatTime]);

  // 渲染空状态
  const renderEmptyState = () => (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={
        <Space direction="vertical" size="small">
          <Bot size={48} style={{ color: '#d9d9d9' }} />
          <Title level={4} style={{ margin: 0, color: '#8c8c8c' }}>
            开始新的对话
          </Title>
          <Text type="secondary">
            在下方输入框中输入您的问题
          </Text>
        </Space>
      }
      style={{ marginTop: '60px' }}
    />
  );

  return (
    <>
      {contextHolder}
      <ThemeProvider>
        <Layout className="ai-assistant-layout">
          {/* 左侧边栏 */}
          <Sider 
            width={320} 
            className="ai-assistant-sider"
            theme="light"
          >
            <Card 
              className="sidebar-card"
              bodyStyle={{ padding: '16px', height: '100%', display: 'flex', flexDirection: 'column' }}
            >
              {/* 顶部标题和新建按钮 */}
              <Space direction="vertical" size="middle" style={{ width: '100%' }}>
                <Space style={{ width: '100%', justifyContent: 'space-between' }}>
                  <Title level={4} style={{ margin: 0 }}>
                    对话记录
                  </Title>
                  <Button
                    type="primary"
                    icon={<Plus size={16} />}
                    size="small"
                    onClick={createNewChat}
                  >
                    新建
                  </Button>
                </Space>
                
                <Divider style={{ margin: '8px 0' }} />
                
                {/* 话题列表 */}
                {topicsLoading ? (
                  <div style={{ textAlign: 'center', padding: '40px 0' }}>
                    <Spin />
                  </div>
                ) : topics.length === 0 ? (
                  <Empty
                    description="暂无对话记录"
                    style={{ marginTop: '40px' }}
                  />
                ) : (
                  <List
                    dataSource={topics}
                    renderItem={renderTopicItem}
                    className="topics-list"
                    style={{ flex: 1, overflow: 'auto' }}
                  />
                )}
              </Space>
            </Card>
          </Sider>

          {/* 右侧主内容区 */}
          <Content className="ai-assistant-content">
            {/* 上半部分：对话历史容器 */}
            <Card 
              className="chat-list-container"
              bodyStyle={{ 
                padding: 0, 
                flex: 1, 
                display: 'flex', 
                flexDirection: 'column' 
              }}
            >
              {/* 顶部标题栏 */}
              <Card 
                size="small" 
                className="header-card"
                bodyStyle={{ padding: '16px 24px' }}
              >
                {editing ? (
                  <Input
                    ref={inputRef}
                    value={editValue}
                    onChange={(e) => setEditValue(e.target.value)}
                    onBlur={handleTitleSave}
                    onPressEnter={handleTitleSave}
                    maxLength={30}
                    style={{
                      fontWeight: 600,
                      fontSize: '18px',
                      textAlign: 'center',
                      borderRadius: '8px',
                    }}
                    autoFocus
                  />
                ) : (
                  <Space style={{ width: '100%', justifyContent: 'center' }}>
                    <Title
                      level={3}
                      style={{ margin: 0, cursor: 'pointer' }}
                      onClick={() => setEditing(true)}
                    >
                      {currentTitle || DEFAULT_TITLE}
                    </Title>
                    <Tooltip title="编辑标题">
                      <Button
                        type="text"
                        size="small"
                        icon={<Edit3 size={16} />}
                        onClick={() => setEditing(true)}
                      />
                    </Tooltip>
                  </Space>
                )}
              </Card>

              {/* ChatList 区域 */}
              <div 
                ref={chatListRef} 
                className="chat-list-area"
                style={{ flex: 1, overflow: 'auto', padding: '16px 24px' }}
              >
                {loading ? (
                  <div style={{ textAlign: 'center', padding: '40px 0' }}>
                    <Spin size="large" />
                  </div>
                ) : messages.length === 0 ? (
                  renderEmptyState()
                ) : (
                  <ChatList
                    data={messages.filter(msg => msg?.id && msg.content !== undefined).map((msg) => ({
                      id: msg.id,
                      role: msg.role,
                      content: msg.content || "",
                      createAt: msg.createAt,
                      updateAt: msg.updateAt ?? msg.createAt,
                      extra: msg.extra || {},
                      meta: msg.meta || {
                        avatar: msg.role === 'user' ? "https://avatars.githubusercontent.com/u/17870709?v=4" : "😎",
                        backgroundColor: msg.role === 'user' ? undefined : "#E8DA5A",
                        title: msg.role === 'user' ? "用户" : "AI 助手",
                      },
                    }))}
                    renderActions={{ default: ActionsBar }}
                    renderMessages={{
                      default: ({ id, editableContent }) => (
                        <div id={id}>{editableContent}</div>
                      ),
                    }}
                    style={{ width: "100%" }}
                  />
                )}
              </div>
            </Card>

            {/* 下半部分：ChatInputArea 容器 */}
            <Card 
              className="chat-input-container"
              bodyStyle={{ 
                padding: 0, 
                height: 'auto' 
              }}
            >
              <ChatInputArea
                value={input}
                onInput={setInput}
                onSend={handleSend}
                placeholder="请输入你的问题..."
                disabled={sending}
                bottomAddons={<ChatSendButton onSend={handleSend} />}
                topAddons={
                  <ChatInputActionBar
                    leftAddons={
                      <>
                        <ActionIcon icon={Languages} />
                        <ActionIcon icon={Eraser} />
                        <TokenTag maxValue={5000} value={input.length} />
                      </>
                    }
                  />
                }
              />
            </Card>
          </Content>
        </Layout>
      </ThemeProvider>
    </>
  );
};

export default AiAssistant;
