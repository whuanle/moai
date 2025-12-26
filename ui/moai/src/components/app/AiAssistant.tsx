import React, { useState, useEffect, useCallback, useRef } from "react";
import { useParams, useNavigate } from "react-router";
import {
  Layout,
  List,
  Button,
  Typography,
  Popconfirm,
  message,
  Spin,
  Empty,
  Input,
  Avatar,
  Collapse,
  Modal,
  Select,
} from "antd";
import {
  PlusOutlined,
  DeleteOutlined,
  MessageOutlined,
  ArrowLeftOutlined,
  SendOutlined,
  UserOutlined,
  RobotOutlined,
  ToolOutlined,
  LoadingOutlined,
} from "@ant-design/icons";
import { EmojiPicker } from "@lobehub/ui";
import ReactMarkdown from "react-markdown";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import { EnvOptions } from "../../Env";
import type {
  AiAssistantChatTopic,
  ChatContentItem,
  AiProcessingPluginCall,
  QueryAiAssistantChatHistoryCommandResponse,
  PublicModelInfo,
} from "../../apiClient/models";
import { AiModelTypeObject } from "../../apiClient/models";
import AssistantConfigPanel, { AssistantConfig } from "./AssistantConfigPanel";
import "./AiAssistant.css";

const { Sider, Content } = Layout;
const { Text, Title } = Typography;
const { TextArea } = Input;

// 从 ChatContentItem 中提取文本消息内容（不包含插件调用）
const extractTextContent = (item: ChatContentItem): string => {
  const choices = item.choices;
  
  if (!choices || choices.length === 0) {
    console.log("Empty choices for item:", item);
    return "";
  }
  
  const contents: string[] = [];
  
  for (const choice of choices) {
    if (choice.textCall?.content) {
      contents.push(choice.textCall.content);
    }
  }
  
  return contents.join("");
};

// 从 ChatContentItem 中提取插件调用
const extractPluginCalls = (item: ChatContentItem): AiProcessingPluginCall[] => {
  const choices = item.choices;
  if (!choices || choices.length === 0) return [];
  
  return choices
    .filter((choice) => choice.pluginCall != null)
    .map((choice) => choice.pluginCall!);
};

// 判断消息是否包含插件调用
const hasPluginCall = (item: ChatContentItem): boolean => {
  if (!item.choices) return false;
  return item.choices.some((choice) => choice.pluginCall != null);
};

// 尝试解析 JSON 字符串
const tryParseJson = (str: string | null | undefined): Record<string, unknown> | null => {
  if (!str) return null;
  try {
    return JSON.parse(str);
  } catch {
    return null;
  }
};

// 插件调用组件
const PluginCallDisplay: React.FC<{ pluginCall: AiProcessingPluginCall }> = ({ pluginCall }) => {
  const params = pluginCall.params; // KeyValueString[] 类型
  const parsedResult = tryParseJson(pluginCall.result);

  const items = [
    {
      key: "1",
      label: (
        <span className="plugin-call-label">
          <ToolOutlined style={{ marginRight: 8 }} />
          <span className="plugin-name">{pluginCall.pluginName || pluginCall.pluginKey || "未知插件"}</span>
          {pluginCall.pluginKey && pluginCall.pluginName && (
            <span className="plugin-key-badge">{pluginCall.pluginKey}</span>
          )}
        </span>
      ),
      children: (
        <div className="plugin-call-details">
          {/* 输入参数 - 键值对形式 */}
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
          
          {/* 输出结果 */}
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
          
          {/* 错误信息 */}
          {pluginCall.message && (
            <div className="plugin-detail-section">
              <Text type="warning" strong>信息</Text>
              <div className="plugin-message">{pluginCall.message}</div>
            </div>
          )}
        </div>
      ),
    },
  ];

  return (
    <Collapse
      size="small"
      items={items}
      className="plugin-call-collapse"
    />
  );
};

const AiAssistant: React.FC = () => {
  const { chatId: urlChatId } = useParams<{ chatId?: string }>();
  const navigate = useNavigate();
  const [messageApi, contextHolder] = message.useMessage();
  const chatContainerRef = useRef<HTMLDivElement>(null);

  // 话题列表状态
  const [topics, setTopics] = useState<AiAssistantChatTopic[]>([]);
  const [topicsLoading, setTopicsLoading] = useState(false);
  const [currentChatId, setCurrentChatId] = useState<string | null>(null);

  // 对话历史状态
  const [chatHistory, setChatHistory] = useState<ChatContentItem[]>([]);
  const [historyLoading, setHistoryLoading] = useState(false);

  // 输入框状态
  const [inputValue, setInputValue] = useState("");
  const [sending, setSending] = useState(false);

  // 流式消息状态
  const [streamingContent, setStreamingContent] = useState<string>("");
  const [isStreaming, setIsStreaming] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);

  // 助手配置状态
  const [assistantConfig, setAssistantConfig] = useState<AssistantConfig>({
    title: "",
    avatar: "🤖",
    systemPrompt: "你是智能助手",
    temperature: 1,
    topP: 1,
    presencePenalty: 0,
    frequencyPenalty: 0,
    selectedWikiIds: [],
    selectedPluginIds: [],
  });

  // Token 统计
  const [tokenStats, setTokenStats] = useState({
    total: 0,
    input: 0,
    output: 0,
  });

  // 新建对话模态窗口状态
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createModalLoading, setCreateModalLoading] = useState(false);
  const [models, setModels] = useState<PublicModelInfo[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [selectedModelId, setSelectedModelId] = useState<number | undefined>();
  const [newChatTitle, setNewChatTitle] = useState("新对话");

  // 滚动到底部
  const scrollToBottom = useCallback(() => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTop = chatContainerRef.current.scrollHeight;
    }
  }, []);

  // 加载话题列表
  const loadTopics = useCallback(async () => {
    setTopicsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.topic_list.get();
      if (response?.items) {
        setTopics(response.items.filter((t) => t.chatId));
      } else {
        setTopics([]);
      }
    } catch (error) {
      console.error("加载话题列表失败:", error);
      proxyRequestError(error, messageApi, "加载话题列表失败");
    } finally {
      setTopicsLoading(false);
    }
  }, [messageApi]);

  // 加载模型列表
  const loadModels = useCallback(async () => {
    setModelsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.aimodel.modellist.post({
        aiModelType: AiModelTypeObject.Chat,
      });
      if (response?.aiModels) {
        setModels(response.aiModels);
      }
    } catch (error) {
      console.error("加载模型列表失败:", error);
      proxyRequestError(error, messageApi, "加载模型列表失败");
    } finally {
      setModelsLoading(false);
    }
  }, [messageApi]);

  // 加载对话历史并更新配置
  const loadChatHistory = useCallback(
    async (chatId: string) => {
      setHistoryLoading(true);
      try {
        const client = GetApiClient();
        const response: QueryAiAssistantChatHistoryCommandResponse | undefined = 
          await client.api.app.assistant.chat_history.get({
            queryParameters: { chatId },
          });
        
        if (response) {
          // 调试：打印响应数据
          console.log("Chat history response:", response);
          console.log("Chat history items:", response.chatHistory);
          
          // 设置聊天历史
          setChatHistory(response.chatHistory || []);
          
          // 从响应中提取配置并更新
          const executionSettings = response.executionSettings || [];
          const getSettingValue = (key: string, defaultValue: number): number => {
            const setting = executionSettings.find((s) => s.key === key);
            return setting?.value ? parseFloat(setting.value) : defaultValue;
          };

          setAssistantConfig((prev) => ({
            ...prev,
            modelId: response.modelId || prev.modelId,
            title: response.title || prev.title,
            avatar: response.avatar || prev.avatar || "🤖",
            systemPrompt: response.prompt || "你是智能助手",
            temperature: getSettingValue("temperature", prev.temperature),
            topP: getSettingValue("top_p", prev.topP),
            presencePenalty: getSettingValue("presence_penalty", prev.presencePenalty),
            frequencyPenalty: getSettingValue("frequency_penalty", prev.frequencyPenalty),
            selectedWikiIds: response.wikiIds || [],
            selectedPluginIds: response.plugins || [],
          }));

          // 更新 Token 统计
          if (response.tokenUsage) {
            setTokenStats({
              total: (response.tokenUsage.promptTokens || 0) + (response.tokenUsage.completionTokens || 0),
              input: response.tokenUsage.promptTokens || 0,
              output: response.tokenUsage.completionTokens || 0,
            });
          }

          // 滚动到底部
          setTimeout(scrollToBottom, 100);
        } else {
          setChatHistory([]);
        }
      } catch (error) {
        console.error("加载对话历史失败:", error);
        proxyRequestError(error, messageApi, "加载对话历史失败");
      } finally {
        setHistoryLoading(false);
      }
    },
    [messageApi, scrollToBottom]
  );

  // 打开新建对话模态窗口
  const handleOpenCreateModal = () => {
    setNewChatTitle("新对话");
    setSelectedModelId(undefined);
    setCreateModalOpen(true);
    if (models.length === 0) {
      loadModels();
    }
  };

  // 创建新话题
  const handleCreateTopic = async () => {
    if (!selectedModelId) {
      messageApi.warning("请选择AI模型");
      return;
    }

    setCreateModalLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.create_chat.post({
        title: newChatTitle || "新对话",
        modelId: selectedModelId,
      });
      if (response?.chatId) {
        await loadTopics();
        const newChatId = response.chatId;
        setCurrentChatId(newChatId);
        navigate(`/chat/${newChatId}`);
        setChatHistory([]);
        setTokenStats({ total: 0, input: 0, output: 0 });
        setCreateModalOpen(false);
        messageApi.success("创建新对话成功");
        // 加载新对话的配置
        await loadChatHistory(newChatId);
      }
    } catch (error) {
      console.error("创建新对话失败:", error);
      proxyRequestError(error, messageApi, "创建新对话失败");
    } finally {
      setCreateModalLoading(false);
    }
  };

  // 删除话题
  const handleDeleteTopic = async (chatId: string) => {
    try {
      const client = GetApiClient();
      await client.api.app.assistant.delete_chat.delete({ chatId });
      await loadTopics();
      if (currentChatId === chatId) {
        setCurrentChatId(null);
        setChatHistory([]);
        setTokenStats({ total: 0, input: 0, output: 0 });
        // 重置助手配置
        setAssistantConfig({
          title: "",
          avatar: "🤖",
          systemPrompt: "你是智能助手",
          temperature: 1,
          topP: 1,
          presencePenalty: 0,
          frequencyPenalty: 0,
          selectedWikiIds: [],
          selectedPluginIds: [],
        });
        navigate("/chat");
      }
      messageApi.success("删除对话成功");
    } catch (error) {
      console.error("删除对话失败:", error);
      proxyRequestError(error, messageApi, "删除对话失败");
    }
  };

  // 选择话题
  const handleSelectTopic = (chatId: string) => {
    if (chatId === currentChatId) return;
    setCurrentChatId(chatId);
    navigate(`/chat/${chatId}`);
    loadChatHistory(chatId);
  };

  // 发送消息 - 流式对话
  const handleSendMessage = async () => {
    if (!inputValue.trim() || !currentChatId || sending) return;
    
    if (!assistantConfig.modelId) {
      messageApi.warning("请先选择AI模型");
      return;
    }

    const userMessage = inputValue.trim();
    setInputValue("");
    setSending(true);
    setIsStreaming(true);
    setStreamingContent("");

    // 先添加用户消息到聊天历史
    const userChatItem: ChatContentItem = {
      authorName: "user",
      choices: [{ textCall: { content: userMessage } }],
    };
    setChatHistory((prev) => [...prev, userChatItem]);
    setTimeout(scrollToBottom, 50);

    // 创建 AbortController 用于取消请求
    abortControllerRef.current = new AbortController();

    try {
      const token = localStorage.getItem("userinfo.accessToken");
      const response = await fetch(`${EnvOptions.ServerUrl}/api/app/assistant/completions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          "Authorization": token ? `Bearer ${token}` : "",
        },
        body: JSON.stringify({
          chatId: currentChatId,
          content: userMessage,
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

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        
        // 处理 SSE 格式的数据
        const lines = buffer.split("\n");
        buffer = lines.pop() || ""; // 保留未完成的行

        for (const line of lines) {
          if (line.startsWith("data:")) {
            const jsonStr = line.slice(5).trim();
            if (jsonStr === "[DONE]") {
              continue;
            }
            
            try {
              const data = JSON.parse(jsonStr);
              
              // 处理流式内容
              if (data.choices && data.choices.length > 0) {
                for (const choice of data.choices) {
                  if (choice.textCall?.content) {
                    accumulatedContent += choice.textCall.content;
                    setStreamingContent(accumulatedContent);
                    scrollToBottom();
                  }
                }
              }
              
              // 检查是否完成
              if (data.finish_reason === "stop" || data.finishReason === "stop") {
                // 流式完成，刷新对话历史
                setIsStreaming(false);
                setStreamingContent("");
                await loadChatHistory(currentChatId);
                
                // 更新 Token 统计
                if (data.usage) {
                  setTokenStats({
                    total: (data.usage.prompt_tokens || data.usage.promptTokens || 0) + 
                           (data.usage.completion_tokens || data.usage.completionTokens || 0),
                    input: data.usage.prompt_tokens || data.usage.promptTokens || 0,
                    output: data.usage.completion_tokens || data.usage.completionTokens || 0,
                  });
                }
              }
            } catch (parseError) {
              // 忽略解析错误，可能是不完整的 JSON
              console.debug("Parse error:", parseError, "for line:", jsonStr);
            }
          }
        }
      }

      // 如果流结束但没有收到 stop 信号，也刷新历史
      if (isStreaming) {
        setIsStreaming(false);
        setStreamingContent("");
        await loadChatHistory(currentChatId);
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
    } finally {
      setSending(false);
      abortControllerRef.current = null;
    }
  };

  // 取消流式请求
  const handleCancelStream = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      setIsStreaming(false);
      setStreamingContent("");
      setSending(false);
    }
  };

  // 处理头像选择
  const handleAvatarChange = async (emoji: string) => {
    const newConfig = { ...assistantConfig, avatar: emoji };
    setAssistantConfig(newConfig);
    
    // 自动保存头像到服务器（带上完整配置）
    if (currentChatId) {
      try {
        const client = GetApiClient();
        
        // 构建 executionSettings
        const executionSettings = [
          { key: "temperature", value: String(newConfig.temperature) },
          { key: "top_p", value: String(newConfig.topP) },
          { key: "presence_penalty", value: String(newConfig.presencePenalty) },
          { key: "frequency_penalty", value: String(newConfig.frequencyPenalty) },
        ];

        await client.api.app.assistant.update_chat.post({
          chatId: currentChatId,
          modelId: newConfig.modelId,
          title: newConfig.title,
          avatar: emoji,
          prompt: newConfig.systemPrompt,
          executionSettings: executionSettings,
          wikiIds: newConfig.selectedWikiIds,
          plugins: newConfig.selectedPluginIds,
        });
      } catch (error) {
        console.error("保存头像失败:", error);
        proxyRequestError(error, messageApi, "保存头像失败");
      }
    }
  };

  // 处理按键事件
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSendMessage();
    }
  };

  // 获取角色头像
  const getRoleAvatar = (authorName?: string | null) => {
    switch (authorName) {
      case "user":
        return <Avatar icon={<UserOutlined />} style={{ backgroundColor: "#1890ff" }} />;
      case "assistant":
        // 使用配置的 avatar，如果为空则使用默认图标
        if (assistantConfig.avatar) {
          return <Avatar style={{ backgroundColor: "#52c41a", fontSize: 20 }}>{assistantConfig.avatar}</Avatar>;
        }
        return <Avatar icon={<RobotOutlined />} style={{ backgroundColor: "#52c41a" }} />;
      case "tool":
        return <Avatar icon={<ToolOutlined />} style={{ backgroundColor: "#faad14" }} />;
      case "system":
        return <Avatar style={{ backgroundColor: "#722ed1" }}>S</Avatar>;
      default:
        return <Avatar>{authorName?.charAt(0).toUpperCase() || "?"}</Avatar>;
    }
  };

  // 获取角色显示名称
  const getRoleDisplayName = (authorName?: string | null) => {
    switch (authorName) {
      case "user":
        return "用户";
      case "assistant":
        return "助手";
      case "tool":
        return "工具";
      case "system":
        return "系统";
      default:
        return authorName || "未知";
    }
  };

  // 初始化：加载话题列表
  useEffect(() => {
    loadTopics();
  }, [loadTopics]);

  // 根据 URL 参数自动切换对话
  useEffect(() => {
    if (urlChatId && urlChatId !== currentChatId) {
      setCurrentChatId(urlChatId);
      loadChatHistory(urlChatId);
    } else if (!urlChatId && currentChatId) {
      // URL 中没有 chatId，说明是删除后跳转或直接访问 /chat
      // 不需要做任何操作，状态已在 handleDeleteTopic 中处理
    }
  }, [urlChatId]); // 移除 currentChatId 和 loadChatHistory 依赖，避免删除后重复请求

  return (
    <Layout className="ai-assistant-layout">
      {contextHolder}
      {/* 左侧：话题列表 */}
      <Sider width={280} className="ai-assistant-sider-left">
        <div className="back-button-area">
          <Button
            type="text"
            icon={<ArrowLeftOutlined />}
            onClick={() => navigate("/app")}
          >
            返回
          </Button>
        </div>
        <div className="topic-list-header">
          <Text strong>对话列表</Text>
          <Button
            type="primary"
            icon={<PlusOutlined />}
            size="small"
            onClick={handleOpenCreateModal}
          >
            新对话
          </Button>
        </div>
        <div className="topic-list-content">
          <Spin spinning={topicsLoading}>
            {topics.length === 0 && !topicsLoading ? (
              <Empty description="暂无对话" />
            ) : (
              <List
                dataSource={topics}
                renderItem={(topic) => (
                  <List.Item
                    className={`topic-item ${
                      currentChatId === topic.chatId ? "topic-item-active" : ""
                    }`}
                    onClick={() => handleSelectTopic(topic.chatId!)}
                    actions={[
                      <Popconfirm
                        key="delete"
                        title="确定删除此对话？"
                        onConfirm={(e) => {
                          e?.stopPropagation();
                          handleDeleteTopic(topic.chatId!);
                        }}
                        onCancel={(e) => e?.stopPropagation()}
                        okText="确定"
                        cancelText="取消"
                      >
                        <Button
                          type="text"
                          size="small"
                          danger
                          icon={<DeleteOutlined />}
                          onClick={(e) => e.stopPropagation()}
                        />
                      </Popconfirm>,
                    ]}
                  >
                    <List.Item.Meta
                      avatar={<MessageOutlined className="topic-icon" />}
                      title={
                        <Text ellipsis className="topic-title">
                          {topic.title || "未命名对话"}
                        </Text>
                      }
                      description={
                        <Text type="secondary" className="topic-time">
                          {topic.createTime}
                        </Text>
                      }
                    />
                  </List.Item>
                )}
              />
            )}
          </Spin>
        </div>
      </Sider>

      {/* 中间：对话内容区域 */}
      <Content className="ai-assistant-content">
        <div className="chat-container">
          {/* 对话标题头部 */}
          {currentChatId && (
            <div className="chat-header">
              <EmojiPicker
                value={assistantConfig.avatar || "🤖"}
                onChange={handleAvatarChange}
                size={32}
              />
              <Title level={4} className="chat-title">
                {assistantConfig.title || "未命名对话"}
              </Title>
            </div>
          )}
          
          {/* 聊天历史区域 */}
          <div className="chat-history-area" ref={chatContainerRef}>
            <Spin spinning={historyLoading}>
              {chatHistory.length === 0 && !isStreaming ? (
                <div className="chat-empty">
                  <Empty description="选择或创建一个对话开始聊天" />
                </div>
              ) : (
                <div className="chat-messages">
                  {chatHistory.map((item, index) => {
                    const textContent = extractTextContent(item);
                    const pluginCalls = extractPluginCalls(item);
                    const isPluginCall = hasPluginCall(item);
                    
                    // 跳过完全空的消息
                    if (!textContent && pluginCalls.length === 0) return null;
                    
                    return (
                      <div
                        key={item.recordId || index}
                        className={`chat-message chat-message-${item.authorName}`}
                      >
                        <div className="message-avatar">
                          {getRoleAvatar(item.authorName)}
                        </div>
                        <div className="message-body">
                          <div className="message-header">
                            <Text strong className="message-role">
                              {getRoleDisplayName(item.authorName)}
                            </Text>
                            {isPluginCall && (
                              <Text type="secondary" className="message-tag">
                                <ToolOutlined /> 插件调用
                              </Text>
                            )}
                          </div>
                          <div className="message-content">
                            {/* 显示插件调用 */}
                            {pluginCalls.length > 0 && (
                              <div className="plugin-calls-container">
                                {pluginCalls.map((pluginCall, idx) => (
                                  <PluginCallDisplay key={idx} pluginCall={pluginCall} />
                                ))}
                              </div>
                            )}
                            {/* 显示文本内容 */}
                            {textContent && (
                              item.authorName === "user" ? (
                                <div className="message-text">{textContent}</div>
                              ) : (
                                <div className="message-markdown">
                                  <ReactMarkdown>
                                    {textContent}
                                  </ReactMarkdown>
                                </div>
                              )
                            )}
                          </div>
                        </div>
                      </div>
                    );
                  })}
                  
                  {/* 流式消息显示 */}
                  {isStreaming && (
                    <div className="chat-message chat-message-assistant chat-message-streaming">
                      <div className="message-avatar">
                        {getRoleAvatar("assistant")}
                      </div>
                      <div className="message-body">
                        <div className="message-header">
                          <Text strong className="message-role">
                            {getRoleDisplayName("assistant")}
                          </Text>
                          <Text type="secondary" className="message-tag streaming-indicator">
                            <LoadingOutlined /> 正在输入...
                          </Text>
                        </div>
                        <div className="message-content">
                          {streamingContent ? (
                            <div className="message-markdown">
                              <ReactMarkdown>
                                {streamingContent}
                              </ReactMarkdown>
                              <span className="typing-cursor">|</span>
                            </div>
                          ) : (
                            <div className="message-thinking">
                              <LoadingOutlined /> 思考中...
                            </div>
                          )}
                        </div>
                      </div>
                    </div>
                  )}
                </div>
              )}
            </Spin>
          </div>

          {/* 输入框区域 */}
          <div className="chat-input-area">
            <div className="chat-input-wrapper">
              <TextArea
                value={inputValue}
                onChange={(e) => setInputValue(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder={
                  currentChatId
                    ? assistantConfig.modelId
                      ? "输入消息，按 Enter 发送，Shift+Enter 换行..."
                      : "请先在右侧选择AI模型"
                    : "请先选择或创建一个对话"
                }
                disabled={!currentChatId || sending}
                autoSize={{ minRows: 1, maxRows: 6 }}
                className="chat-input"
              />
              {isStreaming ? (
                <Button
                  danger
                  onClick={handleCancelStream}
                  className="chat-send-button"
                >
                  停止
                </Button>
              ) : (
                <Button
                  type="primary"
                  icon={<SendOutlined />}
                  onClick={handleSendMessage}
                  loading={sending}
                  disabled={!currentChatId || !inputValue.trim() || !assistantConfig.modelId}
                  className="chat-send-button"
                >
                  发送
                </Button>
              )}
            </div>
            {currentChatId && !assistantConfig.modelId && (
              <Text type="warning" className="chat-input-hint">
                请先在右侧面板选择AI模型才能开始对话
              </Text>
            )}
          </div>
        </div>
      </Content>

      {/* 右侧：配置面板 - 仅在选择话题后显示 */}
      {currentChatId && (
        <Sider width={320} className="ai-assistant-sider-right">
          <AssistantConfigPanel
            config={assistantConfig}
            onConfigChange={setAssistantConfig}
            chatId={currentChatId}
            tokenStats={tokenStats}
          />
        </Sider>
      )}

      {/* 新建对话模态窗口 */}
      <Modal
        title="新建对话"
        open={createModalOpen}
        onCancel={() => setCreateModalOpen(false)}
        maskClosable={false}
        onOk={handleCreateTopic}
        confirmLoading={createModalLoading}
        okText="创建"
        cancelText="取消"
        okButtonProps={{ disabled: !selectedModelId }}
      >
        <div className="create-chat-form">
          <div className="form-item">
            <Text strong>对话标题</Text>
            <Input
              placeholder="输入对话标题"
              value={newChatTitle}
              onChange={(e) => setNewChatTitle(e.target.value)}
              style={{ marginTop: 8 }}
            />
          </div>
          <div className="form-item" style={{ marginTop: 16 }}>
            <Text strong>
              选择模型 <span style={{ color: "red" }}>*</span>
            </Text>
            <Spin spinning={modelsLoading}>
              <Select
                placeholder="请选择AI模型"
                style={{ width: "100%", marginTop: 8 }}
                value={selectedModelId}
                onChange={(value) => setSelectedModelId(value)}
                options={models.map((m) => ({
                  label: m.title || m.name,
                  value: m.id,
                }))}
              />
            </Spin>
          </div>
        </div>
      </Modal>
    </Layout>
  );
};

export default AiAssistant;
