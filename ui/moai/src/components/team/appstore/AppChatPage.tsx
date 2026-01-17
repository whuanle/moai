import { useState, useCallback, useRef, useEffect } from "react";
import { useParams, useNavigate } from "react-router";
import { Button, Input, message, Empty, Typography, Avatar, Collapse, Spin, Modal } from "antd";
import {
  SendOutlined,
  UserOutlined,
  RobotOutlined,
  LoadingOutlined,
  ToolOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
  PlusOutlined,
  DeleteOutlined,
  EditOutlined,
  ArrowLeftOutlined,
} from "@ant-design/icons";
import { Markdown } from "@lobehub/ui";
import { GetApiClient } from "../../ServiceClient";
import { proxyRequestError } from "../../../helper/RequestError";
import { EnvOptions } from "../../../Env";
import useAppStore from "../../../stateshare/store";
import type {
  AiProcessingPluginCall,
  AiProcessingChatStreamState,
  AppChatTopicItem,
  AppChatHistoryItem,
  AiProcessingChoice,
} from "../../../apiClient/models";
import "./AppChatPage.css";

const { TextArea } = Input;
const { Text } = Typography;

interface StreamingPluginCall extends AiProcessingPluginCall {
  streamState?: AiProcessingChatStreamState | null;
}

interface ChatMessage {
  role: "user" | "assistant" | "tool";
  content: string;
  pluginCalls?: StreamingPluginCall[];
}

const tryParseJson = (str: string | null | undefined): Record<string, unknown> | null => {
  if (!str) return null;
  try {
    return JSON.parse(str);
  } catch {
    return null;
  }
};

const PluginCallDisplay: React.FC<{ pluginCall: StreamingPluginCall }> = ({ pluginCall }) => {
  const params = pluginCall.params;
  const parsedResult = tryParseJson(pluginCall.result);
  const isLoading = pluginCall.streamState === "start" || pluginCall.streamState === "processing";
  const isError = pluginCall.streamState === "error";
  const isComplete = pluginCall.streamState === "end" || (!isLoading && !isError && pluginCall.result);

  const getStatusIcon = () => {
    if (isLoading) return <LoadingOutlined spin style={{ color: "#1890ff" }} />;
    if (isError) return <CloseCircleOutlined style={{ color: "#ff4d4f" }} />;
    if (isComplete) return <CheckCircleOutlined style={{ color: "#52c41a" }} />;
    return <ToolOutlined />;
  };

  const getStatusText = () => {
    if (isLoading) return "执行中...";
    if (isError) return "执行失败";
    if (isComplete) return "执行完成";
    return "";
  };

  const items = [
    {
      key: "1",
      label: (
        <span className="plugin-call-label">
          {getStatusIcon()}
          <span className="plugin-name">{pluginCall.pluginName || pluginCall.pluginKey || "未知插件"}</span>
          {pluginCall.pluginKey && pluginCall.pluginName && (
            <span className="plugin-key-badge">{pluginCall.pluginKey}</span>
          )}
          {(isLoading || isError) && (
            <span className={`plugin-status-text ${isError ? "error" : ""}`}>{getStatusText()}</span>
          )}
        </span>
      ),
      children: (
        <div className="plugin-call-details">
          {params && params.length > 0 && (
            <div className="plugin-detail-section">
              <Text type="secondary" strong>输入参数</Text>
              <div className="plugin-params-table">
                {params.map((param, idx) => (
                  <div key={idx} className="plugin-param-row">
                    <span className="plugin-param-key">{param.key}</span>
                    <span className="plugin-param-value">{param.value}</span>
                  </div>
                ))}
              </div>
            </div>
          )}
          {isLoading && (
            <div className="plugin-detail-section plugin-loading">
              <LoadingOutlined spin /> 正在执行插件...
            </div>
          )}
          {pluginCall.result && (
            <div className="plugin-detail-section">
              <Text type="secondary" strong>输出结果</Text>
              {parsedResult ? (
                <pre className="plugin-code-block">{JSON.stringify(parsedResult, null, 2)}</pre>
              ) : (
                <pre className="plugin-code-block">{pluginCall.result}</pre>
              )}
            </div>
          )}
        </div>
      ),
    },
  ];

  return <Collapse size="small" items={items} className={`plugin-call-collapse ${isLoading ? "loading" : ""}`} defaultActiveKey={isLoading ? ["1"] : []} />;
};

export default function AppChatPage() {
  const { id, appId } = useParams();
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const chatContainerRef = useRef<HTMLDivElement>(null);
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);

  const teamId = parseInt(id || "0");

  // 应用信息
  const [appInfo, setAppInfo] = useState<{ name: string; avatar: string | null }>({ name: "", avatar: null });
  const [pageLoading, setPageLoading] = useState(true);

  // 话题列表
  const [topics, setTopics] = useState<AppChatTopicItem[]>([]);
  const [currentChatId, setCurrentChatId] = useState<string | null>(null);
  const [topicsLoading, setTopicsLoading] = useState(false);

  // 对话消息
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [inputValue, setInputValue] = useState("");
  const [sending, setSending] = useState(false);
  const [streamingContent, setStreamingContent] = useState("");
  const [streamingPluginCalls, setStreamingPluginCalls] = useState<Map<string, StreamingPluginCall>>(new Map());
  const [isStreaming, setIsStreaming] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);

  // 编辑标题
  const [editingTopicId, setEditingTopicId] = useState<string | null>(null);
  const [editingTitle, setEditingTitle] = useState("");

  useEffect(() => {
    if (teamId && appId) {
      fetchAppInfo();
      fetchTopics();
    }
  }, [teamId, appId]);

  const fetchAppInfo = async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.app.common.app_list.get({
        queryParameters: { teamId },
      });
      const app = response?.items?.find((a: any) => a.id === appId);
      if (app) {
        setAppInfo({ name: app.name || "应用", avatar: app.avatar || null });
      }
    } catch (error) {
      console.error("获取应用信息失败:", error);
    } finally {
      setPageLoading(false);
    }
  };

  const fetchTopics = async () => {
    try {
      setTopicsLoading(true);
      const client = GetApiClient();
      const response = await client.api.app.common.topic_list.post({
        teamId,
        appId,
      });
      setTopics(response?.items || []);
    } catch (error) {
      console.error("获取话题列表失败:", error);
      proxyRequestError(error, messageApi, "获取话题列表失败");
    } finally {
      setTopicsLoading(false);
    }
  };

  const fetchChatHistory = async (chatId: string) => {
    try {
      const client = GetApiClient();
      const response = await client.api.app.common.chat_history.get({
        queryParameters: { teamId, appId, chatId },
      });
      if (response?.chatHistory) {
        const msgs: ChatMessage[] = response.chatHistory.map((item: AppChatHistoryItem) => {
          const role = item.authorName === "user" ? "user" : item.authorName === "tool" ? "tool" : "assistant";
          let content = "";
          const pluginCalls: StreamingPluginCall[] = [];
          
          if (item.choices) {
            item.choices.forEach((choice: AiProcessingChoice) => {
              if (choice.textCall?.content) {
                content += choice.textCall.content;
              }
              if (choice.pluginCall) {
                pluginCalls.push({ ...choice.pluginCall, streamState: "end" });
              }
            });
          }
          
          return { role, content, pluginCalls: pluginCalls.length > 0 ? pluginCalls : undefined };
        });
        setMessages(msgs);
      }
    } catch (error) {
      console.error("获取对话历史失败:", error);
      proxyRequestError(error, messageApi, "获取对话历史失败");
    }
  };

  const handleSelectTopic = (chatId: string) => {
    setCurrentChatId(chatId);
    setMessages([]);
    fetchChatHistory(chatId);
  };

  const handleNewChat = async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.app.common.create_chat.post({
        teamId,
        appId,
        title: "新对话",
      });
      if (response?.chatId) {
        setCurrentChatId(response.chatId);
        setMessages([]);
        fetchTopics();
      }
    } catch (error) {
      console.error("创建对话失败:", error);
      proxyRequestError(error, messageApi, "创建对话失败");
    }
  };

  const handleDeleteTopic = useCallback((chatId: string) => {
    Modal.confirm({
      title: "确认删除",
      content: "确定要删除这个对话吗？",
      maskClosable: false,
      okText: "确认删除",
      cancelText: "取消",
      okButtonProps: { danger: true },
      onOk: async () => {
        try {
          const token = localStorage.getItem("userinfo.accessToken");
          const response = await fetch(`${EnvOptions.ServerUrl}/api/app/common/delete_chat`, {
            method: "DELETE",
            headers: {
              "Content-Type": "application/json",
              Authorization: token ? `Bearer ${token}` : "",
            },
            body: JSON.stringify({ chatId }),
          });

          if (!response.ok) {
            throw new Error(`HTTP error! status: ${response.status}`);
          }

          if (currentChatId === chatId) {
            setCurrentChatId(null);
            setMessages([]);
          }
          await fetchTopics();
          messageApi.success("删除成功");
        } catch (error) {
          console.error("删除对话失败:", error);
          proxyRequestError(error, messageApi, "删除对话失败");
        }
      },
    });
  }, [currentChatId, messageApi]);

  const handleUpdateTitle = async (chatId: string) => {
    if (!editingTitle.trim()) {
      setEditingTopicId(null);
      return;
    }
    try {
      const client = GetApiClient();
      await client.api.app.common.update_chat_title.post({
        teamId,
        appId,
        chatId,
        title: editingTitle.trim(),
      });
      setEditingTopicId(null);
      fetchTopics();
    } catch (error) {
      console.error("更新标题失败:", error);
      proxyRequestError(error, messageApi, "更新标题失败");
    }
  };

  const scrollToBottom = useCallback(() => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTop = chatContainerRef.current.scrollHeight;
    }
  }, []);

  const handleSendMessage = async () => {
    if (!inputValue.trim() || sending) return;

    let chatId = currentChatId;
    if (!chatId) {
      try {
        const client = GetApiClient();
        const response = await client.api.app.common.create_chat.post({
          teamId,
          appId,
          title: inputValue.trim().slice(0, 20),
        });
        if (response?.chatId) {
          chatId = response.chatId;
          setCurrentChatId(chatId);
          fetchTopics();
        } else {
          messageApi.error("创建对话失败");
          return;
        }
      } catch (error) {
        console.error("创建对话失败:", error);
        proxyRequestError(error, messageApi, "创建对话失败");
        return;
      }
    }

    const userMessage = inputValue.trim();
    setInputValue("");
    setSending(true);
    setIsStreaming(true);
    setStreamingContent("");

    setMessages((prev) => [...prev, { role: "user", content: userMessage }]);
    setTimeout(scrollToBottom, 50);

    abortControllerRef.current = new AbortController();

    try {
      const token = localStorage.getItem("userinfo.accessToken");
      const response = await fetch(`${EnvOptions.ServerUrl}/api/app/common/completions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: token ? `Bearer ${token}` : "",
        },
        body: JSON.stringify({
          teamId,
          appId,
          chatId,
          question: userMessage,
        }),
        signal: abortControllerRef.current.signal,
      });

      if (!response.ok) {
        throw new Error(`HTTP error! status: ${response.status}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error("无法获取响应流");
      }

      const decoder = new TextDecoder();
      let accumulatedContent = "";
      let buffer = "";
      let messageAdded = false;
      const pluginCallsMap = new Map<string, StreamingPluginCall>();

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() || "";

        for (const line of lines) {
          if (line.startsWith("data:")) {
            const jsonStr = line.slice(5).trim();
            if (jsonStr === "[DONE]") continue;

            try {
              const data = JSON.parse(jsonStr);
              const isFinished = data.finish_reason === "stop" || data.finishReason === "stop";
              const isError = data.finish_reason === "error" || data.finishReason === "error";

              if (!isFinished && !isError && data.choices && data.choices.length > 0) {
                for (const choice of data.choices) {
                  if (choice.textCall?.content) {
                    accumulatedContent += choice.textCall.content;
                    setStreamingContent(accumulatedContent);
                    scrollToBottom();
                  }

                  if (choice.pluginCall || choice.streamType === "plugin") {
                    const pluginId = choice.id || choice.pluginCall?.pluginKey || `plugin-${Date.now()}`;
                    const existingCall = pluginCallsMap.get(pluginId);

                    if (existingCall) {
                      if (choice.pluginCall) {
                        if (choice.pluginCall.pluginName) existingCall.pluginName = choice.pluginCall.pluginName;
                        if (choice.pluginCall.pluginKey) existingCall.pluginKey = choice.pluginCall.pluginKey;
                        if (choice.pluginCall.params) existingCall.params = choice.pluginCall.params;
                        if (choice.pluginCall.result) existingCall.result = choice.pluginCall.result;
                      }
                      if (choice.streamState) existingCall.streamState = choice.streamState;
                    } else {
                      const newCall: StreamingPluginCall = {
                        ...choice.pluginCall,
                        streamState: choice.streamState || "start",
                      };
                      pluginCallsMap.set(pluginId, newCall);
                    }

                    setStreamingPluginCalls(new Map(pluginCallsMap));
                    scrollToBottom();
                  }
                }
              }

              if (isFinished) {
                setIsStreaming(false);
                const finalPluginCalls = Array.from(pluginCallsMap.values());
                if ((accumulatedContent || finalPluginCalls.length > 0) && !messageAdded) {
                  setMessages((prev) => [
                    ...prev,
                    {
                      role: "assistant",
                      content: accumulatedContent,
                      pluginCalls: finalPluginCalls.length > 0 ? finalPluginCalls : undefined,
                    },
                  ]);
                  messageAdded = true;
                }
                setStreamingContent("");
                setStreamingPluginCalls(new Map());
              }

              if (isError) {
                setIsStreaming(false);
                setStreamingContent("");
                setStreamingPluginCalls(new Map());
                messageAdded = true;
                let errorMessage = "AI 处理出错";
                if (data.choices && data.choices.length > 0) {
                  for (const choice of data.choices) {
                    if (choice.textCall?.content) {
                      errorMessage = choice.textCall.content;
                      break;
                    }
                  }
                }
                messageApi.error(errorMessage);
              }
            } catch (parseError) {
              console.debug("Parse error:", parseError, "for line:", jsonStr);
            }
          }
        }
      }

      const finalPluginCalls = Array.from(pluginCallsMap.values());
      if (!messageAdded && (accumulatedContent || finalPluginCalls.length > 0)) {
        setIsStreaming(false);
        setMessages((prev) => [
          ...prev,
          {
            role: "assistant",
            content: accumulatedContent,
            pluginCalls: finalPluginCalls.length > 0 ? finalPluginCalls : undefined,
          },
        ]);
        setStreamingContent("");
        setStreamingPluginCalls(new Map());
      }
    } catch (error) {
      if ((error as Error).name === "AbortError") {
        console.log("请求已取消");
      } else {
        console.error("发送消息失败:", error);
        proxyRequestError(error, messageApi, "发送消息失败");
      }
      setIsStreaming(false);
      setStreamingContent("");
      setStreamingPluginCalls(new Map());
    } finally {
      setSending(false);
      abortControllerRef.current = null;
    }
  };

  const handleCancelStream = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      setIsStreaming(false);
      setStreamingContent("");
      setStreamingPluginCalls(new Map());
      setSending(false);
    }
  };

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  const getRoleAvatar = (role: string) => {
    switch (role) {
      case "user":
        return userDetailInfo?.avatar ? (
          <Avatar src={userDetailInfo.avatar} className="user-avatar" />
        ) : (
          <Avatar icon={<UserOutlined />} className="user-avatar" />
        );
      case "assistant":
        return appInfo.avatar ? (
          <Avatar src={appInfo.avatar} className="assistant-avatar" />
        ) : (
          <Avatar icon={<RobotOutlined />} className="assistant-avatar" />
        );
      case "tool":
        return <Avatar icon={<ToolOutlined />} className="tool-avatar" />;
      default:
        return <Avatar>{role.charAt(0).toUpperCase()}</Avatar>;
    }
  };

  const getRoleDisplayName = (role: string) => {
    switch (role) {
      case "user":
        return "用户";
      case "assistant":
        return appInfo.name || "助手";
      case "tool":
        return "工具";
      default:
        return role;
    }
  };

  if (pageLoading) {
    return (
      <div className="app-chat-page">
        <div className="app-chat-loading">
          <Spin size="large" />
        </div>
      </div>
    );
  }

  return (
    <div className="app-chat-page">
      {contextHolder}
      {/* 左侧话题列表 */}
      <div className="app-chat-sidebar">
        <div className="app-chat-sidebar-header">
          <Button
            type="text"
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate(`/app/team/${teamId}/appstore`)}
          >
            返回
          </Button>
          <span className="app-chat-sidebar-title">{appInfo.name}</span>
        </div>
        <div className="app-chat-sidebar-actions">
          <Button type="primary" icon={<PlusOutlined />} block onClick={handleNewChat}>
            新对话
          </Button>
        </div>
        <div className="app-chat-topic-list">
          {topicsLoading ? (
            <div className="app-chat-topic-loading">
              <Spin size="small" />
            </div>
          ) : topics.length === 0 ? (
            <div className="app-chat-topic-empty">暂无对话</div>
          ) : (
            topics.map((topic) => (
              <div
                key={topic.chatId}
                className={`app-chat-topic-item ${currentChatId === topic.chatId ? "active" : ""}`}
                onClick={() => handleSelectTopic(topic.chatId!)}
              >
                {editingTopicId === topic.chatId ? (
                  <Input
                    size="small"
                    value={editingTitle}
                    onChange={(e) => setEditingTitle(e.target.value)}
                    onBlur={() => handleUpdateTitle(topic.chatId!)}
                    onPressEnter={() => handleUpdateTitle(topic.chatId!)}
                    autoFocus
                    onClick={(e) => e.stopPropagation()}
                  />
                ) : (
                  <>
                    <span className="app-chat-topic-title">{topic.title || "未命名对话"}</span>
                    <div className="app-chat-topic-actions">
                      <Button
                        type="text"
                        size="small"
                        icon={<EditOutlined />}
                        onClick={(e) => {
                          e.stopPropagation();
                          setEditingTopicId(topic.chatId!);
                          setEditingTitle(topic.title || "");
                        }}
                      />
                      <Button
                        type="text"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                        onClick={(e) => {
                          e.stopPropagation();
                          handleDeleteTopic(topic.chatId!);
                        }}
                      />
                    </div>
                  </>
                )}
              </div>
            ))
          )}
        </div>
      </div>

      {/* 右侧对话区域 */}
      <div className="app-chat-main">
        <div className="app-chat-history" ref={chatContainerRef}>
          {messages.length === 0 && !isStreaming ? (
            <div className="app-chat-empty">
              <Empty description="发送消息开始对话" />
            </div>
          ) : (
            <div className="app-chat-messages">
              {messages.map((msg, index) => (
                <div key={index} className={`chat-message chat-message-${msg.role}`}>
                  <div className="message-avatar">{getRoleAvatar(msg.role)}</div>
                  <div className="message-body">
                    <div className="message-header">
                      <Text strong className="message-role">{getRoleDisplayName(msg.role)}</Text>
                    </div>
                    <div className="message-content">
                      {msg.pluginCalls && msg.pluginCalls.length > 0 && (
                        <div className="plugin-calls-container">
                          {msg.pluginCalls.map((pluginCall, idx) => (
                            <PluginCallDisplay key={idx} pluginCall={pluginCall} />
                          ))}
                        </div>
                      )}
                      {msg.content && (
                        <div className="message-markdown">
                          <Markdown fullFeaturedCodeBlock>{msg.content}</Markdown>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              ))}

              {isStreaming && (
                <div className="chat-message chat-message-assistant chat-message-streaming">
                  <div className="message-avatar">{getRoleAvatar("assistant")}</div>
                  <div className="message-body">
                    <div className="message-header">
                      <Text strong className="message-role">{getRoleDisplayName("assistant")}</Text>
                      <Text type="secondary" className="message-tag streaming-indicator">
                        <LoadingOutlined /> 正在输入...
                      </Text>
                    </div>
                    <div className="message-content">
                      {streamingPluginCalls.size > 0 && (
                        <div className="plugin-calls-container">
                          {Array.from(streamingPluginCalls.values()).map((pluginCall, idx) => (
                            <PluginCallDisplay key={idx} pluginCall={pluginCall} />
                          ))}
                        </div>
                      )}
                      {streamingContent ? (
                        <div className="message-markdown">
                          <Markdown fullFeaturedCodeBlock>{streamingContent}</Markdown>
                          <span className="typing-cursor">|</span>
                        </div>
                      ) : streamingPluginCalls.size === 0 ? (
                        <div className="message-thinking">
                          <LoadingOutlined /> 思考中...
                        </div>
                      ) : null}
                    </div>
                  </div>
                </div>
              )}
            </div>
          )}
        </div>

        <div className="app-chat-input-area">
          <div className="app-chat-input-wrapper">
            <TextArea
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="输入消息，按 Enter 发送，Shift+Enter 换行..."
              disabled={sending}
              className="app-chat-input"
            />
            <div className="app-chat-input-actions">
              <span />
              {isStreaming ? (
                <Button danger onClick={handleCancelStream}>停止</Button>
              ) : (
                <Button
                  type="primary"
                  icon={<SendOutlined />}
                  onClick={handleSendMessage}
                  loading={sending}
                  disabled={!inputValue.trim()}
                >
                  发送
                </Button>
              )}
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
