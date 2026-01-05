/**
 * 编辑插件抽屉组件
 * 只需传入 pluginId（实例插件）或 templatePluginKey（工具插件）
 */
import { useState, useEffect, useCallback } from "react";
import {
  Drawer,
  Form,
  Input,
  Select,
  Switch,
  Button,
  Space,
  Typography,
  Tag,
  Card,
  Empty,
  Spin,
  Divider,
  Row,
  Col,
  Alert,
  message,
} from "antd";
import type { FormInstance } from "antd";
import type {
  NativePluginInfo,
  NativePluginTemplateInfo,
  NativePluginConfigFieldTemplate,
  PluginClassifyItem,
} from "../../../../../apiClient/models";
import { PluginTypeObject, PluginConfigFieldTypeObject } from "../../../../../apiClient/models";
import { GetApiClient } from "../../../../ServiceClient";
import { proxyRequestError, proxyFormRequestError } from "../../../../../helper/RequestError";
import ParamFormItem from "./ParamFormItem";

/** 编辑目标：pluginId 表示实例插件，templatePluginKey 表示工具插件 */
export interface EditTarget {
  pluginId?: number;
  templatePluginKey?: string;
}

interface EditPluginDrawerProps {
  open: boolean;
  /** 编辑目标，打开时传入 */
  target: EditTarget | null;
  onClose: () => void;
  onSuccess?: () => void;
  onOpenCodeEditor?: (fieldKey: string, currentValue: string, formInstance: FormInstance) => void;
}

export default function EditPluginDrawer({
  open,
  target,
  onClose,
  onSuccess,
  onOpenCodeEditor,
}: EditPluginDrawerProps) {
  const [messageApi, contextHolder] = message.useMessage();
  const [form] = Form.useForm();

  // 加载状态
  const [loading, setLoading] = useState(false);
  const [paramsLoading, setParamsLoading] = useState(false);
  const [submitLoading, setSubmitLoading] = useState(false);

  // 数据状态
  const [plugin, setPlugin] = useState<NativePluginInfo | null>(null);
  const [templateInfo, setTemplateInfo] = useState<NativePluginTemplateInfo | null>(null);
  const [templateParams, setTemplateParams] = useState<NativePluginConfigFieldTemplate[]>([]);
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);

  const isToolPlugin = target?.templatePluginKey && !target?.pluginId;

  // 获取分类列表
  const fetchClassifyList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items);
      }
    } catch (error) {
      console.error("获取分类列表失败:", error);
    }
  }, []);

  // 获取插件详情
  const fetchPluginDetail = useCallback(async () => {
    if (!target) return;

    setLoading(true);
    setParamsLoading(true);
    form.resetFields();
    setPlugin(null);
    setTemplateInfo(null);
    setTemplateParams([]);

    try {
      const client = GetApiClient();

      if (isToolPlugin) {
        // 工具插件：只需获取模板信息和当前分类
        const templateListResponse = await client.api.admin.native_plugin.template_list.post({});
        const template = templateListResponse?.plugins?.find((t) => t.key === target.templatePluginKey) || null;
        
        if (template) {
          setTemplateInfo(template);
          setPlugin({
            pluginName: template.name || "",
            templatePluginKey: target.templatePluginKey,
            pluginType: PluginTypeObject.ToolPlugin,
            classifyId: undefined, // 工具插件的分类从列表获取
          });
          // 需要从插件列表获取当前分类
          const pluginListResponse = await client.api.admin.native_plugin.list.post({});
          const existingPlugin = pluginListResponse?.items?.find((p) => p.templatePluginKey === target.templatePluginKey);
          if (existingPlugin) {
            form.setFieldsValue({ classifyId: existingPlugin.classifyId });
          }
        }
      } else if (target.pluginId) {
        // 实例插件：获取详情
        const detailResponse = await client.api.admin.native_plugin.detail.post({ pluginId: target.pluginId });
        
        if (detailResponse) {
          setPlugin({
            pluginId: detailResponse.pluginId,
            pluginName: detailResponse.pluginName || "",
            templatePluginKey: detailResponse.templatePluginKey || undefined,
            title: detailResponse.title || "",
            description: detailResponse.description || undefined,
            pluginType: PluginTypeObject.NativePlugin,
            classifyId: detailResponse.classifyId,
            isPublic: detailResponse.isPublic,
          });

          form.setFieldsValue({
            name: detailResponse.pluginName,
            title: detailResponse.title,
            description: detailResponse.description,
            classifyId: detailResponse.classifyId,
            isPublic: detailResponse.isPublic ?? true,
          });

          // 获取模板信息
          if (detailResponse.templatePluginKey) {
            const templateListResponse = await client.api.admin.native_plugin.template_list.post({});
            const template = templateListResponse?.plugins?.find((t) => t.key === detailResponse.templatePluginKey) || null;
            if (template) {
              setTemplateInfo(template);
            }

            // 获取模板参数
            const paramsResponse = await client.api.admin.native_plugin.template_params.post({
              templatePluginKey: detailResponse.templatePluginKey,
            });
            
            if (paramsResponse?.fieldTemplates) {
              setTemplateParams(paramsResponse.fieldTemplates);
              
              // 解析配置并填充表单
              if (detailResponse.config) {
                try {
                  const paramsObj = JSON.parse(detailResponse.config);
                  const initialValues: Record<string, any> = {};
                  
                  paramsResponse.fieldTemplates.forEach((item) => {
                    if (item.key && paramsObj[item.key] !== undefined) {
                      const ft = item.fieldType;
                      if (ft === PluginConfigFieldTypeObject.Number || ft === PluginConfigFieldTypeObject.Integer) {
                        initialValues[item.key] = Number(paramsObj[item.key]);
                      } else if (ft === PluginConfigFieldTypeObject.Boolean) {
                        initialValues[item.key] = String(paramsObj[item.key]) === "true";
                      } else {
                        initialValues[item.key] = paramsObj[item.key];
                      }
                    }
                  });
                  
                  form.setFieldsValue(initialValues);
                } catch (e) {
                  console.error("解析参数失败:", e);
                }
              }
            }
          }
        }
      }
    } catch (error) {
      console.error("获取插件详情失败:", error);
      proxyRequestError(error, messageApi, "获取插件详情失败");
    } finally {
      setLoading(false);
      setParamsLoading(false);
    }
  }, [target, isToolPlugin, form, messageApi]);

  // 打开时加载数据
  useEffect(() => {
    if (open && target) {
      fetchClassifyList();
      fetchPluginDetail();
    }
  }, [open, target]);

  // 提交更新
  const handleSubmit = useCallback(async () => {
    if (!target) return;

    try {
      const values = await form.validateFields(isToolPlugin ? ["classifyId"] : undefined);
      setSubmitLoading(true);

      const client = GetApiClient();
      
      if (isToolPlugin) {
        await client.api.admin.native_plugin.update_tool.post({
          templatePluginKey: target.templatePluginKey,
          classifyId: values.classifyId,
        });
        messageApi.success("工具插件更新成功");
      } else {
        const paramsObj: Record<string, any> = {};
        templateParams.forEach((param) => {
          if (param.key && values[param.key] != null) {
            paramsObj[param.key] = values[param.key];
          }
        });
        
        await client.api.admin.native_plugin.update.post({
          pluginId: target.pluginId,
          name: values.name,
          title: values.title,
          description: values.description,
          classifyId: values.classifyId,
          isPublic: values.isPublic ?? true,
          config: Object.keys(paramsObj).length > 0 ? JSON.stringify(paramsObj) : undefined,
        });
        messageApi.success("内置插件更新成功");
      }

      onClose();
      onSuccess?.();
    } catch (error) {
      console.error("更新插件失败:", error);
      proxyFormRequestError(error, messageApi, form, "更新插件失败");
    } finally {
      setSubmitLoading(false);
    }
  }, [target, isToolPlugin, form, templateParams, messageApi, onClose, onSuccess]);

  // 关闭时重置状态
  const handleClose = useCallback(() => {
    form.resetFields();
    setPlugin(null);
    setTemplateInfo(null);
    setTemplateParams([]);
    onClose();
  }, [form, onClose]);

  return (
    <>
      {contextHolder}
      <Drawer
        title="编辑插件"
        placement="right"
        onClose={handleClose}
        open={open}
        width={800}
        destroyOnClose
        maskClosable={false}
        className="plugin-drawer"
      >
        <Spin spinning={loading}>
          <Card
            className="drawer-form-card"
            title={
              <Space>
                <Typography.Text strong>编辑插件</Typography.Text>
                {plugin && <Tag color="purple">{plugin.pluginName}</Tag>}
              </Space>
            }
            extra={
              <Button type="primary" onClick={handleSubmit} loading={submitLoading}>
                更新
              </Button>
            }
          >
            <Spin spinning={paramsLoading}>
              <Form form={form} layout="vertical" initialValues={{ isPublic: true }}>
                {isToolPlugin ? (
                  <>
                    <Alert
                      message="工具类插件"
                      description="工具类插件只需要绑定分类，不需要其他配置。"
                      type="info"
                      showIcon
                      style={{ marginBottom: 16 }}
                    />
                    <Form.Item
                      name="classifyId"
                      label={
                        <Space>
                          <Typography.Text>绑定分类</Typography.Text>
                          <Typography.Text type="danger">*</Typography.Text>
                        </Space>
                      }
                      rules={[{ required: true, message: "请选择分类" }]}
                    >
                      <Select placeholder="请选择分类" allowClear style={{ width: "100%" }}>
                        {classifyList.map((item) => (
                          <Select.Option key={item.classifyId} value={item.classifyId}>
                            {item.name}
                          </Select.Option>
                        ))}
                      </Select>
                    </Form.Item>
                  </>
                ) : (
                  <>
                    <Form.Item
                      name="name"
                      label={
                        <Space>
                          <Typography.Text>插件名称</Typography.Text>
                          <Typography.Text type="danger">*</Typography.Text>
                        </Space>
                      }
                      help="只能包含字母，用于AI识别使用"
                      rules={[
                        { required: true, message: "请输入插件名称" },
                        { pattern: /^[a-zA-Z_]+$/, message: "插件名称只能包含字母和下划线" },
                        { max: 30, message: "插件名称不能超过30个字符" },
                      ]}
                    >
                      <Input placeholder="请输入插件名称（仅限字母和下划线）" />
                    </Form.Item>

                    <Form.Item
                      name="title"
                      label={
                        <Space>
                          <Typography.Text>插件标题</Typography.Text>
                          <Typography.Text type="danger">*</Typography.Text>
                        </Space>
                      }
                      help="插件标题，可中文，用于系统显示"
                      rules={[{ required: true, message: "请输入插件标题" }]}
                    >
                      <Input placeholder="请输入插件标题" />
                    </Form.Item>

                    <Form.Item name="description" label="描述">
                      <Input.TextArea rows={3} placeholder="请输入插件描述" />
                    </Form.Item>

                    <Row gutter={16}>
                      <Col span={12}>
                        <Form.Item
                          name="classifyId"
                          label={
                            <Space>
                              <Typography.Text>分类</Typography.Text>
                              <Typography.Text type="danger">*</Typography.Text>
                            </Space>
                          }
                          rules={[{ required: true, message: "请选择分类" }]}
                        >
                          <Select placeholder="请选择分类" allowClear style={{ width: "100%" }}>
                            {classifyList.map((item) => (
                              <Select.Option key={item.classifyId} value={item.classifyId}>
                                {item.name}
                              </Select.Option>
                            ))}
                          </Select>
                        </Form.Item>
                      </Col>
                      <Col span={12}>
                        <Form.Item name="isPublic" label="是否公开" valuePropName="checked">
                          <Switch checkedChildren="公开" unCheckedChildren="私有" />
                        </Form.Item>
                      </Col>
                    </Row>

                    <Divider>模板参数</Divider>

                    {templateParams.map((param) => (
                      <ParamFormItem
                        key={param.key}
                        param={param}
                        formInstance={form}
                        onOpenCodeEditor={onOpenCodeEditor}
                      />
                    ))}
                    {templateParams.length === 0 && !paramsLoading && (
                      <Empty description="该模板暂无配置参数" image={Empty.PRESENTED_IMAGE_SIMPLE} />
                    )}
                  </>
                )}
              </Form>
            </Spin>
          </Card>
        </Spin>
      </Drawer>
    </>
  );
}
