import { useState, useEffect, useCallback, useRef } from "react";
import { Layout, Button, message, Space, Spin, Typography } from "antd";
import { ArrowLeftOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import { useParams, useNavigate } from "react-router";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { KeyValueString, AppChatHistoryItem } from "../../../../apiClient/models";
import AppConfigPanel, { type AppConfigData } from "./ChatAppConfigPanel";
import AppDebugChat from "./DebugChatApp";
import "./ChatAppConfig.css";

const { Sider, Content } = Layout;
const { Title } = Typography;

export default function ChatAppConfig() {
  const { id, appId } = useParams();
  const navigate = useNavigate();
  const teamId = parseInt(id!);

  const [messageApi, contextHolder] = message.useMessage();
  const [loading, setLoading] = useState(false);
  const [saveLoading, setSaveLoading] = useState(false);
  const [appName, setAppName] = useState<string>("");
  const [initialData, setInitialData] = useState<AppConfigData | undefined>();
  const [debugHistory, setDebugHistory] = useState<AppChatHistoryItem[]>([]);

  // 当前配置数据（用于调试对话）
  const currentConfigRef = useRef<AppConfigData>({});

  // 加载应用详情
  const loadAppDetail = useCallback(async () => {
    if (!appId) return;
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.team.chatapp.config.post({
        appId: appId,
        teamId: teamId
      });

      if (response) {
        console.log("加载应用详情成功:", response);
        setAppName(response.name || "");
        const executionSettings = response.executionSettings || [];
        const getSettingValue = (key: string, defaultValue: number): number => {
          const setting = executionSettings.find((s: KeyValueString) => s.key === key);
          return setting?.value ? parseFloat(setting.value) : defaultValue;
        };

        const data: AppConfigData = {
          appId: response.appId || undefined,
          name: response.name || "",
          description: response.description || "",
          avatar: response.avatar || "",
          avatarKey: response.avatarKey || "",
          modelId: response.modelId || undefined,
          prompt: response.prompt || "",
          temperature: getSettingValue("temperature", 1),
          topP: getSettingValue("top_p", 1),
          presencePenalty: getSettingValue("presence_penalty", 0),
          frequencyPenalty: getSettingValue("frequency_penalty", 0),
          wikiIds: response.wikiIds || [],
          plugins: response.plugins || [],
          isPublic: response.isPublic ?? undefined,
          isForeign: response.isForeign ?? undefined,
          isAuth: response.isAuth ?? undefined,
          classifyId: response.classifyId || undefined,
        };
        console.log("初始化数据:", data);
        setInitialData(data);
        currentConfigRef.current = data; // 同时更新 ref
      }
    } catch (error) {
      console.error("获取应用详情失败:", error);
      proxyRequestError(error, messageApi, "获取应用详情失败");
    } finally {
      setLoading(false);
    }
  }, [appId, messageApi]);

  useEffect(() => {
    loadAppDetail();
  }, [loadAppDetail]);

  // 处理配置变更
  const handleConfigChange = useCallback((data: AppConfigData) => {
    currentConfigRef.current = data;
    if (data.name) {
      setAppName(data.name);
    }
  }, []);

  // 处理调试历史加载
  const handleDebugHistoryLoad = useCallback((history: AppChatHistoryItem[]) => {
    console.log("ChatAppConfig 收到历史记录:", history);
    // 确保传入的是有效数组
    if (Array.isArray(history)) {
      setDebugHistory(history);
    } else {
      setDebugHistory([]);
    }
  }, []);

  // 处理调试历史清空
  const handleDebugHistoryClear = useCallback(() => {
    setDebugHistory([]);
  }, []);

  // 获取当前配置（供调试对话使用）
  const getConfig = useCallback(() => {
    return currentConfigRef.current;
  }, []);

  // 保存配置
  const handleSave = async (data: AppConfigData) => {
    if (!appId) return;

    console.log("保存配置，data:", data);
    console.log("modelId:", data.modelId);

    try {
      setSaveLoading(true);
      const client = GetApiClient();

      const executionSettings: KeyValueString[] = [
        { key: "temperature", value: String(data.temperature ?? 1) },
        { key: "top_p", value: String(data.topP ?? 1) },
        { key: "presence_penalty", value: String(data.presencePenalty ?? 0) },
        { key: "frequency_penalty", value: String(data.frequencyPenalty ?? 0) },
      ];

      const requestBody = {
        teamId,
        appId,
        name: data.name,
        description: data.description,
        avatar: data.avatarKey,
        modelId: data.modelId,
        prompt: data.prompt,
        executionSettings,
        wikiIds: data.wikiIds,
        plugins: data.plugins,
        isPublic: data.isPublic,
        isAuth: data.isAuth,
        classifyId: data.classifyId,
      };

      console.log("保存请求体:", requestBody);

      await client.api.app.team.chatapp.update.post(requestBody);

      messageApi.success("保存成功");
    } catch (error) {
      console.error("保存配置失败:", error);
      proxyRequestError(error, messageApi, "保存配置失败");
    } finally {
      setSaveLoading(false);
    }
  };

  const handleBack = () => {
    navigate(`/app/team/${teamId}/manage_apps`);
  };

  return (
    <>
      {contextHolder}
      <Layout className="app-config-debug-layout">
        {/* 中间：对话调试区域 */}
        <Content className="app-config-debug-content">
          <div className="debug-chat-container">
            {/* 头部 */}
            <div className="debug-chat-header">
              <Space>
                <Button type="text" icon={<ArrowLeftOutlined />} onClick={handleBack} />
                <Title level={4} style={{ margin: 0 }}>
                  {appName || "应用配置"} - 调试
                </Title>
              </Space>
            </div>

            {/* 对话区域 */}
            <Spin spinning={loading} style={{ height: "100%" }}>
              {initialData && (
                <AppDebugChat
                  teamId={teamId}
                  appId={appId}
                  getConfig={getConfig}
                  appAvatar={currentConfigRef.current.avatar}
                  initialHistory={debugHistory}
                  onHistoryCleared={handleDebugHistoryClear}
                />
              )}
            </Spin>
          </div>
        </Content>

        {/* 右侧：配置面板 */}
        <Sider width={380} className="app-config-debug-sider">
          <div className="config-panel-wrapper">
            <div className="config-panel-header">
              <Title level={5} style={{ margin: 0 }}>
                应用配置
              </Title>
            </div>
            <div className="config-panel-content">
              <Spin spinning={loading}>
                {initialData && (
                  <AppConfigPanel
                    teamId={teamId}
                    appId={appId}
                    initialData={initialData}
                    onConfigChange={handleConfigChange}
                    onSave={handleSave}
                    saveLoading={saveLoading}
                    onDebugHistoryLoad={handleDebugHistoryLoad}
                    onDebugHistoryClear={handleDebugHistoryClear}
                  />
                )}
              </Spin>
            </div>
          </div>
        </Sider>
      </Layout>
    </>
  );
}
