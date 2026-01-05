/**
 * 创建插件核心内容组件
 * 三栏布局：分类侧边栏 | 模板列表 | 配置表单
 */
import { useState, useCallback, useMemo, useEffect } from "react";
import {
  Form,
  Input,
  Select,
  Switch,
  Button,
  Space,
  Typography,
  Tag,
  Empty,
  Spin,
  Divider,
  Row,
  Col,
  Alert,
  message,
  Badge,
} from "antd";
import {
  AppstoreOutlined,
  CheckCircleFilled,
  RocketOutlined,
  SettingOutlined,
} from "@ant-design/icons";
import type { FormInstance } from "antd";
import type {
  NativePluginTemplateInfo,
  NativePluginConfigFieldTemplate,
  PluginClassifyItem,
} from "../../../../../apiClient/models";
import { PluginTypeObject } from "../../../../../apiClient/models";
import { GetApiClient } from "../../../../ServiceClient";
import {
  proxyRequestError,
  proxyFormRequestError,
} from "../../../../../helper/RequestError";
import { TemplateItem, ClassifyList } from "../TemplatePlugin";
import ParamFormItem from "./ParamFormItem";
import "./CreatePluginContent.css";

const { Text, Title } = Typography;

interface CreatePluginContentProps {
  /** 关闭/返回 */
  onClose?: () => void;
  /** 创建成功回调 */
  onSuccess?: () => void;
  /** 打开代码编辑器 */
  onOpenCodeEditor?: (
    fieldKey: string,
    currentValue: string,
    formInstance: FormInstance
  ) => void;
}

export default function CreatePluginContent({
  onSuccess,
  onOpenCodeEditor,
}: CreatePluginContentProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  // 模板数据状态
  const [templateList, setTemplateList] = useState<NativePluginTemplateInfo[]>(
    []
  );
  const [templateClassify, setTemplateClassify] =
    useState<TemplateItem[]>(ClassifyList);
  const [templateLoading, setTemplateLoading] = useState(false);
  const [selectedClassify, setSelectedClassify] = useState("all");
  const [selectedTemplate, setSelectedTemplate] =
    useState<NativePluginTemplateInfo | null>(null);
  const [templateParams, setTemplateParams] = useState<
    NativePluginConfigFieldTemplate[]
  >([]);
  const [paramsLoading, setParamsLoading] = useState(false);

  // 分类列表
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);
  const [classifyLoading, setClassifyLoading] = useState(false);

  // 创建状态
  const [createLoading, setCreateLoading] = useState(false);

  // 获取分类列表
  const fetchClassifyList = useCallback(async () => {
    setClassifyLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items);
      }
    } catch (error) {
      console.error("获取分类列表失败:", error);
      proxyRequestError(error, messageApi, "获取分类列表失败");
    } finally {
      setClassifyLoading(false);
    }
  }, [messageApi]);

  // 获取模板列表
  const fetchTemplateList = useCallback(async () => {
    setTemplateLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.admin.native_plugin.template_list.post(
        {}
      );

      if (response?.plugins) {
        setTemplateList(response.plugins);
      }

      // 更新分类计数
      const templateItems: TemplateItem[] = ClassifyList.map((item) => ({
        ...item,
        count: 0,
      }));

      response?.classifyCount?.forEach((cv) => {
        const item = templateItems.find(
          (t) => t.key.toLowerCase() === cv.key?.toLowerCase()
        );
        if (item && typeof cv.value === "number") {
          item.count = cv.value;
        }
      });

      setTemplateClassify(templateItems);
    } catch (error) {
      console.error("获取模板列表失败:", error);
      proxyRequestError(error, messageApi, "获取模板列表失败");
    } finally {
      setTemplateLoading(false);
    }
  }, [messageApi]);

  // 获取模板参数
  const fetchTemplateParams = useCallback(
    async (templateKey: string) => {
      setParamsLoading(true);
      try {
        const client = GetApiClient();
        const response =
          await client.api.admin.native_plugin.template_params.post({
            templatePluginKey: templateKey,
          });
        setTemplateParams(response?.fieldTemplates || []);
      } catch (error) {
        console.error("获取模板参数失败:", error);
        proxyRequestError(error, messageApi, "获取模板参数失败");
        setTemplateParams([]);
      } finally {
        setParamsLoading(false);
      }
    },
    [messageApi]
  );

  // 初始化数据
  useEffect(() => {
    fetchTemplateList();
    fetchClassifyList();
  }, [fetchTemplateList, fetchClassifyList]);

  // 选择模板
  const handleTemplateSelect = useCallback(
    (template: NativePluginTemplateInfo) => {
      setSelectedTemplate(template);
      form.setFieldsValue({
        name: template.key || "",
        title: template.name || "",
        description: template.description || "",
      });
      if (template.key) {
        fetchTemplateParams(template.key);
      }
    },
    [fetchTemplateParams, form]
  );

  // 创建插件
  const handleCreate = useCallback(async () => {
    if (!selectedTemplate) {
      messageApi.error("请先选择模板");
      return;
    }
    try {
      const values = await form.validateFields();
      setCreateLoading(true);

      const paramsObj: Record<string, unknown> = {};
      templateParams.forEach((param) => {
        if (param.key && values[param.key] != null) {
          paramsObj[param.key] = values[param.key];
        }
      });

      const client = GetApiClient();
      const response = await client.api.admin.native_plugin.create.post({
        templatePluginKey: selectedTemplate.key || undefined,
        name: values.name,
        title: values.title,
        description: values.description,
        classifyId: values.classifyId,
        isPublic: values.isPublic ?? true,
        config:
          Object.keys(paramsObj).length > 0
            ? JSON.stringify(paramsObj)
            : undefined,
      });

      if (response?.value !== undefined) {
        messageApi.success("内置插件创建成功");
        onSuccess?.();
        form.resetFields();
        setSelectedTemplate(null);
        setTemplateParams([]);
      }
    } catch (error) {
      console.error("创建插件失败:", error);
      proxyFormRequestError(error, messageApi, form, "创建插件失败");
    } finally {
      setCreateLoading(false);
    }
  }, [selectedTemplate, form, templateParams, messageApi, onSuccess]);

  // 按分类分组模板
  const groupedTemplates = useMemo(() => {
    const groups: Record<string, NativePluginTemplateInfo[]> = {};
    templateList.forEach((template) => {
      const classify = template.classify ? String(template.classify) : "未分类";
      if (!groups[classify]) {
        groups[classify] = [];
      }
      groups[classify].push(template);
    });
    return groups;
  }, [templateList]);

  // 当前选中分类的模板列表
  const currentTemplates = useMemo(() => {
    if (selectedClassify === "all") {
      return templateList;
    }
    return groupedTemplates[selectedClassify] || [];
  }, [selectedClassify, groupedTemplates, templateList]);

  // 截断文本
  const truncateText = (
    text: string | null | undefined,
    maxLength: number = 100
  ): string => {
    if (!text) return "暂无描述";
    if (text.length <= maxLength) return text;
    return text.substring(0, maxLength) + "...";
  };

  // 分类数据（包含全部选项）
  const classifyData = useMemo(
    () => [
      {
        key: "all",
        name: "全部模板",
        icon: "📋",
        count: templateList.length,
      },
      ...templateClassify,
    ],
    [templateClassify, templateList.length]
  );

  const isToolPlugin =
    selectedTemplate?.pluginType === PluginTypeObject.ToolPlugin;

  return (
    <>
      {contextHolder}
      <Spin spinning={templateLoading || classifyLoading}>
        <div className="create-plugin-layout">
          {/* 左侧分类侧边栏 */}
          <aside className="create-plugin-sidebar">
            <div className="sidebar-header">
              <AppstoreOutlined className="sidebar-header-icon" />
              <span>插件分类</span>
            </div>
            <nav className="sidebar-nav">
              {classifyData.map((item) => {
                const isSelected = selectedClassify === item.key;
                return (
                  <div
                    key={item.key}
                    className={`sidebar-nav-item ${isSelected ? "active" : ""}`}
                    onClick={() => setSelectedClassify(item.key)}
                  >
                    <span className="nav-item-icon">{item.icon}</span>
                    <span className="nav-item-name">{item.name}</span>
                    <Badge
                      count={item.count}
                      showZero
                      color={isSelected ? "#1677ff" : "#d9d9d9"}
                      className="nav-item-badge"
                    />
                  </div>
                );
              })}
            </nav>
          </aside>

          {/* 中间模板列表 */}
          <section className="create-plugin-templates">
            <div className="templates-header">
              <Title level={5} className="templates-title">
                选择模板
              </Title>
              <Text type="secondary" className="templates-subtitle">
                共 {currentTemplates.length} 个模板
              </Text>
            </div>
            <div className="templates-grid">
              {currentTemplates.length > 0 ? (
                currentTemplates.map((template: NativePluginTemplateInfo) => {
                  const isTool =
                    template.pluginType === PluginTypeObject.ToolPlugin;
                  const isSelected = selectedTemplate?.key === template.key;

                  return (
                    <div
                      key={template.key}
                      className={`template-card ${isSelected ? "selected" : ""} ${isTool ? "tool-type" : ""}`}
                      onClick={() => !isTool && handleTemplateSelect(template)}
                    >
                      {isSelected && (
                        <CheckCircleFilled className="template-check-icon" />
                      )}
                      {isTool && (
                        <Tag color="orange" className="template-type-tag">
                          Tool
                        </Tag>
                      )}
                      <div className="template-card-header">
                        <Text strong className="template-name">
                          {template.name}
                        </Text>
                        <Tag color="geekblue" className="template-key-tag">
                          {template.key}
                        </Tag>
                      </div>
                      <Text
                        type="secondary"
                        className="template-desc"
                        title={template.description || ""}
                      >
                        {truncateText(template.description, 60)}
                      </Text>
                    </div>
                  );
                })
              ) : (
                <div className="templates-empty">
                  <Empty
                    description="该分类下暂无模板"
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                  />
                </div>
              )}
            </div>
          </section>

          {/* 右侧配置表单 */}
          <section className="create-plugin-form">
            {selectedTemplate ? (
              <Spin spinning={paramsLoading}>
                <div className="form-container">
                  <div className="form-header">
                    <div className="form-header-info">
                      <SettingOutlined className="form-header-icon" />
                      <div className="form-header-text">
                        <Title level={5} className="form-title">
                          配置插件
                        </Title>
                        <Space size={4}>
                          <Text type="secondary">基于模板:</Text>
                          <Tag color="purple">{selectedTemplate.name}</Tag>
                        </Space>
                      </div>
                    </div>
                    {!isToolPlugin && (
                      <Button
                        type="primary"
                        size="large"
                        icon={<RocketOutlined />}
                        onClick={handleCreate}
                        loading={createLoading}
                        className="form-submit-btn"
                      >
                        创建插件
                      </Button>
                    )}
                  </div>

                  {isToolPlugin && (
                    <Alert
                      message="工具类型插件"
                      description="该插件为工具类型，无需配置即可使用，不支持创建实例。"
                      type="info"
                      showIcon
                      className="form-alert"
                    />
                  )}

                  <Form
                    form={form}
                    layout="vertical"
                    disabled={isToolPlugin}
                    initialValues={{ isPublic: true }}
                    className="plugin-form"
                  >
                    <div className="form-section">
                      <div className="form-section-title">基本信息</div>
                      <Row gutter={16}>
                        <Col span={12}>
                          <Form.Item
                            name="name"
                            label="插件名称"
                            tooltip="仅限字母和下划线，用于 AI 识别"
                            rules={[
                              { required: true, message: "请输入插件名称" },
                              {
                                pattern: /^[a-zA-Z_]+$/,
                                message: "仅限字母和下划线",
                              },
                              { max: 30, message: "不超过30个字符" },
                            ]}
                          >
                            <Input placeholder="例如: weather_query" />
                          </Form.Item>
                        </Col>
                        <Col span={12}>
                          <Form.Item
                            name="title"
                            label="显示标题"
                            tooltip="支持中文，用于界面展示"
                            rules={[
                              { required: true, message: "请输入显示标题" },
                            ]}
                          >
                            <Input placeholder="例如: 天气查询" />
                          </Form.Item>
                        </Col>
                      </Row>

                      <Form.Item name="description" label="插件描述">
                        <Input.TextArea
                          rows={3}
                          placeholder="描述插件的功能和用途..."
                          showCount
                          maxLength={500}
                        />
                      </Form.Item>

                      <Row gutter={16}>
                        <Col span={12}>
                          <Form.Item
                            name="classifyId"
                            label="所属分类"
                            rules={[{ required: true, message: "请选择分类" }]}
                          >
                            <Select placeholder="选择分类" allowClear>
                              {classifyList.map((item) => (
                                <Select.Option
                                  key={item.classifyId}
                                  value={item.classifyId}
                                >
                                  {item.name}
                                </Select.Option>
                              ))}
                            </Select>
                          </Form.Item>
                        </Col>
                        <Col span={12}>
                          <Form.Item
                            name="isPublic"
                            label="可见性"
                            valuePropName="checked"
                          >
                            <Switch
                              checkedChildren="公开"
                              unCheckedChildren="私有"
                            />
                          </Form.Item>
                        </Col>
                      </Row>
                    </div>

                    {templateParams.length > 0 && (
                      <div className="form-section">
                        <div className="form-section-title">
                          <Divider orientation="left" plain>
                            模板参数配置
                          </Divider>
                        </div>
                        {templateParams.map((param) => (
                          <ParamFormItem
                            key={param.key}
                            param={param}
                            formInstance={form}
                            onOpenCodeEditor={onOpenCodeEditor}
                          />
                        ))}
                      </div>
                    )}

                    {templateParams.length === 0 && !paramsLoading && (
                      <div className="form-section">
                        <Empty
                          description="该模板无需额外配置"
                          image={Empty.PRESENTED_IMAGE_SIMPLE}
                          className="params-empty"
                        />
                      </div>
                    )}
                  </Form>
                </div>
              </Spin>
            ) : (
              <div className="form-placeholder">
                <div className="placeholder-content">
                  <div className="placeholder-icon">🎯</div>
                  <Title level={4} className="placeholder-title">
                    选择一个模板开始
                  </Title>
                  <Text type="secondary" className="placeholder-desc">
                    从左侧列表中选择一个插件模板，然后在此处配置参数
                  </Text>
                </div>
              </div>
            )}
          </section>
        </div>
      </Spin>
    </>
  );
}
