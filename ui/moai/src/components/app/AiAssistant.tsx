import React, {
  useState,
  useRef,
  useEffect,
  useCallback,
  useMemo,
} from "react";
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
  Badge,
  Drawer,
  Modal,
  Slider,
} from "antd";
import type { InputRef } from "antd";
import {
  ChatList,
  ChatInputArea,
  ChatSendButton,
  ChatInputActionBar,
  TokenTag,
} from "@lobehub/ui/chat";
import { EmojiPicker, Markdown, CodeEditor } from "@lobehub/ui";
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
  MoreHorizontal,
  Settings,
  FileText,
  BookOpen,
  Puzzle,
  ChevronUp,
  ChevronDown,
  Copy,
} from "lucide-react";
import { Flexbox } from "react-layout-kit";
import { GetApiClient } from "../ServiceClient";
import { EnvOptions } from "../../Env";
import useAppStore from "../../stateshare/store";
import PromptSelector from "../prompt/PromptSelector";
import WikiSelector from "../wiki/WikiSelector";
import PluginSelector from "../plugin/PluginSelector";
import ModelSelector from "../aimodel/ModelSelector";
import {
  ProcessingAiAssistantChatCommand,
  DeleteAiAssistantChatCommand,
  QueryAiAssistantChatTopicListCommandResponse,
  QueryAiAssistantChatHistoryCommandResponse,
  AiAssistantChatTopic,
  AIAssistantChatObject,
  CreateAiAssistantChatCommandResponse,
  ChatContentItem,
  UpdateAiAssistanChatConfigCommand,
  DeleteAiAssistantChatOneRecordCommand,
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
  
  // 更新输入值的函数
  const updateInput = useCallback((value: string) => {
    setInput(value);
    inputValueRef.current = value;
  }, []);

  // 初始化时设置 ref 值
  useEffect(() => {
    inputValueRef.current = input;
  }, [input]);
  const [loading, setLoading] = useState(false);
  const [topicsLoading, setTopicsLoading] = useState(false);
  const [sending, setSending] = useState(false);
  const [assistantPrompt, setAssistantPrompt] = useState("");
  const [abortController, setAbortController] = useState<AbortController | null>(null);
  const [aiAvatar, setAiAvatar] = useState<string>("😎");
  const [promptSelectorVisible, setPromptSelectorVisible] = useState(false);
  const [promptEditVisible, setPromptEditVisible] = useState(false);
  const [editingPrompt, setEditingPrompt] = useState("");
  const [isEditing, setIsEditing] = useState(false);
  const [tokenInfo, setTokenInfo] = useState<{
    totalTokens: number;
    promptTokens: number;
    completionTokens: number;
  }>({
    totalTokens: 0,
    promptTokens: 0,
    completionTokens: 0,
  });
  const [selectedWikiId, setSelectedWikiId] = useState<number | null>(null);
  const [selectedWikiName, setSelectedWikiName] = useState<string>("");
  const [wikiInfo, setWikiInfo] = useState<{
    name: string;
    description: string;
    userCount: number;
    documentCount: number;
  } | null>(null);
  const [wikiSelectorVisible, setWikiSelectorVisible] = useState(false);
  const [selectedPluginIds, setSelectedPluginIds] = useState<number[]>([]);
  const [pluginInfos, setPluginInfos] = useState<{
    id: number;
    name: string;
    description: string;
  }[]>([]);
  const [pluginSelectorVisible, setPluginSelectorVisible] = useState(false);
  const [selectedModelId, setSelectedModelId] = useState<number | null>(null);
  const [selectedModelName, setSelectedModelName] = useState<string>("");
  const [modelSelectorVisible, setModelSelectorVisible] = useState(false);
  const [modelParams, setModelParams] = useState({
    temperature: 1.0,
    topP: 1.0,
    presencePenalty: 0.0,
    frequencyPenalty: 0.0,
  });
  const [modelConfigExpanded, setModelConfigExpanded] = useState(false);
  // 引用
  const inputRef = useRef<InputRef>(null);
  const chatListRef = useRef<HTMLDivElement>(null);
  const aiContentUpdateTimerRef = useRef<NodeJS.Timeout | null>(null);
  const inputValueRef = useRef<string>("");
  const [messageApi, contextHolder] = message.useMessage();

  // 获取用户信息和服务器信息
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);
  const serverInfo = useAppStore((state) => state.serverInfo);

  // 获取完整的头像URL
  const getAvatarUrl = useCallback(
    (avatarPath: string | null | undefined) => {
      if (!avatarPath) return null;
      if (
        avatarPath.startsWith("http://") ||
        avatarPath.startsWith("https://")
      ) {
        return avatarPath;
      }
      return serverInfo?.publicStoreUrl
        ? `${serverInfo.publicStoreUrl}/${avatarPath}`
        : avatarPath;
    },
    [serverInfo]
  );

  // 获取当前用户头像
  const getCurrentUserAvatar = useCallback(() => {
    return (
      getAvatarUrl(userDetailInfo?.avatarPath) ||
      "https://avatars.githubusercontent.com/u/17870709?v=4"
    );
  }, [getAvatarUrl, userDetailInfo]);

  // 获取AI头像
  const getAiAvatar = useCallback(() => {
    return aiAvatar;
  }, [aiAvatar]);

  // 缓存头像URL，避免频繁计算
  const currentUserAvatar = useMemo(() => getCurrentUserAvatar(), [getCurrentUserAvatar]);
  const currentAiAvatar = useMemo(() => getAiAvatar(), [getAiAvatar]);

  // 将 ChatList 的 data 抽出来作为单独的变量
  const chatListData = useMemo(() => {
    return messages
      .filter((msg) => msg?.id && msg.content !== undefined)
      .map((msg) => ({
        id: msg.id,
        role: msg.role,
        content: msg.content || "",
        createAt: msg.createAt,
        updateAt: msg.updateAt ?? msg.createAt,
        extra: msg.extra || {},
        meta: msg.meta || {
          avatar: msg.role === "user" ? currentUserAvatar : currentAiAvatar,
          backgroundColor: msg.role === "user" ? undefined : "#E8DA5A",
          title: msg.role === "user" ? "用户" : "AI 助手",
        },
      }));
  }, [messages, currentUserAvatar, currentAiAvatar]);



  // 自动滚动到底部
  useEffect(() => {
    if (chatListRef.current) {
      chatListRef.current.scrollTop = chatListRef.current.scrollHeight;
    }
  }, [messages]);

  // 从URL获取chatId参数
  const getChatIdFromUrl = useCallback(() => {
    const urlParams = new URLSearchParams(window.location.search);
    return urlParams.get("chatId");
  }, []);

  // 更新URL中的chatId参数
  const updateUrlWithChatId = useCallback((chatId: string | null) => {
    const url = new URL(window.location.href);
    if (chatId) {
      url.searchParams.set("chatId", chatId);
    } else {
      url.searchParams.delete("chatId");
    }
    window.history.replaceState({}, "", url.toString());
  }, []);

  // 创建新对话
  const createNewChat = useCallback(
    async (title?: string, content?: string) => {
      // 检查是否已选择模型
      if (!selectedModelId) {
        messageApi.warning("请先选择AI模型");
        return null;
      }

      try {
        const client = GetApiClient();
        const chatObject: AIAssistantChatObject = {
          title: title || DEFAULT_TITLE,
          modelId: selectedModelId,
          prompt: content || assistantPrompt || undefined,
          wikiId: selectedWikiId || 0,
          pluginIds: selectedPluginIds || [],
          executionSettings: [
            {
              key: "temperature",
              value: modelParams.temperature.toString()
            },
            {
              key: "topP",
              value: modelParams.topP.toString()
            },
            {
              key: "presencePenalty",
              value: modelParams.presencePenalty.toString()
            },
            {
              key: "frequencyPenalty",
              value: modelParams.frequencyPenalty.toString()
            }
          ]
        };

        const response = await client.api.app.assistant.create_chat.post(
          chatObject
        );

        if (response?.chatId) {
          const chatId = response.chatId;
          setCurrentChatId(chatId);
          setCurrentTitle(title || DEFAULT_TITLE);
          setEditValue(title || DEFAULT_TITLE);
          // 不清空消息，保持用户的问题显示
          setEditing(false);

          // 更新URL
          updateUrlWithChatId(chatId);

          // 刷新话题列表
          try {
            const topicsResponse = await client.api.app.assistant.topic_list.get();
            if (topicsResponse?.items) {
              const validTopics = topicsResponse.items.filter((topic) => topic?.chatId);
              setTopics(validTopics);
            }
          } catch (error) {
            console.error("刷新话题列表失败:", error);
          }

          return chatId;
        } else {
          throw new Error("创建对话失败：未返回chatId");
        }
      } catch (error) {
        console.error("创建对话失败:", error);
        const errorMessage = error instanceof Error ? error.message : "创建对话失败";
        messageApi.error(errorMessage);
        return null;
      }
    },
    [messageApi, updateUrlWithChatId, assistantPrompt, selectedModelId, selectedWikiId, selectedPluginIds, modelParams]
  );

  // 加载话题列表
  const loadTopics = useCallback(async () => {
    setTopicsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.assistant.topic_list.get();

      if (response?.items) {
        const validTopics = response.items.filter((topic) => topic?.chatId);
        setTopics(validTopics);
      } else {
        setTopics([]);
      }
    } catch (error) {
      console.error("加载话题列表失败:", error);
      const errorMessage = error instanceof Error ? error.message : "加载话题列表失败";
      messageApi.error(errorMessage);
    } finally {
      setTopicsLoading(false);
    }
  }, [messageApi]);

  // 处理 chatHistory 数据的辅助函数
  const parseChatHistory = useCallback(
    (chatHistoryData: any): ChatContentItem[] => {
      // 直接返回 chatHistory 数组，因为后端已经返回了正确的格式
      if (Array.isArray(chatHistoryData)) {
        return chatHistoryData;
      }

      // 如果是 UntypedNode，尝试获取值
      if (
        chatHistoryData &&
        typeof chatHistoryData === "object" &&
        "getValue" in chatHistoryData
      ) {
        try {
          const rawValue = (chatHistoryData as any).getValue();
          if (Array.isArray(rawValue)) {
            return rawValue;
          }
        } catch (e) {
          console.error("Failed to get value from UntypedNode:", e);
        }
      }

      return [];
    },
    []
  );

  // 将 ChatContentItem 数组转换为 ChatMessage 数组
  const convertChatHistoryToMessages = useCallback(
    (chatHistoryArray: ChatContentItem[], chatId: string): ChatMessage[] => {
      const historyMessages: ChatMessage[] = [];

      if (Array.isArray(chatHistoryArray) && chatHistoryArray.length > 0) {
        chatHistoryArray.forEach((msg: ChatContentItem, index: number) => {
          if (msg && typeof msg === "object") {
            // 从 authorName 确定角色
            let role = "user";
            if (msg.authorName) {
              const authorName = msg.authorName.toLowerCase();
              if (
                authorName.includes("assistant") ||
                authorName.includes("system")
              ) {
                role = "assistant";
              } else if (authorName.includes("user")) {
                role = "user";
              }
            }

            // 直接使用 content 字段
            const content = msg.content || "";

            const createTime =
              Date.now() - (chatHistoryArray.length - index) * 1000;
            historyMessages.push({
              id: msg.recordId || `${chatId}-${index}`,
              role: role as "user" | "assistant",
              content: content,
              createAt: createTime,
              updateAt: createTime,
              meta: {
                avatar:
                  role === "user" ? getCurrentUserAvatar() : getAiAvatar(),
                backgroundColor: role === "user" ? undefined : "#E8DA5A",
                title: role === "user" ? "用户" : "AI 助手",
              },
            });
          }
        });
      }

      return historyMessages;
    },
    [getCurrentUserAvatar, getAiAvatar]
  );

  // 获取模型信息
  const loadModelInfo = useCallback(
    async (modelId: number) => {
      try {
        const client = GetApiClient();
        // 先尝试从系统模型列表中查找
        const systemResponse = await client.api.aimodel.type.system_modellist.post({
          aiModelType: "chat"
        });
        
        if (systemResponse?.aiModels) {
          const systemModel = systemResponse.aiModels.find(model => model.id === modelId);
          if (systemModel) {
            setSelectedModelName(systemModel.name || "未命名模型");
            return;
          }
        }
        
        // 如果系统模型中没有找到，尝试从用户模型列表中查找
        const userResponse = await client.api.aimodel.type.user_modellist.post({
          aiModelType: "chat"
        });
        
        if (userResponse?.aiModels) {
          const userModel = userResponse.aiModels.find(model => model.id === modelId);
          if (userModel) {
            setSelectedModelName(userModel.name || "未命名模型");
            return;
          }
        }
        
        // 如果都没有找到，设置为默认值
        setSelectedModelName("未知模型");
      } catch (error) {
        console.error("获取模型信息失败:", error);
        setSelectedModelName("未知模型");
      }
    },
    []
  );

  // 加载对话历史
  const loadChatHistory = useCallback(
    async (chatId: string) => {
      setLoading(true);
      try {
        const client = GetApiClient();
        const response = await client.api.app.assistant.chat_history.get({
          queryParameters: { chatId, isBaseInfo: false },
        });

        console.log("Chat history response:", response); // 调试日志

        if (response?.chatHistory) {
          // 使用辅助函数解析 chatHistory 数据
          const chatHistoryArray = parseChatHistory(response.chatHistory);
          console.log("Parsed chatHistoryArray:", chatHistoryArray); // 调试日志

          // 将 ChatMessageContent 数组转换为 ChatMessage 数组
          const historyMessages = convertChatHistoryToMessages(
            chatHistoryArray,
            chatId
          );
          console.log("Processed historyMessages:", historyMessages); // 调试日志

          setMessages(historyMessages);
          const title = response?.title || DEFAULT_TITLE;
          setCurrentTitle(title);
          setEditValue(title);

          // 设置助手设定
          if (response?.prompt) {
            setAssistantPrompt(response.prompt);
          }

          // 设置AI头像
          if (response?.avatar) {
            setAiAvatar(response.avatar);
          }

          // 设置知识库信息
          if (response?.wikiId) {
            setSelectedWikiId(response.wikiId);
            loadWikiInfo(response.wikiId);
          } else {
            setSelectedWikiId(null);
            setWikiInfo(null);
          }

          // 设置插件信息
          if (response?.pluginIds && response.pluginIds.length > 0) {
            setSelectedPluginIds(response.pluginIds);
          } else {
            setSelectedPluginIds([]);
            setPluginInfos([]);
          }

          // 设置模型信息
          if (response?.modelId) {
            setSelectedModelId(response.modelId);
            // 获取模型名称
            loadModelInfo(response.modelId);
          } else {
            setSelectedModelId(null);
            setSelectedModelName("");
          }

          // 设置token信息
          setTokenInfo({
            totalTokens: response.totalTokens || 0,
            promptTokens: response.inputTokens || 0,
            completionTokens: response.outTokens || 0,
          });
        } else {
          console.log("No chatHistory found in response"); // 调试日志
          setMessages([]);
          setCurrentTitle(DEFAULT_TITLE);
          setEditValue(DEFAULT_TITLE);
          
          // 设置默认token信息
          setTokenInfo({
            totalTokens: 0,
            promptTokens: 0,
            completionTokens: 0,
          });
        }
      } catch (error) {
        console.error("加载对话历史失败:", error);
        const errorMessage = error instanceof Error ? error.message : "加载对话历史失败";
        messageApi.error(errorMessage);
      } finally {
        setLoading(false);
      }
    },
    [messageApi, parseChatHistory, convertChatHistoryToMessages, loadModelInfo]
  );

  // 获取知识库信息
  const loadWikiInfo = useCallback(
    async (wikiId: number) => {
      try {
        const client = GetApiClient();
        const response = await client.api.wiki.query_wiki_info.post({
          wikiId: wikiId,
        });

        if (response) {
          setWikiInfo({
            name: response.name || "",
            description: response.description || "",
            userCount: 0, // QueryWikiInfoResponse没有userCount属性
            documentCount: response.documentCount || 0,
          });
        }
      } catch (error) {
        console.error("获取知识库信息失败:", error);
      }
    },
    []
  );

  // 删除对话
  const deleteChat = useCallback(
    async (chatId: string) => {
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
          setTokenInfo({
            totalTokens: 0,
            promptTokens: 0,
            completionTokens: 0,
          });
          setSelectedWikiId(null);
          setWikiInfo(null);
          setSelectedPluginIds([]);
          setPluginInfos([]);
          setSelectedModelId(null);
          setSelectedModelName("");
          setModelParams({
            temperature: 1.0,
            topP: 1.0,
            presencePenalty: 0.0,
            frequencyPenalty: 0.0,
          });
          setModelConfigExpanded(false);
          updateUrlWithChatId(null);
        }
        loadTopics();
      } catch (error) {
        console.error("删除对话失败:", error);
        messageApi.error("删除失败");
      }
    },
    [currentChatId, loadTopics, messageApi, updateUrlWithChatId]
  );

  // 创建新对话（清空当前对话）
  const handleCreateNewChat = useCallback(() => {
    setCurrentChatId(null);
    setMessages([]);
    setCurrentTitle(DEFAULT_TITLE);
    setEditValue(DEFAULT_TITLE);
    setEditing(false);
    setTokenInfo({
      totalTokens: 0,
      promptTokens: 0,
      completionTokens: 0,
    });
    setSelectedWikiId(null);
    setWikiInfo(null);
    setSelectedPluginIds([]);
    setPluginInfos([]);
    setSelectedModelId(null);
    setSelectedModelName("");
    setModelParams({
      temperature: 1.0,
      topP: 1.0,
      presencePenalty: 0.0,
      frequencyPenalty: 0.0,
    });
    setModelConfigExpanded(false);
    updateUrlWithChatId(null);
  }, [updateUrlWithChatId]);

  // 选择对话
  const selectChat = useCallback(
    (chatId: string) => {
      setCurrentChatId(chatId);
      loadChatHistory(chatId);
      updateUrlWithChatId(chatId);
    },
    [loadChatHistory, updateUrlWithChatId]
  );

  // 停止请求
  const handleStop = useCallback(() => {
    if (abortController) {
      abortController.abort();
      setAbortController(null);
    }
    setSending(false);
    messageApi.info("已停止生成");
  }, [abortController, messageApi]);

  // 发送消息
  const handleSend = useCallback(async () => {
    const currentInput = inputValueRef.current.trim();
    if (!currentInput || sending) return;

    // 检查是否已选择模型
    if (!selectedModelId) {
      messageApi.warning("请先选择AI模型");
      return;
    }

    const userInput = currentInput;

    const userMsg: ChatMessage = {
      id: Date.now().toString(),
      role: "user",
      content: userInput,
      createAt: Date.now(),
      updateAt: Date.now(),
      extra: {},
      meta: {
        avatar: getCurrentUserAvatar(),
        title: "用户",
      },
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setSending(true);

    // 创建AbortController用于停止请求
    const controller = new AbortController();
    setAbortController(controller);

    const aiMsgId = (Date.now() + 1).toString();
    setMessages((prev) => [
      ...prev,
      {
        id: aiMsgId,
        role: "assistant",
        content: "",
        createAt: Date.now(),
        updateAt: Date.now(),
        extra: {},
        meta: {
          avatar: getAiAvatar(),
          backgroundColor: "#E8DA5A",
          title: "AI 助手",
        },
      } as ChatMessage,
    ]);

    try {
      let chatId = currentChatId;

      // 如果没有chatId，先创建对话
      if (!chatId) {
        const title = userInput.substring(0, 30) || DEFAULT_TITLE;
        chatId = await createNewChat(title, userInput);
        if (!chatId) {
          throw new Error("创建对话失败");
        }
      }

      const payload: ProcessingAiAssistantChatCommand = {
        chatId: chatId,
        content: userInput,
      };

      const token = localStorage.getItem("userinfo.accessToken");

      const response = await fetch(
        `${EnvOptions.ServerUrl}/api/app/assistant/completions`,
        {
          method: "POST",
          headers: {
            "Content-Type": "application/json",
            Accept: "text/event-stream",
            ...(token && { Authorization: `Bearer ${token}` }),
          },
          body: JSON.stringify(payload),
          signal: controller.signal,
        }
      );

      if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`请求失败: ${response.status} ${response.statusText} - ${errorText}`);
      }

      if (!response.body) throw new Error("No response body");

      const reader = response.body.getReader();
      let aiContent = "";
      let done = false;
      let buffer = "";

      while (!done) {
        // 检查是否被中断
        if (controller.signal.aborted) {
          break;
        }

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
                  // 使用防抖来减少消息更新频率，避免输入框卡顿
                  if (!aiContentUpdateTimerRef.current) {
                    aiContentUpdateTimerRef.current = setTimeout(() => {
                      setMessages((prevMsgs) =>
                        prevMsgs
                          .map((msg) =>
                            msg?.id === aiMsgId
                              ? { ...msg, content: aiContent || "" }
                              : msg
                          )
                          .filter(Boolean)
                      );
                      aiContentUpdateTimerRef.current = null;
                    }, 50); // 50ms防抖
                  }
                }
              } catch (e) {
                // 忽略解析错误
              }
            }
          }
        }
      }

      // AI回答完成后，重新获取对话信息以更新token统计
      if (chatId) {
        await loadChatHistory(chatId);
      }

      // 不再自动刷新话题列表，避免在对话时刷新
      // setTimeout(() => {
      //   loadTopics();
      // }, 100);
    } catch (err) {
      console.error("发送消息失败:", err);
      
      // 检查是否是用户主动中断
      if (err instanceof Error && err.name === 'AbortError') {
        setMessages((prevMsgs) =>
          prevMsgs
            .map((msg) =>
              msg?.id === aiMsgId
                ? { ...msg, content: "生成已停止" }
                : msg
            )
            .filter(Boolean)
        );
        messageApi.info("生成已停止");
      } else {
        // 其他错误情况
        setMessages((prevMsgs) =>
          prevMsgs
            .map((msg) =>
              msg?.id === aiMsgId
                ? { ...msg, content: "AI 回复失败，请重试" }
                : msg
            )
            .filter(Boolean)
        );
        
        // 显示具体的错误信息
        const errorMessage = err instanceof Error ? err.message : "发送消息失败";
        messageApi.error(errorMessage);
      }
    } finally {
      setSending(false);
      setAbortController(null);
    }
  }, [
    sending,
    currentChatId,
    currentTitle,
    selectedModelId,
    createNewChat,
    loadTopics,
    messageApi,
    loadChatHistory,
    getCurrentUserAvatar,
    getAiAvatar,
  ]);

  // 更新对话配置
  const updateChatConfig = useCallback(
    async (
      chatId: string,
      title?: string,
      prompt?: string,
      avatar?: string,
      wikiId?: number,
      pluginIds?: number[],
      modelId?: number,
      params?: {
        temperature: number;
        topP: number;
        presencePenalty: number;
        frequencyPenalty: number;
      }
    ) => {
      try {
        const client = GetApiClient();
        const updateCommand: UpdateAiAssistanChatConfigCommand = {
          chatId: chatId,
          title: title || currentTitle, // 使用传入的标题或当前标题
          prompt: prompt || assistantPrompt, // 使用传入的提示词或当前提示词
          aiAvatar: avatar || aiAvatar, // 使用传入的头像或当前头像
          modelId: modelId || 7, // 使用传入的模型ID或默认值
          wikiId: wikiId || undefined, // 添加知识库ID
          pluginIds: pluginIds || undefined, // 添加插件ID列表
          executionSettings: [
            {
              key: "temperature",
              value: (params?.temperature ?? modelParams.temperature).toString()
            },
            {
              key: "topP",
              value: (params?.topP ?? modelParams.topP).toString()
            },
            {
              key: "presencePenalty",
              value: (params?.presencePenalty ?? modelParams.presencePenalty).toString()
            },
            {
              key: "frequencyPenalty",
              value: (params?.frequencyPenalty ?? modelParams.frequencyPenalty).toString()
            }
          ]
        };

        await client.api.app.assistant.update_chat.post(updateCommand);
        messageApi.success("更新成功");

        // 不再自动刷新话题列表，避免在保存对话信息时刷新
        // loadTopics();
      } catch (error) {
        console.error("更新对话配置失败:", error);
        const errorMessage = error instanceof Error ? error.message : "更新失败";
        messageApi.error(errorMessage);
      }
    },
    [messageApi, loadTopics, currentTitle, assistantPrompt, aiAvatar, modelParams]
  );

  // 处理emoji选择
  const handleEmojiSelect = useCallback(
    (emoji: string) => {
      setAiAvatar(emoji);

      // 如果有当前对话ID，更新到服务器
      if (currentChatId) {
        updateChatConfig(currentChatId, currentTitle, assistantPrompt, emoji, selectedWikiId || undefined, selectedPluginIds, selectedModelId || undefined);
      }
    },
    [currentChatId, currentTitle, assistantPrompt, selectedWikiId, selectedPluginIds, selectedModelId, updateChatConfig]
  );

  // 保存标题
  const handleTitleSave = useCallback(() => {
    const newTitle = editValue.trim() || DEFAULT_TITLE;
    setCurrentTitle(newTitle);
    setEditValue(newTitle);
    setEditing(false);

    // 如果有当前对话ID，更新到服务器（传递新标题、当前提示词、知识库ID和插件ID）
    if (currentChatId) {
      updateChatConfig(currentChatId, newTitle, assistantPrompt, aiAvatar, selectedWikiId || undefined, selectedPluginIds, selectedModelId || undefined);
    }
  }, [editValue, currentChatId, updateChatConfig, assistantPrompt, aiAvatar, selectedWikiId, selectedPluginIds, selectedModelId]);

  // 保存助手设定
  const handleSaveAssistantPrompt = useCallback(() => {
    // 如果有当前对话ID，更新到服务器（传递当前标题、新提示词、AI头像、知识库ID、插件ID和模型参数）
    if (currentChatId) {
      updateChatConfig(currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedWikiId || undefined, selectedPluginIds, selectedModelId || undefined, modelParams);
    } else {
      messageApi.success("助手设定已保存");
    }
  }, [
    currentChatId,
    currentTitle,
    assistantPrompt,
    aiAvatar,
    selectedWikiId,
    selectedPluginIds,
    selectedModelId,
    modelParams,
    updateChatConfig,
    messageApi,
  ]);

  // 处理知识库选择
  const handleWikiSelect = useCallback((wikiId: number, wikiName: string) => {
    setSelectedWikiId(wikiId);
    setSelectedWikiName(wikiName);
    loadWikiInfo(wikiId);
    
    // 如果有当前对话ID，更新到服务器
    if (currentChatId) {
      updateChatConfig(currentChatId, currentTitle, assistantPrompt, aiAvatar, wikiId, selectedPluginIds, selectedModelId || undefined);
    }
  }, [currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedPluginIds, selectedModelId, loadWikiInfo, updateChatConfig]);

  // 处理插件选择
  const handlePluginSelect = useCallback((pluginIds: number[]) => {
    setSelectedPluginIds(pluginIds);
    
    // 如果有当前对话ID，更新到服务器
    if (currentChatId) {
      updateChatConfig(currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedWikiId || undefined, pluginIds, selectedModelId || undefined);
    }
  }, [currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedWikiId, selectedModelId, updateChatConfig]);

  // 处理模型选择
  const handleModelSelect = useCallback((modelId: number, modelName: string) => {
    setSelectedModelId(modelId);
    setSelectedModelName(modelName);
    
    // 如果有当前对话ID，更新到服务器（不包含模型参数，等用户点击保存设定时再保存）
    if (currentChatId) {
      updateChatConfig(currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedWikiId || undefined, selectedPluginIds, modelId);
    }
  }, [currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedWikiId, selectedPluginIds, updateChatConfig]);

  // 处理提示词选择
  const handlePromptSelect = useCallback((content: string) => {
    setAssistantPrompt(content);
    
    // 如果有当前对话ID，更新到服务器
    if (currentChatId) {
      updateChatConfig(currentChatId, currentTitle, content, aiAvatar, selectedWikiId || undefined, selectedPluginIds, selectedModelId || undefined);
    }
  }, [currentChatId, currentTitle, aiAvatar, selectedWikiId, selectedPluginIds, selectedModelId, updateChatConfig]);

  // 处理提示词编辑
  const handlePromptEdit = useCallback(() => {
    setEditingPrompt(assistantPrompt);
    setPromptEditVisible(true);
    setIsEditing(false); // 初始状态为预览
  }, [assistantPrompt]);

  // 保存编辑的提示词
  const handleSavePromptEdit = useCallback(() => {
    setAssistantPrompt(editingPrompt);
    setPromptEditVisible(false);
    setIsEditing(false);
    
    // 如果有当前对话ID，更新到服务器
    if (currentChatId) {
      updateChatConfig(currentChatId, currentTitle, editingPrompt, aiAvatar, selectedWikiId || undefined, selectedPluginIds, selectedModelId || undefined);
    }
  }, [editingPrompt, currentChatId, currentTitle, aiAvatar, selectedWikiId, selectedPluginIds, selectedModelId, updateChatConfig]);

  // 切换编辑状态
  const handleToggleEdit = useCallback(() => {
    setIsEditing(!isEditing);
  }, [isEditing]);

  // 取消编辑
  const handleCancelEdit = useCallback(() => {
    setEditingPrompt(assistantPrompt); // 恢复原始内容
    setIsEditing(false);
  }, [assistantPrompt]);

  // 格式化时间
  const formatTime = useCallback((timeStr: string) => {
    if (!timeStr) return "未知时间";

    try {
      const date = new Date(timeStr);
      if (isNaN(date.getTime())) return "无效时间";

      const now = new Date();
      const diff = now.getTime() - date.getTime();
      const days = Math.floor(diff / (1000 * 60 * 60 * 24));

      if (days === 0) {
        return date.toLocaleTimeString("zh-CN", {
          hour: "2-digit",
          minute: "2-digit",
        });
      } else if (days === 1) {
        return "昨天";
      } else if (days < 7) {
        return `${days}天前`;
      } else {
        return date.toLocaleDateString("zh-CN");
      }
    } catch (error) {
      return "时间格式错误";
    }
  }, []);

  // 组件挂载时初始化
  useEffect(() => {
    const initializeChat = async () => {
      // 先加载话题列表
      await loadTopics();

      // 检查URL中是否有chatId
      const urlChatId = getChatIdFromUrl();
      if (urlChatId) {
        // 如果有chatId，加载对应的对话
        setCurrentChatId(urlChatId);
        await loadChatHistory(urlChatId);
      }
    };

    initializeChat();

    // 清理定时器
    return () => {
      if (aiContentUpdateTimerRef.current) {
        clearTimeout(aiContentUpdateTimerRef.current);
        aiContentUpdateTimerRef.current = null;
      }
    };
  }, [loadTopics, getChatIdFromUrl, loadChatHistory]);

  // 渲染话题列表项
  const renderTopicItem = useCallback(
    (topic: ChatTopic) => {
      if (!topic?.chatId) return null;

      const isActive = currentChatId === topic.chatId;

      return (
        <List.Item
          className={`topic-item ${isActive ? "active" : ""}`}
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
            </Popconfirm>,
          ]}
        >
          <List.Item.Meta
            avatar={
              <Badge dot={isActive}>
                <Avatar
                  size="small"
                  icon={<MessageSquare size={14} />}
                  className={isActive ? "active-avatar" : ""}
                />
              </Badge>
            }
            title={
              <Text
                ellipsis
                className={`topic-title ${isActive ? "active" : ""}`}
                style={{ fontWeight: isActive ? 600 : 400 }}
              >
                {topic.title || "未命名对话"}
              </Text>
            }
            description={
              <Space size="small" className="topic-time">
                <Clock size={12} />
                <Text type="secondary" style={{ fontSize: "12px" }}>
                  {formatTime(topic.createTime || "")}
                </Text>
              </Space>
            }
          />
        </List.Item>
      );
    },
    [currentChatId, selectChat, deleteChat, formatTime]
  );

  // 渲染空状态
  const renderEmptyState = () => (
    <Empty
      image={Empty.PRESENTED_IMAGE_SIMPLE}
      description={
        <Space direction="vertical" size="small">
          <Bot size={48} style={{ color: "#d9d9d9" }} />
          <Title level={4} style={{ margin: 0, color: "#8c8c8c" }}>
            {selectedModelId ? "开始新的对话" : "请先选择AI模型"}
          </Title>
          <Text type="secondary">
            {selectedModelId 
              ? "在下方输入框中输入您的问题" 
              : "在右侧助手设定中选择AI模型，然后开始对话"
            }
          </Text>
        </Space>
      }
      style={{ marginTop: "60px" }}
    />
  );

  // 删除单条对话记录
  const deleteChatRecord = useCallback(
    async (recordId: string) => {
      if (!currentChatId) {
        messageApi.error("当前没有活跃的对话");
        return;
      }

      try {
        const client = GetApiClient();
        const deleteCommand: DeleteAiAssistantChatOneRecordCommand = {
          chatId: currentChatId,
          recordId: recordId,
        };

        await client.api.app.assistant.delete_chat_record.delete(deleteCommand);
        messageApi.success("删除成功");

        // 重新加载对话历史以更新显示
        await loadChatHistory(currentChatId);
      } catch (error) {
        console.error("删除对话记录失败:", error);
        const errorMessage = error instanceof Error ? error.message : "删除失败";
        messageApi.error(errorMessage);
      }
    },
    [currentChatId, messageApi, loadChatHistory]
  );

  // 自定义操作栏，显示删除和复制按钮
  const CustomActionsBar = useCallback(
    ({ id, role, content }: { id: string; role: string; content: string }) => {
      const handleCopy = useCallback(() => {
        navigator.clipboard.writeText(content).then(() => {
          messageApi.success("已复制到剪贴板");
        }).catch(() => {
          messageApi.error("复制失败");
        });
      }, [content, messageApi]);

      const handleDelete = useCallback(() => {
        deleteChatRecord(id);
      }, [id, deleteChatRecord]);

      return (
        <Space size="small">
          <ActionIcon
            icon={Copy}
            size="small"
            className="copy-btn"
            title="复制消息"
            onClick={handleCopy}
          />
          <Popconfirm
            title="确定要删除这条消息吗？"
            description="删除后无法恢复"
            onConfirm={handleDelete}
            okText="确定"
            cancelText="取消"
            placement="left"
          >
            <ActionIcon
              icon={Trash2}
              size="small"
              className="delete-record-btn"
              title="删除消息"
            />
          </Popconfirm>
        </Space>
      );
    },
    [deleteChatRecord, messageApi]
  );

  return (
    <>
      {contextHolder}
      <ThemeProvider>
        <Layout className="ai-assistant-layout">
          {/* 左侧边栏 */}
          <Sider width={320} className="ai-assistant-sider" theme="light">
            <Card
              className="sidebar-card"
              bodyStyle={{
                padding: "16px",
                height: "100%",
                display: "flex",
                flexDirection: "column",
              }}
            >
              {/* 顶部标题和新建按钮 */}
              <Space
                direction="vertical"
                size="middle"
                style={{ width: "100%" }}
              >
                <Space
                  style={{ width: "100%", justifyContent: "space-between" }}
                >
                  <Title level={4} style={{ margin: 0 }}>
                    对话记录
                  </Title>
                  <Button
                    type="primary"
                    icon={<Plus size={16} />}
                    size="small"
                    onClick={handleCreateNewChat}
                  >
                    新建
                  </Button>
                </Space>

                <Divider style={{ margin: "8px 0" }} />

                {/* 话题列表 */}
                {topicsLoading ? (
                  <div style={{ textAlign: "center", padding: "40px 0" }}>
                    <Spin />
                  </div>
                ) : topics.length === 0 ? (
                  <Empty
                    description="暂无对话记录"
                    style={{ marginTop: "40px" }}
                  />
                ) : (
                  <List
                    dataSource={topics}
                    renderItem={renderTopicItem}
                    className="topics-list"
                    style={{ flex: 1, overflow: "auto" }}
                  />
                )}
              </Space>
            </Card>
          </Sider>

          {/* 中间主内容区 */}
          <Content className="ai-assistant-content">
            <Layout style={{ height: "100%" }}>
              {/* 左侧对话区域 */}
              <Content style={{ display: "flex", flexDirection: "column" }}>
                {/* 上半部分：对话历史容器 */}
                <Card
                  className="chat-list-container"
                  bodyStyle={{
                    padding: 0,
                    flex: 1,
                    display: "flex",
                    flexDirection: "column",
                  }}
                >
                  {/* 顶部标题栏 */}
                  <Card
                    size="small"
                    className="header-card"
                    bodyStyle={{ padding: "16px 24px" }}
                  >
                    <Space
                      style={{ width: "100%", justifyContent: "space-between" }}
                    >
                      <div style={{ flex: 1 }}>
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
                              textAlign: "center",
                              borderRadius: "8px",
                            }}
                            autoFocus
                          />
                        ) : (
                          <Space
                            style={{ width: "100%", justifyContent: "center" }}
                          >
                            {/* AI头像控件 */}
                            <EmojiPicker
                              value={aiAvatar}
                              onChange={handleEmojiSelect}
                                size={24}
                                style={{marginRight: "20px"}}
                            />
                            <Title
                              level={3}
                              style={{ margin: 0, cursor: "pointer" }}
                              onClick={() => setEditing(true)}
                            >
                              {currentTitle || DEFAULT_TITLE}
                            </Title>

                          </Space>
                        )}
                      </div>
                    </Space>
                  </Card>

                  {/* ChatList 区域 */}
                  <div
                    ref={chatListRef}
                    className="chat-list-area"
                    style={{ flex: 1, overflow: "auto", padding: "16px 24px" }}
                  >
                    {loading ? (
                      <div style={{ textAlign: "center", padding: "40px 0" }}>
                        <Spin size="large" />
                      </div>
                    ) : messages.length === 0 ? (
                      renderEmptyState()
                    ) : (
                      <ChatList
                        data={chatListData}
                        renderActions={{ default: CustomActionsBar }}
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
                    height: "auto",
                  }}
                >
                  <ChatInputArea
                    value={input}
                    onInput={updateInput}
                    onSend={handleSend}
                    placeholder={selectedModelId ? "请输入你的问题..." : "请先选择AI模型，然后输入你的问题..."}
                    disabled={sending}
                    bottomAddons={
                      <ChatSendButton 
                        onSend={sending ? handleStop : handleSend} 
                      />
                    }
                    autoFocus={false}
                    autoSize={{ minRows: 3, maxRows: 8 }}
                  />
                </Card>
              </Content>

              {/* 右侧助手设定面板 */}
              <Sider
                width={350}
                className="assistant-settings-sider"
                theme="light"
              >
                <Card
                  className="settings-card"
                  bodyStyle={{
                    padding: "16px",
                    height: "100%",
                    display: "flex",
                    flexDirection: "column",
                    overflow: "auto",
                  }}
                >
                  <Space
                    direction="vertical"
                    size="middle"
                    style={{ width: "100%" }}
                  >
                    <div>
                      <Title level={4} style={{ margin: 0, marginBottom: 8 }}>
                        助手设定
                      </Title>
                      <Text type="secondary" style={{ fontSize: "14px" }}>
                        配置AI助手的头像和系统提示词
                      </Text>
                    </div>

                    {/* Token统计信息 */}
                    <div>
                      <Title level={5} style={{ marginBottom: "4px" }}>Token统计</Title>
                      <div>
                        <Space size="large" style={{ width: "100%", justifyContent: "space-between" }}>
                          <div style={{ textAlign: "center" }}>
                            <div style={{ fontSize: "16px", fontWeight: "bold", color: "#1890ff" }}>
                              {tokenInfo.totalTokens}
                            </div>
                            <div style={{ fontSize: "12px", color: "#8c8c8c" }}>总计</div>
                          </div>
                          <div style={{ textAlign: "center" }}>
                            <div style={{ fontSize: "14px", color: "#52c41a" }}>
                              {tokenInfo.promptTokens}
                            </div>
                            <div style={{ fontSize: "12px", color: "#8c8c8c" }}>输入</div>
                          </div>
                          <div style={{ textAlign: "center" }}>
                            <div style={{ fontSize: "14px", color: "#fa8c16" }}>
                              {tokenInfo.completionTokens}
                            </div>
                            <div style={{ fontSize: "12px", color: "#8c8c8c" }}>输出</div>
                          </div>
                        </Space>
                      </div>
                    </div>

                    {/* 模型配置 */}
                    <div>
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          marginBottom: 8,
                          cursor: "pointer",
                        }}
                        onClick={() => setModelConfigExpanded(!modelConfigExpanded)}
                      >
                        <Title level={5} style={{ margin: 0 }}>
                          模型配置
                        </Title>
                        <Button
                          type="text"
                          size="small"
                          icon={modelConfigExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}
                          onClick={(e) => {
                            e.stopPropagation();
                            setModelConfigExpanded(!modelConfigExpanded);
                          }}
                        />
                      </div>
                      
                      {modelConfigExpanded && (
                        <>
                          <Text type="secondary" style={{ fontSize: "14px", marginBottom: "12px" }}>
                            选择AI模型和配置参数
                          </Text>

                          {/* 模型选择 */}
                          <div style={{ marginBottom: "16px" }}>
                            <div style={{ marginBottom: "8px" }}>
                              <Text strong>模型设置</Text>
                              {!selectedModelId && (
                                <Text type="danger" style={{ marginLeft: "8px", fontSize: "12px" }}>
                                  * 必选
                                </Text>
                              )}
                            </div>
                            {!selectedModelId && (
                              <div style={{ marginBottom: "8px" }}>
                                <Text type="secondary" style={{ fontSize: "12px" }}>
                                  请先选择AI模型才能开始对话
                                </Text>
                              </div>
                            )}
                            <Button
                              type={selectedModelId ? "default" : "primary"}
                              style={{ width: "100%", textAlign: "left", justifyContent: "space-between" }}
                              onClick={() => setModelSelectorVisible(true)}
                            >
                              <Space>
                                <Bot size={16} />
                                <Text>{selectedModelName || "选择模型"}</Text>
                              </Space>
                              <Text type="secondary">选择</Text>
                            </Button>
                          </div>

                          {/* 模型参数 */}
                          <div>
                            <div style={{ marginBottom: "8px" }}>
                              <Text strong>模型参数</Text>
                            </div>
                            
                            {/* Temperature */}
                            <div style={{ marginBottom: "12px" }}>
                              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "4px" }}>
                                <Text style={{ fontSize: "12px" }}>temperature</Text>
                                <Text style={{ fontSize: "12px", color: "#8c8c8c" }}>{modelParams.temperature}</Text>
                              </div>
                              <div style={{ fontSize: "11px", color: "#8c8c8c", marginBottom: "4px" }}>
                                随机性：值越大，回复越随机
                              </div>
                              <Slider
                                min={0}
                                max={2}
                                step={0.1}
                                value={modelParams.temperature}
                                onChange={(value) => {
                                  const newParams = { ...modelParams, temperature: value };
                                  setModelParams(newParams);
                                }}
                                style={{ marginBottom: "8px" }}
                              />
                            </div>

                            {/* Top P */}
                            <div style={{ marginBottom: "12px" }}>
                              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "4px" }}>
                                <Text style={{ fontSize: "12px" }}>top_p</Text>
                                <Text style={{ fontSize: "12px", color: "#8c8c8c" }}>{modelParams.topP}</Text>
                              </div>
                              <div style={{ fontSize: "11px", color: "#8c8c8c", marginBottom: "4px" }}>
                                核采样：与随机性类似，但不要和随机性一起更改
                              </div>
                              <Slider
                                min={0}
                                max={1}
                                step={0.1}
                                value={modelParams.topP}
                                onChange={(value) => {
                                  const newParams = { ...modelParams, topP: value };
                                  setModelParams(newParams);
                                }}
                                style={{ marginBottom: "8px" }}
                              />
                            </div>

                            {/* Presence Penalty */}
                            <div style={{ marginBottom: "12px" }}>
                              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "4px" }}>
                                <Text style={{ fontSize: "12px" }}>presence_penalty</Text>
                                <Text style={{ fontSize: "12px", color: "#8c8c8c" }}>{modelParams.presencePenalty}</Text>
                              </div>
                              <div style={{ fontSize: "11px", color: "#8c8c8c", marginBottom: "4px" }}>
                                话题新鲜度：值越大，越有可能扩展到新话题
                              </div>
                              <Slider
                                min={-2}
                                max={2}
                                step={0.1}
                                value={modelParams.presencePenalty}
                                onChange={(value) => {
                                  const newParams = { ...modelParams, presencePenalty: value };
                                  setModelParams(newParams);
                                }}
                                style={{ marginBottom: "8px" }}
                              />
                            </div>

                            {/* Frequency Penalty */}
                            <div style={{ marginBottom: "12px" }}>
                              <div style={{ display: "flex", justifyContent: "space-between", marginBottom: "4px" }}>
                                <Text style={{ fontSize: "12px" }}>frequency_penalty</Text>
                                <Text style={{ fontSize: "12px", color: "#8c8c8c" }}>{modelParams.frequencyPenalty}</Text>
                              </div>
                              <div style={{ fontSize: "11px", color: "#8c8c8c", marginBottom: "4px" }}>
                                频率惩罚度：值越大，越有可能降低重复字词
                              </div>
                              <Slider
                                min={-2}
                                max={2}
                                step={0.1}
                                value={modelParams.frequencyPenalty}
                                onChange={(value) => {
                                  const newParams = { ...modelParams, frequencyPenalty: value };
                                  setModelParams(newParams);
                                }}
                                style={{ marginBottom: "8px" }}
                              />
                            </div>
                          </div>
                        </>
                      )}
                    </div>



                    <div>
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          marginBottom: 8,
                        }}
                      >
                        <Title level={5} style={{ margin: 0 }}>
                          系统提示词
                        </Title>
                        <Space>
                          <Tooltip title="从提示词库中选择">
                            <Button
                              type="text"
                              icon={<FileText size={16} />}
                              onClick={() => setPromptSelectorVisible(true)}
                              size="small"
                            />
                          </Tooltip>

                        </Space>
                      </div>
                      <Text
                        type="secondary"
                        style={{ fontSize: "14px", marginBottom: 8 }}
                      >
                        设定AI助手的角色和行为，这将影响AI的回复风格和内容。
                      </Text>

                      <div className="prompt-markdown-container">
                        {assistantPrompt ? (
                          <Markdown
                            children={assistantPrompt}
                            fontSize={14}
                            lineHeight={1.6}
                            marginMultiple={1}
                            headerMultiple={1}
                            fullFeaturedCodeBlock={true}
                            allowHtml={false}
                          />
                        ) : (
                          <Text type="secondary" style={{ fontSize: "14px" }}>
                            例如：你是一个专业的编程助手，擅长解释技术概念和提供代码示例...
                          </Text>
                        )}
                      </div>
                    </div>

                    {/* 知识库选择 */}
                    <div>
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          marginBottom: 8,
                        }}
                      >
                        <Title level={5} style={{ margin: 0 }}>
                          知识库
                        </Title>
                        <Button
                          type="text"
                          size="small"
                          onClick={() => setWikiSelectorVisible(true)}
                        >
                          {selectedWikiId ? "更换" : "选择"}
                        </Button>
                      </div>
                      <Text type="secondary" style={{ fontSize: "14px", marginBottom: 8 }}>
                        选择知识库来增强AI助手的回答能力
                      </Text>

                      {selectedWikiId && wikiInfo ? (
                        <div style={{ 
                          padding: "12px", 
                          border: "1px solid #f0f0f0", 
                          borderRadius: "8px",
                          backgroundColor: "#fafafa"
                        }}>
                          <div style={{ marginBottom: "8px" }}>
                            <Text strong>{wikiInfo.name}</Text>
                          </div>
                          <div style={{ marginBottom: "8px" }}>
                            <Text type="secondary" style={{ fontSize: "12px" }}>
                              {wikiInfo.description}
                            </Text>
                          </div>
                          <div style={{ display: "flex", justifyContent: "space-between", fontSize: "12px" }}>
                            <Text type="secondary">
                              <BookOpen size={12} style={{ marginRight: "4px" }} />
                              {wikiInfo.documentCount} 个文档
                            </Text>
                            <Button
                              type="text"
                              size="small"
                              danger
                              onClick={() => {
                                setSelectedWikiId(null);
                                setWikiInfo(null);
                                if (currentChatId) {
                                  updateChatConfig(currentChatId, currentTitle, assistantPrompt, aiAvatar, undefined);
                                }
                              }}
                            >
                              移除
                            </Button>
                          </div>
                        </div>
                      ) : (
                        <div style={{ 
                          padding: "12px", 
                          border: "1px dashed #d9d9d9", 
                          borderRadius: "8px",
                          textAlign: "center",
                          color: "#8c8c8c"
                        }}>
                          未选择知识库
                        </div>
                      )}
                    </div>

                    {/* 插件选择 */}
                    <div>
                      <div
                        style={{
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                          marginBottom: 8,
                        }}
                      >
                        <Title level={5} style={{ margin: 0 }}>
                          插件
                        </Title>
                        <Button
                          type="text"
                          size="small"
                          onClick={() => setPluginSelectorVisible(true)}
                        >
                          {selectedPluginIds.length > 0 ? "管理" : "添加"}
                        </Button>
                      </div>
                      <Text type="secondary" style={{ fontSize: "14px", marginBottom: 8 }}>
                        选择插件来增强AI助手的功能，最多可选择3个插件
                      </Text>

                      {selectedPluginIds.length > 0 ? (
                        <div style={{ 
                          padding: "12px", 
                          border: "1px solid #f0f0f0", 
                          borderRadius: "8px",
                          backgroundColor: "#fafafa"
                        }}>
                          <div style={{ marginBottom: "8px" }}>
                            <Text strong>已选择 {selectedPluginIds.length} 个插件</Text>
                          </div>
                          <div style={{ display: "flex", justifyContent: "space-between", fontSize: "12px" }}>
                            <Text type="secondary">
                              <Puzzle size={12} style={{ marginRight: "4px" }} />
                              {selectedPluginIds.length}/3 个插件
                            </Text>
                            <Button
                              type="text"
                              size="small"
                              danger
                              onClick={() => {
                                setSelectedPluginIds([]);
                                setPluginInfos([]);
                                if (currentChatId) {
                                  updateChatConfig(currentChatId, currentTitle, assistantPrompt, aiAvatar, selectedWikiId || undefined, []);
                                }
                              }}
                            >
                              清空
                            </Button>
                          </div>
                        </div>
                      ) : (
                        <div style={{ 
                          padding: "12px", 
                          border: "1px dashed #d9d9d9", 
                          borderRadius: "8px",
                          textAlign: "center",
                          color: "#8c8c8c"
                        }}>
                          未选择插件
                        </div>
                      )}
                    </div>

                    <div style={{ marginTop: "auto" }}>
                      <Button
                        type="primary"
                        onClick={handleSaveAssistantPrompt}
                        style={{ width: "100%" }}
                      >
                        保存设定
                      </Button>
                    </div>
                  </Space>
                </Card>
              </Sider>
            </Layout>
          </Content>
        </Layout>

        {/* 提示词选择器 */}
        <PromptSelector
          visible={promptSelectorVisible}
          onCancel={() => setPromptSelectorVisible(false)}
          onChange={handlePromptSelect}
        />

        {/* 提示词编辑模态窗口 */}
        <Modal
          title="编辑系统提示词"
          open={promptEditVisible}
          onCancel={() => setPromptEditVisible(false)}
          width={"70vw"}
          className="prompt-edit-modal"
          maskClosable={false}
          bodyProps={{ overflow: "auto" }}
          footer={[
            <Button key="cancel" onClick={() => setPromptEditVisible(false)}>
              取消
            </Button>,
            isEditing ? (
              <Button key="cancel-edit" onClick={handleCancelEdit}>
                取消编辑
              </Button>
            ) : null,
            <Button
              key="toggle-edit"
              type={isEditing ? "default" : "primary"}
              onClick={handleToggleEdit}
            >
              {isEditing ? "预览" : "编辑"}
            </Button>,
            <Button key="save" type="primary" onClick={handleSavePromptEdit}>
              保存
            </Button>,
          ].filter(Boolean)}
        >
          <div style={{ marginBottom: 16 }}>
            <Text type="secondary" style={{ fontSize: "14px" }}>
              {isEditing
                ? "编辑AI助手的系统提示词，支持Markdown格式"
                : "预览AI助手的系统提示词"}
            </Text>
          </div>

          {isEditing ? (
            <div style={{ marginBottom: 16 }}>
              <CodeEditor
                language="md"
                onValueChange={setEditingPrompt}
                value={editingPrompt}
                width="100%"
                height="60vh"
                placeholder="例如：你是一个专业的编程助手，擅长解释技术概念和提供代码示例..."
                variant="pure"
              />
            </div>
          ) : (
            <div className="prompt-preview-container">
              {editingPrompt ? (
                <Markdown
                  children={editingPrompt}
                  fontSize={14}
                  lineHeight={1.6}
                  marginMultiple={1}
                  headerMultiple={1}
                  fullFeaturedCodeBlock={true}
                  allowHtml={false}
                />
              ) : (
                <Text type="secondary" style={{ fontSize: "14px" }}>
                  暂无提示词内容
                </Text>
              )}
            </div>
          )}
        </Modal>

        {/* 知识库选择器 */}
        <WikiSelector
          visible={wikiSelectorVisible}
          onCancel={() => setWikiSelectorVisible(false)}
          onSelect={handleWikiSelect}
          selectedWikiId={selectedWikiId}
        />

        {/* 插件选择器 */}
        <PluginSelector
          visible={pluginSelectorVisible}
          onCancel={() => setPluginSelectorVisible(false)}
          onSelect={handlePluginSelect}
          selectedPluginIds={selectedPluginIds}
        />

        {/* 模型选择器 */}
        <ModelSelector
          visible={modelSelectorVisible}
          onCancel={() => setModelSelectorVisible(false)}
          onSelect={handleModelSelect}
          selectedModelId={selectedModelId}
          modelType="chat"
        />
      </ThemeProvider>
    </>
  );
};

export default AiAssistant;
