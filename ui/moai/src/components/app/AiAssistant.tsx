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
} from "@ant-design/icons";
import ReactMarkdown from "react-markdown";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import type {
  AiAssistantChatTopic,
  ChatContentItem,
  AiProcessingPluginCall,
  KeyValueString,
  QueryAiAssistantChatHistoryCommandResponse,
} from "../../apiClient/models";
import AssistantConfigPanel, { AssistantConfig } from "./AssistantConfigPanel";
import "./AiAssistant.css";

const { Sider, Content } = Layout;
const { Text } = Typography;
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

  // 助手配置状态
  const [assistantConfig, setAssistantConfig] = useState<AssistantConfig>({
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
            systemPrompt: response.prompt || prev.systemPrompt,
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

  // 创建新话题
  const handleCreateTopic = async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.create_chat.post({
        title: "新对话",
      });
      if (response?.chatId) {
        await loadTopics();
        const newChatId = response.chatId;
        setCurrentChatId(newChatId);
        navigate(`/chat/${newChatId}`);
        setChatHistory([]);
        setTokenStats({ total: 0, input: 0, output: 0 });
        messageApi.success("创建新对话成功");
      }
    } catch (error) {
      console.error("创建新对话失败:", error);
      proxyRequestError(error, messageApi, "创建新对话失败");
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

  // 发送消息（待实现完整功能）
  const handleSendMessage = async () => {
    if (!inputValue.trim() || !currentChatId || sending) return;
    
    if (!assistantConfig.modelId) {
      messageApi.warning("请先选择AI模型");
      return;
    }

    setSending(true);
    try {
      // TODO: 调用发送消息 API
      messageApi.info("发送消息功能待实现");
      setInputValue("");
    } catch (error) {
      console.error("发送消息失败:", error);
      proxyRequestError(error, messageApi, "发送消息失败");
    } finally {
      setSending(false);
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
    }
  }, [urlChatId, currentChatId, loadChatHistory]);

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
            onClick={handleCreateTopic}
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
          {/* 聊天历史区域 */}
          <div className="chat-history-area" ref={chatContainerRef}>
            <Spin spinning={historyLoading}>
              {chatHistory.length === 0 ? (
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
            </div>
            {currentChatId && !assistantConfig.modelId && (
              <Text type="warning" className="chat-input-hint">
                请先在右侧面板选择AI模型才能开始对话
              </Text>
            )}
          </div>
        </div>
      </Content>

      {/* 右侧：配置面板 */}
      <Sider width={320} className="ai-assistant-sider-right">
        <AssistantConfigPanel
          config={assistantConfig}
          onConfigChange={setAssistantConfig}
          tokenStats={tokenStats}
        />
      </Sider>
    </Layout>
  );
};

export default AiAssistant;
