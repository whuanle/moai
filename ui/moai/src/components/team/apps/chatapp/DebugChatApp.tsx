import { useState, useCallback, useRef, useEffect } from "react";
import { Button, Input, message, Empty, Typography, Avatar, Collapse } from "antd";
import {
  SendOutlined,
  UserOutlined,
  RobotOutlined,
  LoadingOutlined,
  ToolOutlined,
  CheckCircleOutlined,
  CloseCircleOutlined,
} from "@ant-design/icons";
import { Markdown } from "@lobehub/ui";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import { EnvOptions } from "../../../../Env";
import useAppStore from "../../../../stateshare/store";
import type { AiProcessingPluginCall, KeyValueString, AiProcessingChatStreamState, AppChatHistoryItem } from "../../../../apiClient/models";
import type { AppConfigData } from "./ChatAppConfigPanel";
import "./DebugChatApp.css";

const { TextArea } = Input;
const { Text } = Typography;

// 流式插件调用状态（用于合并同一 id 的流式数据）
interface StreamingPluginCall extends AiProcessingPluginCall {
  streamState?: AiProcessingChatStreamState | null;
}

// 调试对话消息类型
interface DebugMessage {
  role: "user" | "assistant" | "tool";
  content: string;
  pluginCalls?: StreamingPluginCall[];
}

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
const PluginCallDisplay: React.FC<{ pluginCall: StreamingPluginCall }> = ({ pluginCall }) => {
  const params = pluginCall.params;
  const parsedResult = tryParseJson(pluginCall.result);
  const isLoading = pluginCall.streamState === "start" || pluginCall.streamState === "processing";
  const isError = pluginCall.streamState === "error";
  const isComplete = pluginCall.streamState === "end" || (!isLoading && !isError && pluginCall.result);

  // 获取状态图标
  const getStatusIcon = () => {
    if (isLoading) return <LoadingOutlined spin style={{ color: "#1890ff" }} />;
    if (isError) return <CloseCircleOutlined style={{ color: "#ff4d4f" }} />;
    if (isComplete) return <CheckCircleOutlined style={{ color: "#52c41a" }} />;
    return <ToolOutlined />;
  };

  // 获取状态文本
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
          <span className="plugin-name">
            {pluginCall.pluginName || pluginCall.pluginKey || "未知插件"}
          </span>
          {pluginCall.pluginKey && pluginCall.pluginName && (
            <span className="plugin-key-badge">{pluginCall.pluginKey}</span>
          )}
          {(isLoading || isError) && (
            <span className={`plugin-status-text ${isError ? "error" : ""}`}>
              {getStatusText()}
            </span>
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

interface AppDebugChatProps {
  teamId: number;
  appId?: string;
  getConfig: () => AppConfigData;
  appAvatar?: string;
  initialHistory?: AppChatHistoryItem[];
  onHistoryCleared?: () => void;
}

export default function AppDebugChat({ teamId, appId, getConfig, appAvatar, initialHistory, onHistoryCleared }: AppDebugChatProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const chatContainerRef = useRef<HTMLDivElement>(null);
  
  // 获取当前用户信息
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);

  // 调试对话状态
  const [debugMessages, setDebugMessages] = useState<DebugMessage[]>([]);
  const [inputValue, setInputValue] = useState("");
  const [sending, setSending] = useState(false);
  const [streamingContent, setStreamingContent] = useState("");
  const [streamingPluginCalls, setStreamingPluginCalls] = useState<Map<string, StreamingPluginCall>>(new Map());
  const [isStreaming, setIsStreaming] = useState(false);
  const abortControllerRef = useRef<AbortController | null>(null);

  // 滚动到底部
  const scrollToBottom = useCallback(() => {
    if (chatContainerRef.current) {
      chatContainerRef.current.scrollTop = chatContainerRef.current.scrollHeight;
    }
  }, []);

  // 加载初始历史记录
  useEffect(() => {
    console.log("DebugChatApp 收到 initialHistory:", initialHistory);
    if (initialHistory && Array.isArray(initialHistory) && initialHistory.length > 0) {
      try {
        const messages: DebugMessage[] = initialHistory
          .filter((item) => {
            console.log("处理历史记录项:", item);
            return item && item.authorName && item.choices && item.choices.length > 0;
          })
          .map((item) => {
            // 从 choices 中提取内容
            const firstChoice = item.choices![0];
            const content = firstChoice.textCall?.content || "";
            const pluginCalls = firstChoice.pluginCall ? [firstChoice.pluginCall as StreamingPluginCall] : undefined;
            
            return {
              role: item.authorName === "user" ? "user" : "assistant",
              content,
              pluginCalls,
            };
          });
        console.log("转换后的消息:", messages);
        setDebugMessages(messages);
        setTimeout(scrollToBottom, 100);
      } catch (error) {
        console.error("加载历史记录失败:", error);
      }
    } else {
      console.log("initialHistory 为空或无效，清空消息列表");
      // 当 initialHistory 为空数组时，也清空消息列表
      setDebugMessages([]);
    }
  }, [initialHistory, scrollToBottom]);

  // 发送调试消息 - 流式对话
  const handleSendDebugMessage = async () => {
    const config = getConfig();
    console.log("开始发送消息，配置:", config);
    if (!inputValue.trim() || sending) return;

    const userMessage = inputValue.trim();
    console.log("用户消息:", userMessage);
    setInputValue("");
    setSending(true);
    setIsStreaming(true);
    setStreamingContent("");

    // 添加用户消息
    setDebugMessages((prev) => [...prev, { role: "user", content: userMessage }]);
    setTimeout(scrollToBottom, 50);

    // 创建 AbortController
    abortControllerRef.current = new AbortController();

    try {
      const token = localStorage.getItem("userinfo.accessToken");

      // 构建 executionSettings
      const executionSettings: KeyValueString[] = [
        { key: "temperature", value: String(config.temperature ?? 1) },
        { key: "top_p", value: String(config.topP ?? 1) },
        { key: "presence_penalty", value: String(config.presencePenalty ?? 0) },
        { key: "frequency_penalty", value: String(config.frequencyPenalty ?? 0) },
      ];

      const requestBody = {
        teamId,
        appId,
        question: userMessage,
        modelId: config.modelId,
        prompt: config.prompt,
        executionSettings,
        wikiIds: config.wikiIds,
        plugins: config.plugins,
      };
      console.log("发送请求体:", requestBody);

      // 使用 debug_completions API
      const response = await fetch(`${EnvOptions.ServerUrl}/api/app/team/chatapp/debug_completions`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: token ? `Bearer ${token}` : "",
        },
        body: JSON.stringify(requestBody),
        signal: abortControllerRef.current.signal,
      });

      console.log("响应状态:", response.status);
      if (!response.ok) {
        const errorText = await response.text();
        console.error("响应错误:", errorText);
        throw new Error(`HTTP error! status: ${response.status}, body: ${errorText}`);
      }

      const reader = response.body?.getReader();
      if (!reader) {
        throw new Error("无法获取响应流");
      }

      const decoder = new TextDecoder();
      let accumulatedContent = "";
      let buffer = "";
      let messageAdded = false;
      // 使用 Map 根据 id 合并插件调用
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
              
              // 先检查是否是结束状态
              const isFinished = data.finish_reason === "stop" || data.finishReason === "stop";
              const isError = data.finish_reason === "error" || data.finishReason === "error";

              // 只有非结束状态才处理内容
              if (!isFinished && !isError && data.choices && data.choices.length > 0) {
                for (const choice of data.choices) {
                  // 处理文本内容
                  if (choice.textCall?.content) {
                    accumulatedContent += choice.textCall.content;
                    setStreamingContent(accumulatedContent);
                    scrollToBottom();
                  }
                  
                  // 处理插件调用 - 根据 id 合并
                  if (choice.pluginCall || choice.streamType === "plugin") {
                    const pluginId = choice.id || choice.pluginCall?.pluginKey || `plugin-${Date.now()}`;
                    const existingCall = pluginCallsMap.get(pluginId);
                    
                    if (existingCall) {
                      // 合并更新现有插件调用
                      if (choice.pluginCall) {
                        if (choice.pluginCall.pluginName) existingCall.pluginName = choice.pluginCall.pluginName;
                        if (choice.pluginCall.pluginKey) existingCall.pluginKey = choice.pluginCall.pluginKey;
                        if (choice.pluginCall.params) existingCall.params = choice.pluginCall.params;
                        if (choice.pluginCall.result) existingCall.result = choice.pluginCall.result;
                      }
                      if (choice.streamState) existingCall.streamState = choice.streamState;
                    } else {
                      // 创建新的插件调用
                      const newCall: StreamingPluginCall = {
                        ...choice.pluginCall,
                        streamState: choice.streamState || "start",
                      };
                      pluginCallsMap.set(pluginId, newCall);
                    }
                    
                    // 更新流式插件调用状态
                    setStreamingPluginCalls(new Map(pluginCallsMap));
                    scrollToBottom();
                  }
                }
              }

              // 处理结束状态
              if (isFinished) {
                setIsStreaming(false);
                const finalPluginCalls = Array.from(pluginCallsMap.values());
                if ((accumulatedContent || finalPluginCalls.length > 0) && !messageAdded) {
                  setDebugMessages((prev) => [
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
        setDebugMessages((prev) => [
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

  // 取消流式请求
  const handleCancelStream = () => {
    if (abortControllerRef.current) {
      abortControllerRef.current.abort();
      setIsStreaming(false);
      setStreamingContent("");
      setStreamingPluginCalls(new Map());
      setSending(false);
    }
  };

  // 处理按键事件
  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      handleSendDebugMessage();
    }
  };

  // 获取角色头像
  const getRoleAvatar = (role: string) => {
    switch (role) {
      case "user":
        return userDetailInfo?.avatar ? (
          <Avatar src={userDetailInfo.avatar} className="user-avatar" />
        ) : (
          <Avatar icon={<UserOutlined />} className="user-avatar" />
        );
      case "assistant":
        return appAvatar ? (
          <Avatar src={appAvatar} className="assistant-avatar" />
        ) : (
          <Avatar icon={<RobotOutlined />} className="assistant-avatar" />
        );
      case "tool":
        return <Avatar icon={<ToolOutlined />} className="tool-avatar" />;
      default:
        return <Avatar>{role.charAt(0).toUpperCase()}</Avatar>;
    }
  };

  // 获取角色显示名称
  const getRoleDisplayName = (role: string) => {
    switch (role) {
      case "user":
        return "用户";
      case "assistant":
        return "助手";
      case "tool":
        return "工具";
      default:
        return role;
    }
  };

  const config = getConfig();

  return (
    <div className="app-debug-chat">
      {contextHolder}
      {/* 对话历史区域 */}
      <div className="debug-chat-history" ref={chatContainerRef}>
        {debugMessages.length === 0 && !isStreaming ? (
          <div className="debug-chat-empty">
            <Empty description="发送消息开始调试" />
          </div>
        ) : (
          <div className="debug-chat-messages">
            {debugMessages.map((msg, index) => (
              <div key={index} className={`debug-message debug-message-${msg.role}`}>
                <div className="message-avatar">{getRoleAvatar(msg.role)}</div>
                <div className="message-body">
                  <div className="message-header">
                    <Text strong className="message-role">
                      {getRoleDisplayName(msg.role)}
                    </Text>
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

            {/* 流式消息显示 */}
            {isStreaming && (
              <div className="debug-message debug-message-assistant debug-message-streaming">
                <div className="message-avatar">{getRoleAvatar("assistant")}</div>
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
                    {/* 显示正在执行的插件调用 */}
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

      {/* 输入框区域 - 左右布局 */}
      <div className="debug-chat-input-area">
        <div className="debug-chat-input-wrapper">
          <TextArea
            value={inputValue}
            onChange={(e) => setInputValue(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="输入消息，按 Enter 发送，Shift+Enter 换行..."
            disabled={sending}
            className="debug-chat-input"
          />
          <div className="debug-chat-send-button">
            {isStreaming ? (
              <Button danger onClick={handleCancelStream} size="large">
                停止
              </Button>
            ) : (
              <Button
                type="primary"
                icon={<SendOutlined />}
                onClick={handleSendDebugMessage}
                loading={sending}
                disabled={!inputValue.trim()}
                size="large"
              >
                发送
              </Button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
