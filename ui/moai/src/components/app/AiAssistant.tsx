import React, { useState, useRef, useEffect } from "react";
import { Typography, Input, Button, List, Popconfirm, message, Spin, Empty } from "antd";
import type { InputRef } from "antd";
import {
  ChatList,
  ChatInputArea,
  ChatSendButton,
  ChatInputActionBar,
  TokenTag,
  ActionsBar,
} from "@lobehub/ui/chat";
import { ActionIcon } from "@lobehub/ui";
import { 
  Eraser, 
  Languages, 
  Plus, 
  MessageSquare, 
  Trash2, 
  Clock,
  Bot,
  User
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

interface ChatMessage {
  id: string;
  role: "user" | "assistant";
  content: string;
  createAt: number;
  updateAt?: number;
  meta?: any;
}

// 使用 API 类型
type ChatTopic = AiAssistantChatTopic;

const DEFAULT_TITLE = "新对话";

const AiAssistant: React.FC = () => {
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
  const loadTopics = async () => {
    setTopicsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.topic_list.get();
      
      if (response && response.items) {
        // 过滤掉无效的项
        const validTopics = response.items.filter(topic => topic && topic.chatId);
        setTopics(validTopics);
      } else {
        // 如果没有数据，设置为空数组
        setTopics([]);
      }
    } catch (error) {
      console.error("加载话题列表失败:", error);
      messageApi.error("加载话题列表失败");
    } finally {
      setTopicsLoading(false);
    }
  };

  // 加载对话历史
  const loadChatHistory = async (chatId: string) => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.chat_history.get({
        queryParameters: {
          chatId: chatId
        }
      });
      
      if (response && response.chatHistory) {
        // 解析对话历史
        const historyMessages: ChatMessage[] = [];
        const chatHistoryArray = response.chatHistory as unknown as any[];
        if (Array.isArray(chatHistoryArray)) {
          chatHistoryArray.forEach((msg: any, index: number) => {
            if (msg && typeof msg === 'object') {
              historyMessages.push({
                id: `${chatId}-${index}`,
                role: msg.role || "user",
                content: msg.content || "",
                createAt: Date.now() - (chatHistoryArray.length - index) * 1000,
              });
            }
          });
        }
        setMessages(historyMessages);
        // 安全地访问 title 属性
        const title = response?.title || DEFAULT_TITLE;
        setCurrentTitle(title || DEFAULT_TITLE);
        setEditValue(title || DEFAULT_TITLE);
      } else {
        // 如果没有对话历史，设置默认值
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
  };

  // 删除对话
  const deleteChat = async (chatId: string) => {
    try {
      const client = GetApiClient();
      const deleteCommand: DeleteAiAssistantChatCommand = {
        chatId: chatId
      };
      
      await client.api.app.assistant.delete_chat.delete(deleteCommand);
      messageApi.success("删除成功");
      
      // 如果删除的是当前对话，清空当前对话
      if (currentChatId === chatId) {
        setCurrentChatId(null);
        setMessages([]);
        setCurrentTitle(DEFAULT_TITLE);
        setEditValue(DEFAULT_TITLE);
      }
      // 重新加载话题列表
      loadTopics();
    } catch (error) {
      console.error("删除对话失败:", error);
      messageApi.error("删除失败");
    }
  };

  // 创建新对话
  const createNewChat = () => {
    setCurrentChatId(null);
    setMessages([]);
    setCurrentTitle(DEFAULT_TITLE);
    setEditValue(DEFAULT_TITLE);
    setEditing(false);
  };

  // 选择对话
  const selectChat = (chatId: string) => {
    setCurrentChatId(chatId);
    loadChatHistory(chatId);
  };

  // 发送消息
  const handleSend = async () => {
    if (!input.trim() || sending) return;
    
    const userMsg: ChatMessage = {
      id: Date.now().toString(),
      role: "user",
      content: input.trim(),
      createAt: Date.now(),
    };
    
    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setSending(true);

    // 生成AI回复消息占位
    const aiMsgId = (Date.now() + 1).toString();
    setMessages((prev) => [
      ...prev,
      {
        id: aiMsgId,
        role: "assistant",
        content: "",
        createAt: Date.now(),
      } as ChatMessage,
    ]);

    // 构造请求体
    const payload: ProcessingAiAssistantChatCommand = {
      chatId: currentChatId,
      content: input,
      title: (currentTitle === DEFAULT_TITLE ? input.substring(0, 30) : currentTitle) || DEFAULT_TITLE,
      modelId: 7, // 默认模型ID
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
          // 处理多条 data: ...\n
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
                // 处理不同的SSE格式
                let content = "";
                if (delta.choices?.[0]?.delta?.content) {
                  // OpenAI 格式
                  content = delta.choices[0].delta.content;
                } else if (delta.content) {
                  // 直接内容格式
                  content = delta.content;
                } else if (delta.text) {
                  // 文本格式
                  content = delta.text;
                }
                
                if (content) {
                  aiContent += content;
                  setMessages((prevMsgs) =>
                    prevMsgs.map((msg) =>
                      msg && msg.id === aiMsgId ? { ...msg, content: aiContent || "" } : msg
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
      
      // 如果是新对话，更新话题列表
      if (!currentChatId) {
        loadTopics();
      }
      
    } catch (err) {
      console.error("发送消息失败:", err);
      setMessages((prevMsgs) =>
        prevMsgs.map((msg) =>
          msg && msg.id === aiMsgId ? { ...msg, content: "AI 回复失败，请重试" } : msg
        ).filter(Boolean)
      );
      messageApi.error("发送消息失败");
    } finally {
      setSending(false);
    }
  };

  // 保存标题
  const handleTitleSave = () => {
    const newTitle = editValue.trim() || DEFAULT_TITLE;
    setCurrentTitle(newTitle);
    setEditValue(newTitle);
    setEditing(false);
  };

  // 格式化时间
  const formatTime = (timeStr: string) => {
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
  };

  // 组件挂载时加载话题列表
  useEffect(() => {
    loadTopics();
  }, []);

  return (
    <>
      {contextHolder}
            <div className="ai-assistant-container">
        {/* 左侧话题列表 */}
        <div className="sidebar">
          {/* 顶部标题和新建按钮 */}
          <div className="sidebar-header">
            <div style={{ display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "12px" }}>
              <Typography.Title level={4} className="sidebar-title">
                对话记录
              </Typography.Title>
              <Button
                type="primary"
                icon={<Plus size={16} />}
                size="small"
                onClick={createNewChat}
                className="new-chat-btn"
              >
                新建
              </Button>
            </div>
          </div>

          {/* 话题列表 */}
          <div className="topics-list">
            {topicsLoading ? (
              <div className="loading-container">
                <Spin />
              </div>
            ) : topics.length === 0 ? (
              <Empty
                description="暂无对话记录"
                style={{ marginTop: "60px" }}
              />
            ) : (
              <List
                dataSource={topics}
                renderItem={(topic) => {
                  // 安全检查 topic 对象
                  if (!topic || !topic.chatId) {
                    return null;
                  }
                  
                  return (
                    <List.Item
                      className={`topic-item ${currentChatId === topic.chatId ? 'active' : ''}`}
                      onClick={() => topic.chatId && selectChat(topic.chatId)}
                      actions={[
                        <Popconfirm
                          title="确定要删除这个对话吗？"
                          onConfirm={() => topic.chatId && deleteChat(topic.chatId)}
                          okText="确定"
                          cancelText="取消"
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
                          <div className="topic-avatar">
                            <MessageSquare size={16} />
                          </div>
                        }
                        title={
                          <Typography.Text
                            ellipsis
                            className={`topic-title ${currentChatId === topic.chatId ? 'active' : ''}`}
                          >
                            {topic.title || "未命名对话"}
                          </Typography.Text>
                        }
                        description={
                          <div className="topic-time">
                            <Clock size={12} />
                                                      <Typography.Text>
                            {formatTime(topic.createTime || '')}
                          </Typography.Text>
                          </div>
                        }
                      />
                    </List.Item>
                  );
                }}
              />
            )}
          </div>
        </div>

        {/* 右侧主内容区 */}
        <div className="main-content">
          {/* 顶部标题栏 */}
          <div className="header-bar">
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
                  fontSize: "18px",
                  width: "400px",
                  textAlign: "center",
                  borderRadius: "8px",
                }}
                autoFocus
              />
            ) : (
              <Typography.Title
                level={3}
                className="chat-title"
                onClick={() => setEditing(true)}
                title="点击修改标题"
              >
                {currentTitle || DEFAULT_TITLE}
              </Typography.Title>
            )}
          </div>

          {/* 对话历史 */}
          <div ref={chatListRef} className="chat-area">
            {loading ? (
              <div className="loading-container">
                <Spin size="large" />
              </div>
            ) : messages.length === 0 ? (
              <div className="empty-state">
                <Bot size={48} className="empty-state-icon" />
                <Typography.Text className="empty-state-title">
                  开始新的对话
                </Typography.Text>
                <Typography.Text className="empty-state-subtitle">
                  在下方输入框中输入您的问题
                </Typography.Text>
              </div>
            ) : (
              <div className="chat-container">
                <ChatList
                  data={messages.filter(msg => msg && msg.id && msg.content !== undefined).map((msg) => ({
                    id: msg.id,
                    role: msg.role,
                    content: msg.content || "",
                    createAt: msg.createAt,
                    updateAt: msg.updateAt ?? msg.createAt,
                    meta: msg.meta ?? undefined,
                  }))}
                  renderActions={{ default: ActionsBar }}
                  renderMessages={{
                    default: ({ id, editableContent }) => (
                      <div id={id}>{editableContent}</div>
                    ),
                  }}
                />
              </div>
            )}
          </div>

          {/* 输入区 */}
          <div className="input-area">
            <div className="input-container">
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
            </div>
          </div>
        </div>
      </div>
    </>
  );
};

export default AiAssistant;
