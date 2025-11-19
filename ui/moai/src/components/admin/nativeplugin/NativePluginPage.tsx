import { useState, useEffect, useCallback, useMemo } from "react";
import {
  Button,
  Space,
  Typography,
  message,
  Card,
  Table,
  Tag,
  Empty,
  Spin,
  Tooltip,
  Input,
  Row,
  Col,
  Select,
  Drawer,
  List,
  Form,
  Switch,
  InputNumber,
  Divider,
  Alert,
  Popconfirm,
  Modal,
} from "antd";
import {
  ReloadOutlined,
  EditOutlined,
  ApiOutlined,
  PlusOutlined,
  PlayCircleOutlined,
  DeleteOutlined,
} from "@ant-design/icons";
import { GetApiClient } from "../../ServiceClient";
import {
  QueryNativePluginListCommand,
  QueryNativePluginListCommandResponse,
  NativePluginInfo,
  PluginClassifyItem,
  QueryNativePluginTemplateListCommand,
  QueryInternalTemplatePluginListCommandResponse,
  QueryNativePluginTemplateParamsCommand,
  QueryNativePluginTemplateParamsCommandResponse,
  CreateNativePluginCommand,
  UpdateNativePluginCommand,
  QueryNativePluginDetailCommand,
  RunTestNativePluginCommand,
  RunTestNativePluginCommandResponse,
  DeleteNativePluginCommand,
  NativePluginClassify,
  NativePluginClassifyObject,
  NativePluginTemplateInfo,
  NativePluginConfigFieldTemplate,
  PluginConfigFieldTypeObject,
} from "../../../apiClient/models";
import {
  proxyRequestError,
  proxyFormRequestError,
} from "../../../helper/RequestError";
import { formatDateTime } from "../../../helper/DateTimeHelper";
import { TemplateItem, ClassifyList } from "./TemplatePlugin";

const { Title } = Typography;

export default function NativePluginPage() {
  // 状态管理
  const [pluginList, setPluginList] = useState<NativePluginInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchName, setSearchName] = useState<string>("");
  const [filterClassifyId, setFilterClassifyId] = useState<number | undefined>(undefined);
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);
  // 模板列表相关状态
  const [selectedTemplateClassify, setSelectedTemplateClassify] = useState<NativePluginClassify | null>(null);
  // 左侧分类类型切换（"template" 使用 ClassifyList，"api" 使用 classifyList API）
  const [leftClassifyType, setLeftClassifyType] = useState<"template" | "api">("template");
  // 左侧分类选择（可以是 string 用于模板分类，或 number 用于 API 分类）
  const [selectedLeftClassify, setSelectedLeftClassify] = useState<string | number | "all">("all");
  // 只看模板开关（仅在模板分类模式下有效）
  const [showTemplatesOnly, setShowTemplatesOnly] = useState<boolean>(false);
  // 用于主表格显示的模板列表
  const [templateListForDisplay, setTemplateListForDisplay] = useState<NativePluginTemplateInfo[]>([]);

  const [messageApi, contextHolder] = message.useMessage();

  // 模板面板相关状态
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [templateList, setTemplateList] = useState<NativePluginTemplateInfo[]>([]);
  const [templateLoading, setTemplateLoading] = useState(false);
  const [selectedClassify, setSelectedClassify] = useState<string>("all");
  const [selectedTemplate, setSelectedTemplate] = useState<NativePluginTemplateInfo | null>(null);
  const [templateParams, setTemplateParams] = useState<NativePluginConfigFieldTemplate[]>([]);
  const [paramsLoading, setParamsLoading] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [form] = Form.useForm();
  const [templateClassify, settemplateClassify] = useState<TemplateItem[]>(ClassifyList);

  // 编辑相关状态
  const [editingPlugin, setEditingPlugin] = useState<NativePluginInfo | null>(null);
  const [editDrawerVisible, setEditDrawerVisible] = useState(false);
  const [editForm] = Form.useForm();
  const [editLoading, setEditLoading] = useState(false);
  const [editParamsLoading, setEditParamsLoading] = useState(false);
  const [editTemplateParams, setEditTemplateParams] = useState<NativePluginConfigFieldTemplate[]>([]);
  
  // 运行测试模态窗口相关状态
  const [runModalVisible, setRunModalVisible] = useState(false);
  const [runningPlugin, setRunningPlugin] = useState<NativePluginInfo | null>(null);
  const [runParamsValue, setRunParamsValue] = useState<string>("");
  const [runParamsLoading, setRunParamsLoading] = useState(false);
  const [runLoading, setRunLoading] = useState(false);
  const [runResult, setRunResult] = useState<{ success: boolean; message: string | null | undefined } | null>(null);
  const [autoWrap, setAutoWrap] = useState<boolean>(false);

  // 将 ClassifyList 的 key 转换为枚举值的辅助函数
  const keyToEnum = useCallback((key: string): NativePluginClassify | null => {
    // 查找对应的枚举值
    const enumEntry = Object.entries(NativePluginClassifyObject).find(
      ([_, value]) => value.toLowerCase() === key.toLowerCase()
    );
    return enumEntry ? (enumEntry[1] as NativePluginClassify) : null;
  }, []);

  // 获取分类列表
  const fetchClassifyList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.admin_plugin.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items || []);
      }
    } catch (error) {
      console.log("Fetch classify list error:", error);
      proxyRequestError(error, messageApi, "获取分类列表失败");
    }
  }, [messageApi]);

  // 获取所有插件数据（用于计算分类数量）
  const [allPluginList, setAllPluginList] = useState<NativePluginInfo[]>([]);
  const fetchAllPluginList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const requestData: QueryNativePluginListCommand = {};
      const response = await client.api.admin_plugin.native_plugin_list.post(requestData);
      if (response?.items) {
        setAllPluginList(response.items);
      }
    } catch (error) {
      console.log("Fetch all plugin list error:", error);
    }
  }, []);

  // 获取模板列表（用于主表格显示）
  const fetchTemplateListForDisplay = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      // 根据左侧选中的分类来设置筛选条件
      let classify: NativePluginClassify | undefined = undefined;
      
      if (selectedLeftClassify !== "all" && typeof selectedLeftClassify === "string") {
        const enumValue = keyToEnum(selectedLeftClassify);
        if (enumValue) {
          classify = enumValue;
        }
      }
      
      const requestData: QueryNativePluginTemplateListCommand = {
        classify: classify || undefined,
      };
      const response = await client.api.admin_plugin.native_template_list.post(requestData);

      if (response?.plugins) {
        setTemplateListForDisplay(response.plugins);
      } else {
        setTemplateListForDisplay([]);
      }
    } catch (error) {
      console.log("Fetch template list for display error:", error);
      proxyRequestError(error, messageApi, "获取模板列表失败");
      setTemplateListForDisplay([]);
    } finally {
      setLoading(false);
    }
  }, [messageApi, selectedLeftClassify, keyToEnum]);

  // 获取内置插件列表
  const fetchPluginList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      // 根据左侧选中的分类类型和值来设置筛选条件
      let classifyId: number | undefined = undefined;
      let templatePluginClassify: NativePluginClassify | undefined = undefined;
      
      if (selectedLeftClassify !== "all") {
        if (leftClassifyType === "api") {
          // 使用 API 分类（classifyId）
          classifyId = typeof selectedLeftClassify === "number" ? selectedLeftClassify : undefined;
        } else {
          // 使用模板分类（templatePluginClassify）
          const enumValue = typeof selectedLeftClassify === "string" ? keyToEnum(selectedLeftClassify) : null;
          if (enumValue) {
            templatePluginClassify = enumValue;
          }
        }
      }
      
      const requestData: QueryNativePluginListCommand = {
        name: searchName || undefined,
        classifyId: classifyId,
        templatePluginClassify: templatePluginClassify,
      };
      const response = await client.api.admin_plugin.native_plugin_list.post(requestData);

      if (response?.items) {
        setPluginList(response.items);
        // 如果没有筛选条件（全部），同时更新 allPluginList
        if (selectedLeftClassify === "all" && !searchName) {
          setAllPluginList(response.items);
        }
      }
    } catch (error) {
      console.log("Fetch internal plugin list error:", error);
      proxyRequestError(error, messageApi, "获取内置插件列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi, searchName, selectedLeftClassify, leftClassifyType, keyToEnum]);

  // 页面加载时获取数据
  useEffect(() => {
    fetchClassifyList();
    // 页面加载时，fetchPluginList 会获取全部数据并同时更新 allPluginList，避免重复请求
    // 所以这里不需要调用 fetchAllPluginList
  }, [fetchClassifyList]);

  // 当筛选条件变化时，重新获取插件列表或模板列表
  useEffect(() => {
    if (leftClassifyType === "template" && showTemplatesOnly) {
      // 模板分类模式下，如果开启了"只看模板"，则获取模板列表
      fetchTemplateListForDisplay();
    } else {
      // 否则获取插件列表
      fetchPluginList();
    }
  }, [fetchPluginList, fetchTemplateListForDisplay, leftClassifyType, showTemplatesOnly, selectedLeftClassify]);

  // 刷新列表
  const handleRefresh = useCallback(async () => {
    // 先刷新全部插件列表（用于左侧分类数量统计）
    await fetchAllPluginList();
    // 再刷新当前筛选的列表（插件或模板）
    if (leftClassifyType === "template" && showTemplatesOnly) {
      fetchTemplateListForDisplay();
    } else {
      fetchPluginList();
    }
  }, [fetchPluginList, fetchTemplateListForDisplay, fetchAllPluginList, leftClassifyType, showTemplatesOnly]);

  // 编辑插件
  const handleEdit = useCallback(
    async (record: NativePluginInfo) => {
      setEditingPlugin(record);
      setEditDrawerVisible(true);
      setEditLoading(true);
      setEditParamsLoading(true);

      try {
        const client = GetApiClient();
        // 获取插件详情
        const detailRequest: QueryNativePluginDetailCommand = {
          pluginId: record.pluginId,
        };
        const detailResponse = await client.api.admin_plugin.native_plugin_detail.post(detailRequest);

        if (detailResponse) {
          // 设置表单值
          editForm.setFieldsValue({
            name: detailResponse.pluginName,
            title: detailResponse.title,
            description: detailResponse.description,
            classifyId: detailResponse.classifyId,
            isPublic: detailResponse.isPublic ?? true,
          });

          // 获取模板参数
          if (detailResponse.templatePluginKey) {
            const paramsRequest: QueryNativePluginTemplateParamsCommand = {
              templatePluginKey: detailResponse.templatePluginKey,
            };
            const paramsResponse = await client.api.admin_plugin.native_template_params.post(paramsRequest);
            
            if (paramsResponse?.items) {
              setEditTemplateParams(paramsResponse.items);
              
              // 解析params JSON并设置表单值
              if (detailResponse.params) {
                try {
                  const paramsObj = JSON.parse(detailResponse.params);
                  const initialValues: Record<string, any> = {};
                  paramsResponse.items.forEach((item) => {
                    if (item.key && paramsObj[item.key] !== undefined && paramsObj[item.key] !== null) {
                      const fieldType = item.fieldType;
                      if (fieldType === PluginConfigFieldTypeObject.Number || 
                          fieldType === PluginConfigFieldTypeObject.Integer) {
                        initialValues[item.key] = Number(paramsObj[item.key]);
                      } else if (fieldType === PluginConfigFieldTypeObject.Boolean) {
                        const valueStr = String(paramsObj[item.key]);
                        initialValues[item.key] = valueStr === "true" || valueStr === "1";
                      } else {
                        initialValues[item.key] = paramsObj[item.key];
                      }
                    }
                  });
                  editForm.setFieldsValue(initialValues);
                } catch (error) {
                  console.log("Parse params error:", error);
                }
              }
            }
          }
        }
      } catch (error) {
        console.log("Fetch plugin detail error:", error);
        proxyRequestError(error, messageApi, "获取插件详情失败");
      } finally {
        setEditLoading(false);
        setEditParamsLoading(false);
      }
    },
    [messageApi, editForm]
  );

  // 关闭编辑抽屉
  const handleCloseEditDrawer = useCallback(() => {
    setEditDrawerVisible(false);
    setEditingPlugin(null);
    setEditTemplateParams([]);
    editForm.resetFields();
  }, [editForm]);

  // 打开运行测试模态窗口
  const handleOpenRunModal = useCallback(async (record: NativePluginInfo) => {
    setRunningPlugin(record);
    setRunModalVisible(true);
    setRunParamsLoading(true);
    setRunParamsValue("");
    setRunResult(null);

    try {
      const client = GetApiClient();
      // 获取插件详情以获取模板key
      const detailRequest: QueryNativePluginDetailCommand = {
        pluginId: record.pluginId,
      };
      const detailResponse = await client.api.admin_plugin.native_plugin_detail.post(detailRequest);

      if (detailResponse?.templatePluginKey) {
        // 获取模板参数示例值
        const paramsRequest: QueryNativePluginTemplateParamsCommand = {
          templatePluginKey: detailResponse.templatePluginKey,
        };
        const paramsResponse = await client.api.admin_plugin.native_template_params.post(paramsRequest);
        
        // 获取运行参数示例值
        if (paramsResponse?.exampleValue) {
          setRunParamsValue(JSON.parse(paramsResponse.exampleValue));
        } else {
          setRunParamsValue("");
        }
      }
    } catch (error) {
      console.log("Fetch run params error:", error);
      proxyRequestError(error, messageApi, "获取运行参数失败");
    } finally {
      setRunParamsLoading(false);
    }
  }, [messageApi]);

  // 关闭运行测试模态窗口
  const handleCloseRunModal = useCallback(() => {
    setRunModalVisible(false);
    setRunningPlugin(null);
    setRunParamsValue("");
    setRunResult(null);
    setAutoWrap(false); // 重置自动换行状态
  }, []);

  // 运行插件
  const handleRunPlugin = useCallback(async () => {
    if (!runningPlugin) {
      messageApi.error("请先选择插件");
      return;
    }

    if (!runParamsValue.trim()) {
      messageApi.error("请输入运行参数");
      return;
    }

    try {
      setRunLoading(true);
      setRunResult(null);

      // 序列化为 JSON 字符串
      const paramsString = JSON.stringify(runParamsValue);

      const client = GetApiClient();
      const requestData: RunTestNativePluginCommand = {
        templatePluginKey: runningPlugin.templatePluginKey || undefined,
        pluginId: runningPlugin.pluginId || undefined,
        params: paramsString,
      };

      const response = await client.api.admin_plugin.run_native_plugin.post(requestData);

      if (response) {
        let message = response.response!;
        // 如果开启了自动换行，尝试解析 JSON 并格式化
        if (autoWrap && message) {
          try {
            const parsed = JSON.parse(message);
            // 如果 parsed 是字符串 则直接赋值；如果 parsed 是对象则直接使用 response.response!
            if (typeof parsed === "string") {
              message = parsed;
            } else {
              // 避免 parsed 序列化后是 object 导致 setRunResult() 异常
              message = JSON.stringify(parsed, null, 2);
            }
          } catch (error) {
            // 如果解析失败，使用原始消息
            console.log("Failed to parse response as JSON:", error);
          }
        }
        setRunResult({
          success: response.isSuccess!,
          message: message,
        });  
        messageApi.success((response.isSuccess == true )? "插件运行成功" : "插件运行失败");
      }
    } catch (error) {
      console.log("Run plugin error:", error);
      const errorMessage = (error as any)?.message || "运行插件时发生错误";
      setRunResult({
        success: false,
        message: errorMessage,
      });
      proxyRequestError(error, messageApi, "运行插件失败");
    } finally {
      setRunLoading(false);
    }
  }, [runningPlugin, runParamsValue, messageApi, autoWrap]);

  // 提交编辑
  const handleEditSubmit = useCallback(async () => {
    if (!editingPlugin) {
      messageApi.error("请先选择插件");
      return;
    }

    try {
      const values = await editForm.validateFields();
      setEditLoading(true);

      // 构建参数对象
      const paramsObj: Record<string, any> = {};
      editTemplateParams.forEach((param) => {
        if (param.key && values[param.key] !== undefined && values[param.key] !== null) {
          paramsObj[param.key] = values[param.key];
        }
      });

      const client = GetApiClient();
      const requestData: UpdateNativePluginCommand = {
        pluginId: editingPlugin.pluginId || undefined,
        name: values.name,
        title: values.title,
        description: values.description,
        classifyId: values.classifyId,
        isPublic: values.isPublic ?? true,
        config: Object.keys(paramsObj).length > 0 ? JSON.stringify(paramsObj) : undefined,
      };

      await client.api.admin_plugin.update_native_plugin.post(requestData);

      messageApi.success("内置插件更新成功");
      handleCloseEditDrawer();
      // 先刷新全部插件列表（用于左侧分类数量统计）
      await fetchAllPluginList();
      // 再刷新当前筛选的插件列表
      fetchPluginList();
    } catch (error) {
      console.log("Update internal plugin error:", error);
      proxyFormRequestError(error, messageApi, editForm);
    } finally {
      setEditLoading(false);
    }
  }, [editingPlugin, editForm, editTemplateParams, messageApi, fetchPluginList, fetchAllPluginList, handleCloseEditDrawer]);

  // 删除插件
  const handleDelete = useCallback(
    async (pluginId: number) => {
      try {
        const client = GetApiClient();
        const requestData: DeleteNativePluginCommand = {
          pluginId: pluginId,
        };
        await client.api.admin_plugin.delete_native_plugin.delete(requestData);

        messageApi.success("内置插件删除成功");
        // 先刷新全部插件列表（用于左侧分类数量统计）
        await fetchAllPluginList();
        // 再刷新当前筛选的插件列表
        fetchPluginList();
      } catch (error) {
        console.log("Delete internal plugin error:", error);
        proxyRequestError(error, messageApi, "删除内置插件失败");
      }
    },
    [messageApi, fetchPluginList, fetchAllPluginList]
  );

  // 处理模板运行
  const handleRunTemplate = useCallback(async (template: NativePluginTemplateInfo) => {
    if (!template.key) {
      messageApi.error("模板Key不存在");
      return;
    }

    // 创建一个临时的 NativePluginInfo 对象用于运行
    const tempPlugin: NativePluginInfo = {
      pluginId: undefined,
      pluginName: template.name || "",
      templatePluginKey: template.key || undefined,
      title: template.name || "",
      description: template.description || undefined,
      isPublic: true,
    };
    
    setRunningPlugin(tempPlugin);
    setRunModalVisible(true);
    setRunParamsLoading(true);
    setRunParamsValue("");
    setRunResult(null);

    try {
      const client = GetApiClient();
      // 直接使用模板的 key 获取模板参数示例值
      const paramsRequest: QueryNativePluginTemplateParamsCommand = {
        templatePluginKey: template.key,
      };
      const paramsResponse = await client.api.admin_plugin.native_template_params.post(paramsRequest);
      
      // 获取运行参数示例值
      if (paramsResponse?.exampleValue) {
        setRunParamsValue(JSON.parse(paramsResponse.exampleValue));
      } else {
        setRunParamsValue("");
      }
    } catch (error) {
      console.log("Fetch template run params error:", error);
      proxyRequestError(error, messageApi, "获取运行参数失败");
    } finally {
      setRunParamsLoading(false);
    }
  }, [messageApi]);

  // 过滤后的模板列表（支持搜索）
  const filteredTemplateListForDisplay = useMemo(() => {
    if (!searchName.trim()) {
      return templateListForDisplay;
    }
    const searchLower = searchName.toLowerCase();
    return templateListForDisplay.filter(
      (template) =>
        (template.name && template.name.toLowerCase().includes(searchLower)) ||
        (template.key && template.key.toLowerCase().includes(searchLower)) ||
        (template.description && template.description.toLowerCase().includes(searchLower))
    );
  }, [templateListForDisplay, searchName]);

  // 表格列定义
  const columns = useMemo(
    () => [
      {
        title: "插件名称",
        dataIndex: "pluginName",
        key: "pluginName",
        render: (pluginName: string, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 如果是模板（没有 pluginId），使用 name 字段
          if (!('pluginId' in record)) {
            return <Typography.Text strong>{(record as NativePluginTemplateInfo).name || "-"}</Typography.Text>;
          }
          return <Typography.Text strong>{pluginName}</Typography.Text>;
        },
      },
      {
        title: "标题",
        dataIndex: "title",
        key: "title",
        render: (title: string, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 如果是模板（没有 pluginId），使用 name 字段
          if (!('pluginId' in record)) {
            return (record as NativePluginTemplateInfo).name || "-";
          }
          return title || "-";
        },
      },
      {
        title: "模板Key",
        dataIndex: "templatePluginKey",
        key: "templatePluginKey",
        render: (templatePluginKey: string, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 如果是模板（没有 pluginId），使用 key 字段
          if (!('pluginId' in record)) {
            return (
              <Typography.Text type="secondary" style={{ fontSize: "12px", fontFamily: "monospace" }}>
                {(record as NativePluginTemplateInfo).key || "-"}
              </Typography.Text>
            );
          }
          return (
            <Typography.Text type="secondary" style={{ fontSize: "12px", fontFamily: "monospace" }}>
              {templatePluginKey || "-"}
            </Typography.Text>
          );
        },
      },
      {
        title: "描述",
        dataIndex: "description",
        key: "description",
        render: (description: string) => (
          <Typography.Text type="secondary" style={{ fontSize: "12px" }}>
            {description || "-"}
          </Typography.Text>
        ),
      },
      {
        title: "是否公开",
        dataIndex: "isPublic",
        key: "isPublic",
        width: 100,
        render: (isPublic: boolean, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 如果是模板（没有 pluginId），不显示
          if (!('pluginId' in record)) {
            return "-";
          }
          return (
            <Tag color={isPublic ? "green" : "orange"}>
              {isPublic ? "公开" : "私有"}
            </Tag>
          );
        },
      },
      {
        title: "创建时间",
        dataIndex: "createTime",
        key: "createTime",
        render: (createTime: string, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 如果是模板（没有 pluginId），不显示
          if (!('pluginId' in record)) {
            return "-";
          }
          if (!createTime) return "-";
          try {
            return formatDateTime(createTime);
          } catch {
            return createTime;
          }
        },
      },
      {
        title: "创建人",
        dataIndex: "createUserName",
        key: "createUserName",
        render: (createUserName: string, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 如果是模板（没有 pluginId），不显示
          if (!('pluginId' in record)) {
            return "-";
          }
          return createUserName || "-";
        },
      },
      {
        title: "操作",
        key: "action",
        width: 150,
        fixed: "right" as const,
        render: (_: any, record: NativePluginInfo | NativePluginTemplateInfo) => {
          // 判断是否是模板（没有 pluginId 就是模板）
          const isTemplate = !('pluginId' in record);
          const template = record as NativePluginTemplateInfo;
          const plugin = record as NativePluginInfo;
          
          return (
            <Space size="small">
              {/* 如果是模板且是 IsTool，显示运行按钮 */}
              {isTemplate && template.isTool === true && (
                <Tooltip title="运行测试">
                  <Button
                    type="link"
                    size="small"
                    icon={<PlayCircleOutlined />}
                    onClick={() => handleRunTemplate(template)}
                  >
                    运行
                  </Button>
                </Tooltip>
              )}
              {/* 如果是插件，显示运行、编辑、删除按钮 */}
              {!isTemplate && (
                <>
                  <Tooltip title="运行测试">
                    <Button
                      type="link"
                      size="small"
                      icon={<PlayCircleOutlined />}
                      onClick={() => handleOpenRunModal(plugin)}
                    >
                      运行
                    </Button>
                  </Tooltip>
                  <Tooltip title="编辑插件">
                    <Button
                      type="link"
                      size="small"
                      icon={<EditOutlined />}
                      onClick={() => handleEdit(plugin)}
                    >
                      编辑
                    </Button>
                  </Tooltip>
                  <Popconfirm
                    title="删除插件"
                    description="确定要删除这个插件吗？删除后无法恢复。"
                    okText="确认删除"
                    cancelText="取消"
                    onConfirm={() => handleDelete(plugin.pluginId!)}
                    okButtonProps={{ danger: true }}
                  >
                    <Tooltip title="删除插件">
                      <Button
                        type="link"
                        size="small"
                        danger
                        icon={<DeleteOutlined />}
                      >
                        删除
                      </Button>
                    </Tooltip>
                  </Popconfirm>
                </>
              )}
            </Space>
          );
        },
      },
    ],
    [handleEdit, handleDelete, handleOpenRunModal, handleRunTemplate]
  );

  // 获取模板列表
  const fetchTemplateList = useCallback(async () => {
    setTemplateLoading(true);
    try {
      const client = GetApiClient();
      // 不传 classify 参数，默认为 undefined
      const requestData: QueryNativePluginTemplateListCommand = {
        classify: undefined,
      };
      const response = await client.api.admin_plugin.native_template_list.post(requestData);
      if (response) {
        if (response.plugins) {
          setTemplateList(response.plugins);
        }
        
        // 从 ClassifyList 拷贝数据，生成 TemplateItem[]
        const templateItems: TemplateItem[] = ClassifyList.map((item) => ({
          ...item,
          count: 0, // 初始化为 0
        }));
        
        // 使用 classifyCount 匹配，设置每个分类的数量
        // classifyCount 是 KeyValueOfStringAndInt32[] 数组，每个元素有 key 和 value
        if (response.classifyCount && response.classifyCount.length > 0) {
          templateItems.forEach((templateItem) => { 
            // 在 classifyCount 中查找匹配的 key（忽略大小写字符串匹配）
            const countItem = response.classifyCount!.find(
              (cv) => cv.key && cv.key.toLowerCase() === templateItem.key.toLowerCase()
            );
            if (countItem && typeof countItem.value === 'number') {
              templateItem.count = countItem.value;
            }
          });
        }
        
        settemplateClassify(templateItems);
        // 默认选中"全部"
        setSelectedClassify("all");
      }
    } catch (error) {
      console.log("Fetch template list error:", error);
      proxyRequestError(error, messageApi, "获取模板列表失败");
    } finally {
      setTemplateLoading(false);
    }
  }, [messageApi]);

  // 打开模板面板
  const handleOpenDrawer = useCallback(() => {
    setDrawerVisible(true);
    fetchTemplateList();
  }, [fetchTemplateList]);

  // 关闭模板面板
  const handleCloseDrawer = useCallback(() => {
    setDrawerVisible(false);
    setSelectedClassify("all");
    setSelectedTemplate(null);
    setTemplateList([]);
    setTemplateParams([]);
    form.resetFields();
  }, [form]);

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
      // 选中"全部"时，返回所有模板
      return templateList;
    }
    return groupedTemplates[selectedClassify] || [];
  }, [selectedClassify, groupedTemplates, templateList]);

  // 获取模板参数
  const fetchTemplateParams = useCallback(async (templateKey: string) => {
    setParamsLoading(true);
    try {
      const client = GetApiClient();
      const requestData: QueryNativePluginTemplateParamsCommand = {
        templatePluginKey: templateKey,
      };
      const response = await client.api.admin_plugin.native_template_params.post(requestData);
      if (response?.items) {
        setTemplateParams(response.items);
      } else {
        setTemplateParams([]);
        form.resetFields();
      }
    } catch (error) {
      console.log("Fetch template params error:", error);
      proxyRequestError(error, messageApi, "获取模板参数失败");
      setTemplateParams([]);
      form.resetFields();
    } finally {
      setParamsLoading(false);
    }
  }, [messageApi, form]);

  // 点击模板项
  const handleTemplateClick = useCallback((template: NativePluginTemplateInfo) => {
    setSelectedTemplate(template);
    // 设置表单默认值：name 使用模板的 key，title 使用模板的 name
    form.setFieldsValue({
      name: template.key || "",
      title: template.name || "",
    });
    if (template.key) {
      fetchTemplateParams(template.key);
    }
  }, [fetchTemplateParams, form]);

  // 创建内置插件
  const handleCreatePlugin = useCallback(async () => {
    if (!selectedTemplate) {
      messageApi.error("请先选择模板");
      return;
    }

    try {
      const values = await form.validateFields();
      setCreateLoading(true);

      // 构建参数对象
      const paramsObj: Record<string, any> = {};
      templateParams.forEach((param) => {
        if (param.key && values[param.key] !== undefined && values[param.key] !== null) {
          paramsObj[param.key] = values[param.key];
        }
      });

      const client = GetApiClient();
      const requestData: CreateNativePluginCommand = {
        templatePluginKey: selectedTemplate.key || undefined,
        name: values.name,
        title: values.title,
        description: values.description,
        classifyId: values.classifyId,
        isPublic: values.isPublic ?? true,
        config: Object.keys(paramsObj).length > 0 ? JSON.stringify(paramsObj) : undefined,
      };

      const response = await client.api.admin_plugin.create_native_plugin.post(requestData);

      if (response?.value !== undefined) {
        messageApi.success("内置插件创建成功");
        handleCloseDrawer();
        // 先刷新全部插件列表（用于左侧分类数量统计）
        await fetchAllPluginList();
        // 再刷新当前筛选的插件列表
        fetchPluginList();
      }
    } catch (error) {
      console.log("Create internal plugin error:", error);
      proxyFormRequestError(error, messageApi, form);
    } finally {
      setCreateLoading(false);
    }
  }, [selectedTemplate, form, templateParams, messageApi, fetchPluginList, fetchAllPluginList, handleCloseDrawer]);

  return (
    <>
      {contextHolder}
      <div style={{ padding: 24 }}>
        <Card>
          <div
            style={{
              marginBottom: "16px",
              display: "flex",
              justifyContent: "space-between",
              alignItems: "center",
            }}
          >
            <Title level={3} style={{ margin: 0 }}>
              <ApiOutlined style={{ marginRight: "8px" }} />
              内置插件
            </Title>
            <Button
              type="primary"
              icon={<PlusOutlined />}
              onClick={handleOpenDrawer}
            >
              新增
            </Button>
          </div>

          {/* 主体内容：左右布局 */}
          <div style={{ display: "flex", gap: "16px" }}>
            {/* 左侧分类列表 */}
            <div
              style={{
                width: "200px",
                borderRight: "1px solid #f0f0f0",
                paddingRight: "16px",
              }}
            >
              {/* 分类类型切换按钮 */}
              <div style={{ marginBottom: "12px" }}>
                <Button.Group style={{ width: "100%" }}>
                  <Button
                    type={leftClassifyType === "template" ? "primary" : "default"}
                    size="small"
                    onClick={() => {
                      setLeftClassifyType("template");
                      setSelectedLeftClassify("all");
                      setShowTemplatesOnly(false); // 切换分类类型时重置"只看模板"开关
                    }}
                    style={{ flex: 1 }}
                  >
                    模板分类
                  </Button>
                  <Button
                    type={leftClassifyType === "api" ? "primary" : "default"}
                    size="small"
                    onClick={() => {
                      setLeftClassifyType("api");
                      setSelectedLeftClassify("all");
                      setShowTemplatesOnly(false); // 切换分类类型时重置"只看模板"开关
                    }}
                    style={{ flex: 1 }}
                  >
                    插件分类
                  </Button>
                </Button.Group>
              </div>

              <List
                size="small"
                dataSource={
                  (leftClassifyType === "template"
                    ? [
                        { key: "all" as const, name: "全部", icon: "📋", count: allPluginList.length },
                        ...ClassifyList.map((item) => {
                          const enumValue = keyToEnum(item.key);
                          const count = enumValue
                            ? allPluginList.filter(
                                (plugin) => plugin.templatePluginClassify === enumValue
                              ).length
                            : 0;
                          return {
                            key: item.key,
                            name: item.name,
                            icon: item.icon,
                            count,
                          };
                        }),
                      ]
                    : [
                        { key: "all" as const, name: "全部", icon: undefined, count: allPluginList.length },
                        ...classifyList
                          .filter((item) => item.classifyId != null)
                          .map((item) => ({
                            key: item.classifyId!,
                            name: item.name || "",
                            icon: undefined,
                            count: allPluginList.filter(
                              (plugin) => plugin.classifyId === item.classifyId
                            ).length,
                          })),
                      ]) as Array<{ key: string | number | "all"; name: string; icon?: string; count: number }>
                }
                renderItem={(item) => {
                  const isSelected = selectedLeftClassify === item.key;
                  
                  return (
                    <List.Item
                      style={{
                        cursor: "pointer",
                        backgroundColor: isSelected ? "#e6f7ff" : "transparent",
                        borderRadius: "4px",
                        padding: "8px 12px",
                        marginBottom: "4px",
                        display: "flex",
                        justifyContent: "space-between",
                        alignItems: "center",
                      }}
                      onClick={() => setSelectedLeftClassify(item.key)}
                    >
                      <Space>
                        {"icon" in item && item.icon && (
                          <span style={{ fontSize: "16px" }}>{item.icon}</span>
                        )}
                        <Typography.Text strong={isSelected}>
                          {item.name}
                        </Typography.Text>
                      </Space>
                      <Tag color={isSelected ? "blue" : "default"}>
                        {item.count}
                      </Tag>
                    </List.Item>
                  );
                }}
              />
            </div>

            {/* 右侧内容区域 */}
            <div style={{ flex: 1 }}>
              {/* 搜索筛选 */}
              <div style={{ marginBottom: 16 }}>
                <Space>
                  <Input.Search
                    placeholder={
                      leftClassifyType === "template" && showTemplatesOnly
                        ? "搜索模板名称"
                        : "搜索插件名称"
                    }
                    allowClear
                    value={searchName}
                    onChange={(e) => setSearchName(e.target.value)}
                    onSearch={() => {
                      // 搜索功能在模板模式下通过客户端过滤实现，不需要重新请求
                      // 在插件模式下才需要重新请求
                      if (leftClassifyType !== "template" || !showTemplatesOnly) {
                        fetchPluginList();
                      }
                    }}
                    enterButton
                    style={{ maxWidth: "400px" }}
                  />
                  {/* 模板分类模式下显示"只看模板"开关 */}
                  {leftClassifyType === "template" && (
                    <Space>
                      <Typography.Text>只看模板</Typography.Text>
                      <Switch
                        checked={showTemplatesOnly}
                        onChange={(checked) => {
                          setShowTemplatesOnly(checked);
                          setSearchName(""); // 重置搜索
                        }}
                      />
                    </Space>
                  )}
                  <Button
                    icon={<ReloadOutlined />}
                    onClick={handleRefresh}
                    loading={loading}
                  >
                    刷新
                  </Button>
                </Space>
              </div>

              <Table
                columns={columns}
                dataSource={
                  leftClassifyType === "template" && showTemplatesOnly
                    ? filteredTemplateListForDisplay
                    : pluginList
                }
                rowKey={(record) => {
                  // 如果是模板（没有 pluginId），使用 key；如果是插件，使用 pluginId
                  if (!('pluginId' in record)) {
                    return (record as NativePluginTemplateInfo).key || "";
                  }
                  return (record as NativePluginInfo).pluginId?.toString() || "";
                }}
                loading={loading}
                pagination={false}
                scroll={{ x: 'max-content' }}
                locale={{
                  emptyText: (
                    <Empty
                      description={
                        leftClassifyType === "template" && showTemplatesOnly
                          ? "暂无模板数据"
                          : "暂无内置插件数据"
                      }
                      image={Empty.PRESENTED_IMAGE_SIMPLE}
                    />
                  ),
                }}
              />
            </div>
          </div>
        </Card>

        {/* 编辑插件抽屉 */}
        <Drawer
          title="编辑插件"
          placement="right"
          onClose={handleCloseEditDrawer}
          open={editDrawerVisible}
          width={1400}
          destroyOnClose
          styles={{
            body: {
              padding: 0,
            },
          }}
        >
          <Spin spinning={editLoading}>
            <div style={{ padding: 24 }}>
              <Card
                title={
                  <Space>
                    <Typography.Text strong>编辑插件</Typography.Text>
                    {editingPlugin && (
                      <Tag color="purple">{editingPlugin.pluginName}</Tag>
                    )}
                  </Space>
                }
                extra={
                  <Button
                    type="primary"
                    onClick={handleEditSubmit}
                    loading={editLoading}
                  >
                    更新
                  </Button>
                }
              >
                <Spin spinning={editParamsLoading}>
                  <Form
                    form={editForm}
                    layout="vertical"
                    initialValues={{
                      isPublic: true,
                    }}
                  >
                    {/* 基础信息 */}
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

                    <Form.Item
                      name="description"
                      label="描述"
                    >
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
                          <Select
                            placeholder="请选择分类"
                            allowClear
                            style={{ width: "100%" }}
                          >
                            {classifyList.map((item) => (
                              <Select.Option key={item.classifyId} value={item.classifyId}>
                                {item.name}
                              </Select.Option>
                            ))}
                          </Select>
                        </Form.Item>
                      </Col>
                      <Col span={12}>
                        <Form.Item
                          name="isPublic"
                          label="是否公开"
                          valuePropName="checked"
                        >
                          <Switch checkedChildren="公开" unCheckedChildren="私有" />
                        </Form.Item>
                      </Col>
                    </Row>

                    <Divider>模板参数</Divider>

                    {editTemplateParams.map((param) => {
                      const fieldType = param.fieldType;
                      const isRequired = param.isRequired === true;

                      // 根据字段类型渲染不同的表单项
                      if (!param.key) return null;
                      
                      if (fieldType === PluginConfigFieldTypeObject.Boolean) {
                        return (
                          <Form.Item
                            key={param.key}
                            name={param.key}
                            label={
                              <Space>
                                <Typography.Text>{param.key}</Typography.Text>
                                {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                              </Space>
                            }
                            help={param.description || undefined}
                            valuePropName="checked"
                            rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                          >
                            <Switch />
                          </Form.Item>
                        );
                      } else if (
                        fieldType === PluginConfigFieldTypeObject.Number ||
                        fieldType === PluginConfigFieldTypeObject.Integer
                      ) {
                        return (
                          <Form.Item
                            key={param.key}
                            name={param.key}
                            label={
                              <Space>
                                <Typography.Text>{param.key}</Typography.Text>
                                {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                              </Space>
                            }
                            help={param.description || undefined}
                            rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                          >
                            <InputNumber style={{ width: "100%" }} />
                          </Form.Item>
                        );
                      } else if (fieldType === PluginConfigFieldTypeObject.Object || 
                                 fieldType === PluginConfigFieldTypeObject.Map) {
                        return (
                          <Form.Item
                            key={param.key}
                            name={param.key}
                            label={
                              <Space>
                                <Typography.Text>{param.key}</Typography.Text>
                                {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                              </Space>
                            }
                            help={param.description || undefined}
                            rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                          >
                            <Input.TextArea rows={4} placeholder="请输入 JSON 格式" />
                          </Form.Item>
                        );
                      } else {
                        // 默认字符串类型
                        return (
                          <Form.Item
                            key={param.key}
                            name={param.key}
                            label={
                              <Space>
                                <Typography.Text>{param.key}</Typography.Text>
                                {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                              </Space>
                            }
                            help={param.description || undefined}
                            rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                          >
                            <Input placeholder={param.exampleValue ? `示例: ${param.exampleValue}` : ""} />
                          </Form.Item>
                        );
                      }
                    })}
                    {editTemplateParams.length === 0 && !editParamsLoading && (
                      <Empty
                        description="该模板暂无配置参数"
                        image={Empty.PRESENTED_IMAGE_SIMPLE}
                      />
                    )}
                  </Form>
                </Spin>
              </Card>
            </div>
          </Spin>
        </Drawer>

        {/* 模板选择抽屉 */}
        <Drawer
          title="选择模板"
          placement="right"
          onClose={handleCloseDrawer}
          open={drawerVisible}
          width={1400}
          destroyOnClose
          styles={{
            body: {
              padding: 0,
            },
          }}
        >
          <Spin spinning={templateLoading}>
            <div style={{ display: "flex", height: "100%" }}>
              {/* 左侧分类列表 */}
              <div
                style={{
                  width: "200px",
                  borderRight: "1px solid #f0f0f0",
                  paddingRight: "16px",
                  paddingLeft: "16px",
                  paddingTop: "16px",
                  overflowY: "auto",
                  height: "100%",
                }}
              >
                <List
                  size="small"
                  dataSource={[
                    { key: "all", name: "全部", icon: "📋", count: templateList.length, templates: [] },
                    ...templateClassify
                  ]}
                  renderItem={(item) => {
                    const isSelected = selectedClassify === item.key;
                    
                    return (
                      <List.Item
                        style={{
                          cursor: "pointer",
                          backgroundColor: isSelected ? "#e6f7ff" : "transparent",
                          borderRadius: "4px",
                          padding: "8px 12px",
                          marginBottom: "4px",
                          display: "flex",
                          justifyContent: "space-between",
                          alignItems: "center",
                        }}
                        onClick={() => setSelectedClassify(item.key)}
                      >
                        <Space>
                          <span style={{ fontSize: "16px" }}>{item.icon}</span>
                          <Typography.Text strong={isSelected}>
                            {item.name}
                          </Typography.Text>
                        </Space>
                        <Tag color={isSelected ? "blue" : "default"}>
                          {item.count}
                        </Tag>
                      </List.Item>
                    );
                  }}
                />
              </div>

              {/* 中间模板列表 */}
              <div
                style={{
                  width: "300px",
                  borderRight: "1px solid #f0f0f0",
                  paddingLeft: "16px",
                  paddingRight: "16px",
                  paddingTop: "16px",
                  overflowY: "auto",
                  height: "100%",
                }}
              >
                {selectedClassify ? (
                  <List
                    dataSource={currentTemplates}
                    renderItem={(template: NativePluginTemplateInfo) => (
                      <List.Item
                        style={{
                          cursor: "pointer",
                          backgroundColor:
                            selectedTemplate?.key === template.key
                              ? "#e6f7ff"
                              : "transparent",
                          borderRadius: "4px",
                          padding: "8px 12px",
                          marginBottom: "4px",
                        }}
                        onClick={() => handleTemplateClick(template)}
                      >
                        <List.Item.Meta
                          title={
                            <Space direction="vertical" size={4}>
                              <Typography.Text strong>
                                {template.name}
                              </Typography.Text>
                              <Tag color="purple" style={{ margin: 0 }}>
                                {template.key}
                              </Tag>
                            </Space>
                          }
                          description={
                            <Typography.Text type="secondary" style={{ fontSize: "12px" }}>
                              {template.description || "无描述"}
                            </Typography.Text>
                          }
                        />
                      </List.Item>
                    )}
                    locale={{
                      emptyText: (
                        <Empty
                          description="该分类下暂无模板"
                          image={Empty.PRESENTED_IMAGE_SIMPLE}
                        />
                      ),
                    }}
                  />
                ) : (
                  <Empty
                    description="请选择分类"
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                  />
                )}
              </div>

              {/* 右侧创建表单 */}
              <div
                style={{
                  flex: 1,
                  paddingLeft: "16px",
                  paddingRight: "16px",
                  paddingTop: "16px",
                  overflowY: "auto",
                  height: "100%",
                }}
              >
                {selectedTemplate ? (
                  <Spin spinning={paramsLoading}>
                    <Card
                      title={
                        <Space>
                          <Typography.Text strong>创建插件</Typography.Text>
                          <Tag color="purple">{selectedTemplate.name}</Tag>
                        </Space>
                      }
                      extra={
                        selectedTemplate.isTool !== true && (
                          <Button
                            type="primary"
                            onClick={handleCreatePlugin}
                            loading={createLoading}
                          >
                            创建
                          </Button>
                        )
                      }
                    >
                      {selectedTemplate.isTool === true && (
                        <Alert
                          message="该插件不需要配置"
                          description="该插件是工具类型，不需要配置，不能创建实例。"
                          type="info"
                          showIcon
                          style={{ marginBottom: 16 }}
                        />
                      )}
                      <Form
                        form={form}
                        layout="vertical"
                        disabled={selectedTemplate.isTool === true}
                        initialValues={{
                          isPublic: true,
                        }}
                      >
                        {/* 基础信息 */}
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
                            {max: 30, message: "插件名称不能超过30个字符"},
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

                        <Form.Item
                          name="description"
                          label="描述"
                        >
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
                              <Select
                                placeholder="请选择分类"
                                allowClear
                                style={{ width: "100%" }}
                              >
                                {classifyList.map((item) => (
                                  <Select.Option key={item.classifyId} value={item.classifyId}>
                                    {item.name}
                                  </Select.Option>
                                ))}
                              </Select>
                            </Form.Item>
                          </Col>
                          <Col span={12}>
                            <Form.Item
                              name="isPublic"
                              label="是否公开"
                              valuePropName="checked"
                            >
                              <Switch checkedChildren="公开" unCheckedChildren="私有" />
                            </Form.Item>
                          </Col>
                        </Row>

                        <Divider>模板参数</Divider>

                        {templateParams.map((param) => {
                          const fieldType = param.fieldType;
                          const isRequired = param.isRequired === true;

                          // 根据字段类型渲染不同的表单项
                          if (!param.key) return null;
                          
                          if (fieldType === PluginConfigFieldTypeObject.Boolean) {
                            return (
                              <Form.Item
                                key={param.key}
                                name={param.key}
                                label={
                                  <Space>
                                    <Typography.Text>{param.key}</Typography.Text>
                                    {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                                  </Space>
                                }
                                help={param.description || undefined}
                                valuePropName="checked"
                                rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                              >
                                <Switch />
                              </Form.Item>
                            );
                          } else if (
                            fieldType === PluginConfigFieldTypeObject.Number ||
                            fieldType === PluginConfigFieldTypeObject.Integer
                          ) {
                            return (
                              <Form.Item
                                key={param.key}
                                name={param.key}
                                label={
                                  <Space>
                                    <Typography.Text>{param.key}</Typography.Text>
                                    {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                                  </Space>
                                }
                                help={param.description || undefined}
                                rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                              >
                                <InputNumber style={{ width: "100%" }} />
                              </Form.Item>
                            );
                          } else if (fieldType === PluginConfigFieldTypeObject.Object || 
                                     fieldType === PluginConfigFieldTypeObject.Map) {
                            return (
                              <Form.Item
                                key={param.key}
                                name={param.key}
                                label={
                                  <Space>
                                    <Typography.Text>{param.key}</Typography.Text>
                                    {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                                  </Space>
                                }
                                help={param.description || undefined}
                                rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                              >
                                <Input.TextArea rows={4} placeholder="请输入 JSON 格式" />
                              </Form.Item>
                            );
                          } else {
                            // 默认字符串类型
                            return (
                              <Form.Item
                                key={param.key}
                                name={param.key}
                                label={
                                  <Space>
                                    <Typography.Text>{param.key}</Typography.Text>
                                    {isRequired && <Typography.Text type="danger">*</Typography.Text>}
                                  </Space>
                                }
                                help={param.description || undefined}
                                rules={isRequired ? [{ required: true, message: `请输入${param.key}` }] : []}
                              >
                                <Input placeholder={param.exampleValue ? `示例: ${param.exampleValue}` : ""} />
                              </Form.Item>
                            );
                          }
                        })}
                        {templateParams.length === 0 && !paramsLoading && (
                          <Empty
                            description="该模板暂无配置参数"
                            image={Empty.PRESENTED_IMAGE_SIMPLE}
                          />
                        )}
                      </Form>
                    </Card>
                  </Spin>
                ) : (
                  <Empty
                    description="请选择模板"
                    image={Empty.PRESENTED_IMAGE_SIMPLE}
                  />
                )}
              </div>
            </div>
          </Spin>
        </Drawer>

        {/* 运行测试模态窗口 */}
        <Modal
          title={
            <Space>
              <Typography.Text strong>运行测试</Typography.Text>
              {runningPlugin && (
                <Tag color="purple">{runningPlugin.pluginName}</Tag>
              )}
            </Space>
          }
          open={runModalVisible}
          onCancel={handleCloseRunModal}
          width={800}
          footer={[
            <Button key="cancel" onClick={handleCloseRunModal}>
              关闭
            </Button>,
            <Button
              key="run"
              type="primary"
              icon={<PlayCircleOutlined />}
              onClick={handleRunPlugin}
              loading={runLoading}
            >
              运行
            </Button>,
          ]}
        >
          <Spin spinning={runParamsLoading}>
            <Form layout="vertical">
              <Form.Item label="运行参数是完整有效的 json 格式，如果是字符串则直接输入，如果是对象则输入 JSON 格式">
                <Input.TextArea
                  rows={8}
                  value={runParamsValue}
                  onChange={(e) => setRunParamsValue(e.target.value)}
                  placeholder="请输入运行参数（JSON 格式）"
                  style={{ fontFamily: "monospace" }}
                />
              </Form.Item>

              {runResult && (
                <Alert
                  type={runResult.success ? "success" : "error"}
                  message={runResult.success ? "运行成功" : "运行失败"}
                  description={
                    <Typography.Text
                      style={{
                        whiteSpace: "pre-wrap",
                        wordBreak: "break-all",
                      }}
                    >
                      {runResult.message}
                    </Typography.Text>
                  }
                  showIcon
                  style={{ marginTop: 16 }}
                />
              )}
            </Form>
          </Spin>
          {/* 自动换行按钮 */}
          <div style={{ marginTop: 16, display: "flex", justifyContent: "flex-end" }}>
            <Space>
              <Typography.Text>自动换行</Typography.Text>
              <Switch
                checked={autoWrap}
                onChange={(checked) => setAutoWrap(checked)}
              />
            </Space>
          </div>
        </Modal>
      </div>
    </>
  );
}

