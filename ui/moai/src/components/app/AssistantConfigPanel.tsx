import React, { useState, useEffect, useCallback, useMemo } from "react";
import {
  Typography,
  Select,
  Slider,
  Input,
  Collapse,
  Tag,
  Space,
  Spin,
  Empty,
  message,
  Button,
} from "antd";
import {
  RobotOutlined,
  SettingOutlined,
  BookOutlined,
  ApiOutlined,
  FileTextOutlined,
  SaveOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../ServiceClient";
import { proxyRequestError } from "../../helper/RequestError";
import type {
  PublicModelInfo,
  QueryWikiInfoResponse,
  PluginSimpleInfo,
  PluginClassifyItem,
  PromptItem,
  KeyValueString,
} from "../../apiClient/models";
import { AiModelTypeObject } from "../../apiClient/models";
import PromptSelector from "../common/PromptSelector";

const { Text, Title } = Typography;
const { TextArea } = Input;

export interface AssistantConfig {
  modelId?: number;
  title?: string;
  systemPrompt?: string;
  temperature: number;
  topP: number;
  presencePenalty: number;
  frequencyPenalty: number;
  selectedWikiIds: number[];
  selectedPluginIds: string[];
}

interface AssistantConfigPanelProps {
  config: AssistantConfig;
  onConfigChange: (config: AssistantConfig) => void;
  chatId?: string | null;
  tokenStats?: {
    total: number;
    input: number;
    output: number;
  };
}

const AssistantConfigPanel: React.FC<AssistantConfigPanelProps> = ({
  config,
  onConfigChange,
  chatId,
  tokenStats = { total: 0, input: 0, output: 0 },
}) => {
  const [messageApi, contextHolder] = message.useMessage();

  // 提示词选择器状态
  const [promptSelectorOpen, setPromptSelectorOpen] = useState(false);
  
  // 保存状态
  const [saving, setSaving] = useState(false);

  // 数据加载状态
  const [models, setModels] = useState<PublicModelInfo[]>([]);
  const [modelsLoading, setModelsLoading] = useState(false);
  const [wikis, setWikis] = useState<QueryWikiInfoResponse[]>([]);
  const [wikisLoading, setWikisLoading] = useState(false);
  const [plugins, setPlugins] = useState<PluginSimpleInfo[]>([]);
  const [pluginsLoading, setPluginsLoading] = useState(false);
  const [pluginClasses, setPluginClasses] = useState<PluginClassifyItem[]>([]);
  const [pluginClassesLoading, setPluginClassesLoading] = useState(false);
  const [selectedPluginClassId, setSelectedPluginClassId] = useState<number | null>(null);

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

  // 加载知识库列表
  const loadWikis = useCallback(async () => {
    setWikisLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.wiki.query_wiki_list.post({});
      if (response) {
        setWikis(response);
      }
    } catch (error) {
      console.error("加载知识库列表失败:", error);
      proxyRequestError(error, messageApi, "加载知识库列表失败");
    } finally {
      setWikisLoading(false);
    }
  }, [messageApi]);

  // 加载插件列表
  const loadPlugins = useCallback(async () => {
    setPluginsLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.plugin_list.get();
      if (response?.items) {
        setPlugins(response.items);
      }
    } catch (error) {
      console.error("加载插件列表失败:", error);
      proxyRequestError(error, messageApi, "加载插件列表失败");
    } finally {
      setPluginsLoading(false);
    }
  }, [messageApi]);

  // 加载插件分类列表
  const loadPluginClasses = useCallback(async () => {
    setPluginClassesLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      if (response?.items) {
        setPluginClasses(response.items);
      }
    } catch (error) {
      console.error("加载插件分类失败:", error);
      proxyRequestError(error, messageApi, "加载插件分类失败");
    } finally {
      setPluginClassesLoading(false);
    }
  }, [messageApi]);

  // 根据分类筛选插件
  const filteredPlugins = useMemo(() => {
    if (selectedPluginClassId === null) {
      return plugins;
    }
    return plugins.filter((p) => p.classifyId === selectedPluginClassId);
  }, [plugins, selectedPluginClassId]);

  useEffect(() => {
    loadModels();
    loadWikis();
    loadPlugins();
    loadPluginClasses();
  }, [loadModels, loadWikis, loadPlugins, loadPluginClasses]);

  const updateConfig = (partial: Partial<AssistantConfig>) => {
    onConfigChange({ ...config, ...partial });
  };

  // 处理提示词选择
  const handlePromptSelect = (prompt: PromptItem) => {
    if (prompt.content) {
      updateConfig({ systemPrompt: prompt.content });
      messageApi.success(`已选择提示词: ${prompt.name}`);
    }
  };

  // 保存设定
  const handleSaveConfig = async () => {
    if (!chatId) {
      messageApi.warning("请先选择或创建一个对话");
      return;
    }

    setSaving(true);
    try {
      const client = GetApiClient();
      
      // 构建 executionSettings
      const executionSettings: KeyValueString[] = [
        { key: "temperature", value: String(config.temperature) },
        { key: "top_p", value: String(config.topP) },
        { key: "presence_penalty", value: String(config.presencePenalty) },
        { key: "frequency_penalty", value: String(config.frequencyPenalty) },
      ];

      await client.api.app.assistant.update_chat.post({
        chatId: chatId,
        modelId: config.modelId,
        title: config.title,
        prompt: config.systemPrompt,
        executionSettings: executionSettings,
        wikiIds: config.selectedWikiIds,
        plugins: config.selectedPluginIds,
      });

      messageApi.success("保存设定成功");
    } catch (error) {
      console.error("保存设定失败:", error);
      proxyRequestError(error, messageApi, "保存设定失败");
    } finally {
      setSaving(false);
    }
  };

  const collapseItems = [
    {
      key: "assistant",
      label: (
        <Space>
          <RobotOutlined />
          <span>助手设定</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">配置AI助手的头像和系统提示词</Text>
          <div className="token-stats">
            <Title level={5}>Token统计</Title>
            <div className="token-stats-row">
              <div className="token-stat-item">
                <Text strong>{tokenStats.total}</Text>
                <Text type="secondary">总计</Text>
              </div>
              <div className="token-stat-item">
                <Text strong>{tokenStats.input}</Text>
                <Text type="secondary">输入</Text>
              </div>
              <div className="token-stat-item">
                <Text strong>{tokenStats.output}</Text>
                <Text type="secondary">输出</Text>
              </div>
            </div>
          </div>
          
          <div className="config-item">
            <Text strong>对话标题</Text>
            <Input
              placeholder="输入对话标题"
              value={config.title}
              onChange={(e) => updateConfig({ title: e.target.value })}
            />
          </div>
          
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
            <TextArea
              placeholder="例如：你是一个专业的编程助手，擅长解释技术概念和提供代码示例..."
              rows={4}
              value={config.systemPrompt}
              onChange={(e) => updateConfig({ systemPrompt: e.target.value })}
            />
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
          <Text type="warning" style={{ fontSize: 12 }}>
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
              <Select
                placeholder="选择模型"
                style={{ width: "100%" }}
                value={config.modelId}
                onChange={(value) => updateConfig({ modelId: value })}
                options={models.map((m) => ({
                  label: m.title || m.name,
                  value: m.id,
                }))}
              />
            </Spin>
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>temperature</Text>
              <Text type="secondary">{config.temperature}</Text>
            </div>
            <Text type="secondary" className="config-hint">
              随机性：值越大，回复越随机
            </Text>
            <Slider
              min={0}
              max={2}
              step={0.1}
              value={config.temperature}
              onChange={(value) => updateConfig({ temperature: value })}
            />
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>top_p</Text>
              <Text type="secondary">{config.topP}</Text>
            </div>
            <Text type="secondary" className="config-hint">
              核采样：与随机性类似，但不要和随机性一起更改
            </Text>
            <Slider
              min={0}
              max={1}
              step={0.1}
              value={config.topP}
              onChange={(value) => updateConfig({ topP: value })}
            />
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>presence_penalty</Text>
              <Text type="secondary">{config.presencePenalty}</Text>
            </div>
            <Text type="secondary" className="config-hint">
              话题新鲜度：值越大，越有可能扩展到新话题
            </Text>
            <Slider
              min={-2}
              max={2}
              step={0.1}
              value={config.presencePenalty}
              onChange={(value) => updateConfig({ presencePenalty: value })}
            />
          </div>

          <div className="config-item">
            <div className="config-label">
              <Text>frequency_penalty</Text>
              <Text type="secondary">{config.frequencyPenalty}</Text>
            </div>
            <Text type="secondary" className="config-hint">
              频率惩罚度：值越大，越有可能降低重复字词
            </Text>
            <Slider
              min={-2}
              max={2}
              step={0.1}
              value={config.frequencyPenalty}
              onChange={(value) => updateConfig({ frequencyPenalty: value })}
            />
          </div>
        </div>
      ),
    },
    {
      key: "wiki",
      label: (
        <Space>
          <BookOutlined />
          <span>知识库选择</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">选择知识库来增强AI助手的回答能力</Text>
          <Spin spinning={wikisLoading}>
            {wikis.length === 0 ? (
              <Empty description="未选择知识库" />
            ) : (
              <Select
                mode="multiple"
                placeholder="选择知识库"
                style={{ width: "100%" }}
                value={config.selectedWikiIds}
                onChange={(value) => updateConfig({ selectedWikiIds: value })}
                options={wikis.map((w) => ({
                  label: w.name,
                  value: w.wikiId,
                }))}
              />
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
          <span>插件添加</span>
        </Space>
      ),
      children: (
        <div className="config-section">
          <Text type="secondary">
            选择插件来增强AI助手的功能，最多可选择3个插件
          </Text>
          <Spin spinning={pluginsLoading || pluginClassesLoading}>
            <div className="config-item">
              <Text>插件分类</Text>
              <Select
                placeholder="全部分类"
                style={{ width: "100%" }}
                allowClear
                value={selectedPluginClassId}
                onChange={(value) => setSelectedPluginClassId(value ?? null)}
                options={[
                  { label: "全部", value: null },
                  ...pluginClasses.map((c) => ({
                    label: c.name,
                    value: c.classifyId,
                  })),
                ]}
              />
            </div>
            <div className="config-item">
              <Text>选择插件</Text>
              {filteredPlugins.length === 0 ? (
                <Empty description="暂无插件" />
              ) : (
                <Select
                  mode="multiple"
                  placeholder="选择插件"
                  style={{ width: "100%" }}
                  maxCount={3}
                  value={config.selectedPluginIds}
                  onChange={(value) => updateConfig({ selectedPluginIds: value })}
                  options={filteredPlugins.map((p) => ({
                    label: p.title || p.pluginName,
                    value: p.pluginName,
                  }))}
                />
              )}
            </div>
          </Spin>
        </div>
      ),
    },
  ];

  return (
    <div className="assistant-config-panel">
      {contextHolder}
      <div className="config-panel-header">
        <Title level={5}>助手设定</Title>
      </div>
      <Collapse
        defaultActiveKey={["model"]}
        items={collapseItems}
        bordered={false}
      />
      
      {/* 保存按钮 */}
      <div className="config-panel-footer">
        <Button
          type="primary"
          icon={<SaveOutlined />}
          onClick={handleSaveConfig}
          loading={saving}
          block
        >
          保存设定
        </Button>
      </div>
      
      {/* 提示词选择器 */}
      <PromptSelector
        open={promptSelectorOpen}
        onClose={() => setPromptSelectorOpen(false)}
        onSelect={handlePromptSelect}
      />
    </div>
  );
};

export default AssistantConfigPanel;
