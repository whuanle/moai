import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Button,
  message,
  Form,
  Input,
  Space,
  Select,
  Slider,
  Collapse,
  Spin,
  Empty,
  Typography,
  Tag,
  Upload,
  Avatar,
  Switch,
} from "antd";
import type { UploadProps } from "antd";
import {
  RobotOutlined,
  SettingOutlined,
  BookOutlined,
  ApiOutlined,
  SaveOutlined,
  FileTextOutlined,
  CameraOutlined,
  LoadingOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import { GetFileMd5 } from "../../../../helper/Md5Helper";
import type {
  PublicModelInfo,
  QueryWikiInfoResponse,
  PluginSimpleInfo,
  PluginClassifyItem,
  PromptItem,
  AppClassifyItem,
  AppChatHistoryItem,
} from "../../../../apiClient/models";
import { AiModelTypeObject } from "../../../../apiClient/models";
import PromptSelector from "../../../common/PromptSelector";
import "./ChatAppConfigPanel.css";

const { TextArea } = Input;
const { Text } = Typography;

// 应用配置数据类型
export interface AppConfigData {
  appId?: string;
  name?: string;
  description?: string;
  avatar?: string;
  avatarKey?: string;
  modelId?: number;
  prompt?: string;
  temperature?: number;
  topP?: number;
  presencePenalty?: number;
  frequencyPenalty?: number;
  wikiIds?: number[];
  plugins?: string[];
  isPublic?: boolean;
  isForeign?: boolean;
  isAuth?: boolean;
  classifyId?: number;
}

// 表单值类型
interface FormValues {
  name: string;
  description?: string;
  avatarKey?: string;
  modelId: number;
  prompt?: string;
  temperature: number;
  topP: number;
  presencePenalty: number;
  frequencyPenalty: number;
  wikiIds: number[];
  plugins: string[];
  isPublic?: boolean;
  isForeign?: boolean;
  isAuth?: boolean;
  classifyId: number;
}

interface AppConfigPanelProps {
  teamId: number;
  appId?: string;
  initialData?: AppConfigData;
  onSave?: (data: AppConfigData) => void;
  onConfigChange?: (data: AppConfigData) => void;
  saveLoading?: boolean;
  onDebugHistoryLoad?: (history: AppChatHistoryItem[]) => void;
  onDebugHistoryClear?: () => void;
}

export default function AppConfigPanel({
  teamId,
  appId,
  initialData,
  onSave,
  onConfigChange,
  saveLoading = false,
  onDebugHistoryLoad,
  onDebugHistoryClear,
}: AppConfigPanelProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm<FormValues>();

  // UI 状态
  const [promptSelectorOpen, setPromptSelectorOpen] = useState(false);
  const [avatarUrl, setAvatarUrl] = useState<string>();
  const [avatarUploading, setAvatarUploading] = useState(false);
  const [selectedPluginClassId, setSelectedPluginClassId] = useState<number | null>(null);

  // 数据加载状态
  const [models, setModels] = useState<PublicModelInfo[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [wikis, setWikis] = useState<QueryWikiInfoResponse[]>([]);
  const [wikisLoading, setWikisLoading] = useState(false);
  const [plugins, setPlugins] = useState<PluginSimpleInfo[]>([]);
  const [pluginsLoading, setPluginsLoading] = useState(false);
  const [pluginClasses, setPluginClasses] = useState<PluginClassifyItem[]>([]);
  const [pluginClassesLoading, setPluginClassesLoading] = useState(false);
  const [appClassifies, setAppClassifies] = useState<AppClassifyItem[]>([]);
  const [appClassifiesLoading, setAppClassifiesLoading] = useState(false);
  const [clearingHistory, setClearingHistory] = useState(false);

  // 监听表单值变化（用于显示滑块当前值）
  const temperature = Form.useWatch("temperature", form);
  const topP = Form.useWatch("topP", form);
  const presencePenalty = Form.useWatch("presencePenalty", form);
  const frequencyPenalty = Form.useWatch("frequencyPenalty", form);

  // 根据分类筛选插件
  const filteredPlugins = useMemo(() => {
    if (selectedPluginClassId === null) return plugins;
    return plugins.filter((p) => p.classifyId === selectedPluginClassId);
  }, [plugins, selectedPluginClassId]);

  // 通知配置变更
  const notifyConfigChange = useCallback(() => {
    if (!onConfigChange) return;
    const values = form.getFieldsValue();
    onConfigChange({
      appId,
      name: values.name,
      description: values.description,
      avatarKey: values.avatarKey,
      avatar: avatarUrl,
      modelId: values.modelId,
      prompt: values.prompt,
      temperature: values.temperature,
      topP: values.topP,
      presencePenalty: values.presencePenalty,
      frequencyPenalty: values.frequencyPenalty,
      wikiIds: values.wikiIds,
      plugins: values.plugins,
      isPublic: values.isPublic,
      isForeign: values.isForeign,
      isAuth: values.isAuth,
      classifyId: values.classifyId,
    });
  }, [appId, avatarUrl, form, onConfigChange]);

  // 数据加载函数
  const loadModels = useCallback(async () => {
    setModelsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.team.common.team_modellist.post({
        teamId,
        aiModelType: AiModelTypeObject.Chat,
      });
      setModels(response?.aiModels || []);
    } catch (error) {
      console.error("加载模型列表失败:", error);
      proxyRequestError(error, messageApi, "加载模型列表失败");
    } finally {
      setModelsLoading(false);
    }
  }, [teamId, messageApi]);

  const loadWikis = useCallback(async () => {
    setWikisLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.wiki.query_team_wiki_list.post({ teamId });
      setWikis(response || []);
    } catch (error) {
      console.error("加载知识库列表失败:", error);
      proxyRequestError(error, messageApi, "加载知识库列表失败");
    } finally {
      setWikisLoading(false);
    }
  }, [teamId, messageApi]);

  const loadPlugins = useCallback(async () => {
    setPluginsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.team.common.team_pluginlist.post({ teamId });
      setPlugins(response?.plugins || []);
    } catch (error) {
      console.error("加载插件列表失败:", error);
      proxyRequestError(error, messageApi, "加载插件列表失败");
    } finally {
      setPluginsLoading(false);
    }
  }, [teamId, messageApi]);

  const loadPluginClasses = useCallback(async () => {
    setPluginClassesLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      setPluginClasses(response?.items || []);
    } catch (error) {
      console.error("加载插件分类失败:", error);
    } finally {
      setPluginClassesLoading(false);
    }
  }, []);

  const loadAppClassifies = useCallback(async () => {
    setAppClassifiesLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.app.store.classify_list.get();
      setAppClassifies(response?.items || []);
    } catch (error) {
      console.error("加载应用分类失败:", error);
    } finally {
      setAppClassifiesLoading(false);
    }
  }, []);

  // 加载调试历史记录
  const loadDebugHistory = useCallback(async () => {
    if (!appId || !onDebugHistoryLoad) return;
    console.log("开始加载调试历史, appId:", appId, "teamId:", teamId);
    try {
      const client = GetApiClient();
      const response = await client.api.app.team.chatapp.query_debug_history.post({
        teamId,
        appId,
      });
      console.log("调试历史 API 响应:", response);
      // 确保返回的是数组，即使是空数组也正常处理
      if (response && Array.isArray(response.chatHistory)) {
        console.log("调用 onDebugHistoryLoad，历史记录数量:", response.chatHistory.length);
        onDebugHistoryLoad(response.chatHistory);
      } else {
        // 如果没有返回 chatHistory 或格式不对，传递空数组
        console.log("没有返回有效的 chatHistory，传递空数组");
        onDebugHistoryLoad([]);
      }
    } catch (error) {
      console.error("加载调试历史失败:", error);
      // 不显示错误提示，因为可能是首次使用没有历史记录
      // proxyRequestError(error, messageApi, "加载调试历史失败");
      onDebugHistoryLoad([]);
    }
  }, [appId, teamId, onDebugHistoryLoad, messageApi]);

  // 清空调试历史记录
  const handleClearDebugHistory = useCallback(async () => {
    if (!appId || !onDebugHistoryClear) return;
    setClearingHistory(true);
    try {
      const client = GetApiClient();
      await client.api.app.team.chatapp.clear_debug_history.post({
        teamId,
        appId,
      });
      messageApi.success("调试历史已清空");
      onDebugHistoryClear();
    } catch (error) {
      console.error("清空调试历史失败:", error);
      proxyRequestError(error, messageApi, "清空调试历史失败");
    } finally {
      setClearingHistory(false);
    }
  }, [appId, teamId, onDebugHistoryClear, messageApi]);

  // 初始化加载
  useEffect(() => {
    loadModels();
    loadWikis();
    loadPlugins();
    loadPluginClasses();
    loadAppClassifies();
    loadDebugHistory();
  }, [loadModels, loadWikis, loadPlugins, loadPluginClasses, loadAppClassifies, loadDebugHistory]);

  // 初始化表单数据
  useEffect(() => {
    if (!initialData) return;
    
    console.log("AppConfigPanel 收到 initialData:", initialData);
    console.log("modelId 值:", initialData.modelId, "类型:", typeof initialData.modelId);
    console.log("classifyId 值:", initialData.classifyId, "类型:", typeof initialData.classifyId);
    
    setAvatarUrl(initialData.avatar || undefined);
    
    // 构建要设置的值对象，只设置有值的字段
    const formValues: any = {
      name: initialData.name || "",
      description: initialData.description || "",
      avatarKey: initialData.avatarKey || "",
      prompt: initialData.prompt || "",
      temperature: initialData.temperature ?? 1,
      topP: initialData.topP ?? 1,
      presencePenalty: initialData.presencePenalty ?? 0,
      frequencyPenalty: initialData.frequencyPenalty ?? 0,
      wikiIds: initialData.wikiIds || [],
      plugins: initialData.plugins || [],
    };
    
    // 只有当值存在时才设置
    if (initialData.modelId !== undefined && initialData.modelId !== null) {
      formValues.modelId = initialData.modelId;
    }
    if (initialData.classifyId !== undefined && initialData.classifyId !== null) {
      formValues.classifyId = initialData.classifyId;
    }
    if (initialData.isPublic !== undefined) {
      formValues.isPublic = initialData.isPublic;
    }
    if (initialData.isForeign !== undefined) {
      formValues.isForeign = initialData.isForeign;
    }
    if (initialData.isAuth !== undefined) {
      formValues.isAuth = initialData.isAuth;
    }
    
    console.log("准备设置的表单值:", formValues);
    form.setFieldsValue(formValues);
    
    // 验证设置后的表单值
    setTimeout(() => {
      console.log("表单设置后的值:", form.getFieldsValue());
    }, 100);
  }, [initialData, form]);

  // 初始化表单数据后通知配置变更
  useEffect(() => {
    if (!initialData || !onConfigChange) return;
    // 延迟一帧确保表单值已更新
    const timer = setTimeout(() => {
      notifyConfigChange();
    }, 0);
    return () => clearTimeout(timer);
  }, [initialData, onConfigChange, notifyConfigChange]);

  // 头像上传处理
  const handleAvatarUpload = async (file: File) => {
    setAvatarUploading(true);
    try {
      const client = GetApiClient();
      const md5 = await GetFileMd5(file);
      const preUploadResponse = await client.api.storage.public.pre_upload_image.post({
        fileName: file.name,
        fileSize: file.size,
        contentType: file.type,
        mD5: md5,
      });

      if (!preUploadResponse) throw new Error("获取上传地址失败");

      const avatarPath = preUploadResponse.objectKey!;
      if (!preUploadResponse.isExist) {
        const uploadResponse = await fetch(preUploadResponse.uploadUrl!, {
          method: "PUT",
          body: file,
          headers: { "Content-Type": file.type },
        });
        if (!uploadResponse.ok) throw new Error("上传文件失败");
        await client.api.storage.complate_url.post({
          fileId: preUploadResponse.fileId,
          isSuccess: true,
        });
      }

      form.setFieldValue("avatarKey", avatarPath);
      setAvatarUrl(URL.createObjectURL(file));
      messageApi.success("头像上传成功");
      notifyConfigChange();
    } catch (error) {
      console.error("上传头像失败:", error);
      proxyRequestError(error, messageApi, "上传头像失败");
    } finally {
      setAvatarUploading(false);
    }
  };

  const beforeUpload: UploadProps["beforeUpload"] = (file) => {
    if (!file.type.startsWith("image/")) {
      messageApi.error("只能上传图片文件");
      return false;
    }
    if (file.size / 1024 / 1024 >= 5) {
      messageApi.error("图片大小不能超过 5MB");
      return false;
    }
    handleAvatarUpload(file);
    return false;
  };

  // 提示词选择
  const handlePromptSelect = (prompt: PromptItem) => {
    if (prompt.content) {
      form.setFieldsValue({ prompt: prompt.content });
      messageApi.success(`已选择提示词: ${prompt.name}`);
      notifyConfigChange();
    }
  };

  // 保存配置
  const handleSave = (values: FormValues) => {
    console.log("表单验证通过，准备保存:", values);
    
    if (!values.modelId) {
      messageApi.error("请选择AI模型");
      return;
    }
    
    if (!values.classifyId) {
      messageApi.error("请选择应用分类");
      return;
    }
    
    onSave?.({
      appId,
      name: values.name,
      description: values.description,
      avatarKey: values.avatarKey,
      avatar: avatarUrl,
      modelId: values.modelId,
      prompt: values.prompt,
      temperature: values.temperature,
      topP: values.topP,
      presencePenalty: values.presencePenalty,
      frequencyPenalty: values.frequencyPenalty,
      wikiIds: values.wikiIds,
      plugins: values.plugins,
      isPublic: values.isPublic,
      isForeign: values.isForeign,
      isAuth: values.isAuth,
      classifyId: values.classifyId,
    });
  };

  // 折叠面板配置
  const collapseItems = [
    {
      key: "basic",
      label: (
        <Space>
          <RobotOutlined />
          <span>基本信息</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">配置应用的基本信息和系统提示词</Text>

          <div className="config-item">
            <Text strong>应用头像</Text>
            <Form.Item name="avatarKey" hidden>
              <Input />
            </Form.Item>
            <Upload
              showUploadList={false}
              beforeUpload={beforeUpload}
              accept="image/*"
              disabled={avatarUploading}
            >
              <div className="app-avatar-upload-wrapper">
                <Avatar
                  size={64}
                  src={avatarUrl}
                  icon={avatarUploading ? <LoadingOutlined /> : <RobotOutlined />}
                />
                <div className="app-avatar-upload-overlay">
                  {avatarUploading ? <LoadingOutlined /> : <CameraOutlined />}
                  <span>{avatarUploading ? "上传中..." : "更换"}</span>
                </div>
              </div>
            </Upload>
          </div>

          <div className="config-item">
            <Text strong>应用名称</Text>
            <Form.Item
              name="name"
              rules={[{ required: true, message: "请输入应用名称" }]}
              style={{ marginBottom: 0 }}
            >
              <Input placeholder="请输入应用名称" maxLength={50} onChange={() => notifyConfigChange()} />
            </Form.Item>
          </div>

          <div className="config-item">
            <Text strong>应用描述</Text>
            <Form.Item name="description" style={{ marginBottom: 0 }}>
              <TextArea placeholder="请输入应用描述" rows={2} maxLength={200} onChange={() => notifyConfigChange()} />
            </Form.Item>
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text strong>应用分类</Text>
              <Tag color="red">* 必选</Tag>
            </div>
            <Spin spinning={appClassifiesLoading}>
              <Form.Item
                name="classifyId"
                rules={[{ required: true, message: "请选择应用分类" }]}
                style={{ marginBottom: 0 }}
              >
                <Select
                  placeholder="请选择分类"
                  options={appClassifies.map((item) => ({
                    label: item.name,
                    value: item.classifyId,
                  }))}
                  onChange={() => notifyConfigChange()}
                />
              </Form.Item>
            </Spin>
          </div>

          {initialData?.isForeign && (
            <div className="config-item">
              <div className="config-label">
                <Text strong>是否开启鉴权</Text>
              </div>
              <Text type="secondary" className="config-hint">
                开启后，外部应用访问时需要进行身份验证
              </Text>
              <Form.Item name="isAuth" valuePropName="checked" style={{ marginBottom: 0 }}>
                <Switch onChange={() => notifyConfigChange()} />
              </Form.Item>
            </div>
          )}

          <div className="config-item">
            <div className="config-label">
              <Text strong>系统提示词</Text>
              <Button
                type="link"
                size="small"
                icon={<FileTextOutlined />}
                onClick={() => setPromptSelectorOpen(true)}
              >
                选择提示词
              </Button>
            </div>
            <Text type="secondary" className="config-hint">
              设定AI助手的角色和行为，这将影响AI的回复风格和内容。
            </Text>
            <Form.Item name="prompt" style={{ marginBottom: 0 }}>
              <TextArea
                placeholder="例如：你是一个专业的编程助手，擅长解释技术概念和提供代码示例..."
                rows={4}
                onChange={() => notifyConfigChange()}
              />
            </Form.Item>
          </div>
        </div>
      ),
    },
    {
      key: "model",
      label: (
        <Space>
          <SettingOutlined />
          <span>模型配置</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">选择AI模型和配置参数</Text>
          <Text type="warning" className="config-hint" style={{ display: "block", marginTop: 4 }}>
            注意：并非所有模型都支持以下参数配置
          </Text>

          <div className="config-item">
            <div className="config-label">
              <Text strong>模型设置</Text>
              <Tag color="red">* 必选</Tag>
            </div>
            <Text type="secondary" className="config-hint">
              请先选择AI模型才能开始对话
            </Text>
            <Spin spinning={modelsLoading}>
              <Form.Item
                name="modelId"
                rules={[{ required: true, message: "请选择AI模型" }]}
                style={{ marginBottom: 0 }}
              >
                <Select
                  placeholder="选择模型"
                  options={models.map((m) => ({ label: m.title || m.name, value: m.id }))}
                  onChange={() => notifyConfigChange()}
                />
              </Form.Item>
            </Spin>
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>temperature</Text>
              <Text type="secondary">{temperature ?? 1}</Text>
            </div>
            <Text type="secondary" className="config-hint">随机性：值越大，回复越随机</Text>
            <Form.Item name="temperature" style={{ marginBottom: 0 }}>
              <Slider min={0} max={2} step={0.1} onChangeComplete={() => notifyConfigChange()} />
            </Form.Item>
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>top_p</Text>
              <Text type="secondary">{topP ?? 1}</Text>
            </div>
            <Text type="secondary" className="config-hint">核采样：与随机性类似，但不要和随机性一起更改</Text>
            <Form.Item name="topP" style={{ marginBottom: 0 }}>
              <Slider min={0} max={1} step={0.1} onChangeComplete={() => notifyConfigChange()} />
            </Form.Item>
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>presence_penalty</Text>
              <Text type="secondary">{presencePenalty ?? 0}</Text>
            </div>
            <Text type="secondary" className="config-hint">话题新鲜度：值越大，越有可能扩展到新话题</Text>
            <Form.Item name="presencePenalty" style={{ marginBottom: 0 }}>
              <Slider min={-2} max={2} step={0.1} onChangeComplete={() => notifyConfigChange()} />
            </Form.Item>
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>frequency_penalty</Text>
              <Text type="secondary">{frequencyPenalty ?? 0}</Text>
            </div>
            <Text type="secondary" className="config-hint">频率惩罚度：值越大，越有可能降低重复字词</Text>
            <Form.Item name="frequencyPenalty" style={{ marginBottom: 0 }}>
              <Slider min={-2} max={2} step={0.1} onChangeComplete={() => notifyConfigChange()} />
            </Form.Item>
          </div>
        </div>
      ),
    },
    {
      key: "wiki",
      label: (
        <Space>
          <BookOutlined />
          <span>知识库</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">选择知识库来增强AI助手的回答能力</Text>
          <Spin spinning={wikisLoading}>
            {wikis.length === 0 ? (
              <Empty description="暂无知识库" />
            ) : (
              <div className="config-item">
                <Form.Item name="wikiIds" style={{ marginBottom: 0 }}>
                  <Select
                    mode="multiple"
                    placeholder="选择知识库"
                    options={wikis.map((w) => ({ label: w.name, value: w.wikiId }))}
                    onChange={() => notifyConfigChange()}
                  />
                </Form.Item>
              </div>
            )}
          </Spin>
        </div>
      ),
    },
    {
      key: "plugin",
      label: (
        <Space>
          <ApiOutlined />
          <span>插件</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">选择插件来增强AI助手的功能，最多可选择3个插件</Text>
          <Spin spinning={pluginsLoading || pluginClassesLoading}>
            <div className="config-item">
              <Text>插件分类</Text>
              <Select
                placeholder="全部分类"
                allowClear
                value={selectedPluginClassId}
                onChange={(value) => setSelectedPluginClassId(value ?? null)}
                options={[
                  { label: "全部", value: null },
                  ...pluginClasses.map((c) => ({ label: c.name, value: c.classifyId })),
                ]}
              />
            </div>
            <div className="config-item">
              <Text>选择插件</Text>
              {filteredPlugins.length === 0 ? (
                <Empty description="暂无插件" />
              ) : (
                <Form.Item name="plugins" style={{ marginBottom: 0 }}>
                  <Select
                    mode="multiple"
                    placeholder="选择插件"
                    maxCount={3}
                    options={filteredPlugins.map((p) => ({ label: p.title || p.pluginName, value: p.pluginName }))}
                    onChange={() => notifyConfigChange()}
                  />
                </Form.Item>
              )}
            </div>
          </Spin>
        </div>
      ),
    },
  ];

  return (
    <div className="app-config-panel">
      {contextHolder}
      <Form
        form={form}
        layout="vertical"
        onFinish={handleSave}
        initialValues={{
          temperature: 1,
          topP: 1,
          presencePenalty: 0,
          frequencyPenalty: 0,
          wikiIds: [],
          plugins: [],
        }}
      >
        {/* 所有表单字段都在这里，但用 hidden 隐藏 */}
        <Form.Item name="name" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="description" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="avatarKey" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="modelId" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="prompt" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="temperature" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="topP" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="presencePenalty" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="frequencyPenalty" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="wikiIds" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="plugins" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="isPublic" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="isForeign" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="isAuth" hidden>
          <Input />
        </Form.Item>
        <Form.Item name="classifyId" hidden>
          <Input />
        </Form.Item>

        <Collapse
          defaultActiveKey={["basic"]}
          items={collapseItems}
          bordered={false}
          className="app-config-collapse"
          destroyInactivePanel={false}
        />
        {onSave && (
          <div className="config-panel-footer">
            <Space direction="vertical" style={{ width: "100%" }}>
              <Button
                type="primary"
                icon={<SaveOutlined />}
                loading={saveLoading}
                onClick={() => form.submit()}
                block
              >
                保存配置
              </Button>
              {onDebugHistoryClear && (
                <Button
                  danger
                  loading={clearingHistory}
                  onClick={handleClearDebugHistory}
                  block
                >
                  清空调试历史
                </Button>
              )}
            </Space>
          </div>
        )}
      </Form>

      <PromptSelector
        open={promptSelectorOpen}
        onClose={() => setPromptSelectorOpen(false)}
        onSelect={handlePromptSelect}
      />
    </div>
  );
}
