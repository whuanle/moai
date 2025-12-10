// === 导入依赖 ===
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
  ExpandOutlined,
  CodeOutlined,
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
import CodeEditorModal from "../../common/CodeEditorModal";

const { Title } = Typography;

// === 样式常量 ===
const STYLES = {
  pageContainer: { padding: 24 },
  headerContainer: {
    marginBottom: "16px",
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
  },
  mainLayout: { display: "flex", gap: "16px" },
  leftSidebar: {
    width: "200px",
    borderRight: "1px solid #f0f0f0",
    paddingRight: "16px",
  },
  classifyToggleContainer: { marginBottom: "12px" },
  classifyToggleButton: { flex: 1 },
  classifyItem: {
    cursor: "pointer",
    borderRadius: "4px",
    padding: "8px 12px",
    marginBottom: "4px",
    display: "flex",
    justifyContent: "space-between",
    alignItems: "center",
  },
  classifyItemSelected: {
    backgroundColor: "#e6f7ff",
  },
  classifyItemUnselected: {
    backgroundColor: "transparent",
  },
  iconSize: { fontSize: "16px" },
  searchContainer: { marginBottom: 16 },
  searchInput: { maxWidth: "400px" },
  drawerBody: { padding: 0 },
  drawerContent: { padding: 24 },
  drawerSidebar: {
    width: "200px",
    borderRight: "1px solid #f0f0f0",
    paddingRight: "16px",
    paddingLeft: "16px",
    paddingTop: "16px",
    overflowY: "auto",
    height: "100%",
  },
  drawerTemplateList: {
    width: "300px",
    borderRight: "1px solid #f0f0f0",
    paddingLeft: "16px",
    paddingRight: "16px",
    paddingTop: "16px",
    overflowY: "auto",
    height: "100%",
  },
  drawerForm: {
    flex: 1,
    paddingLeft: "16px",
    paddingRight: "16px",
    paddingTop: "16px",
    overflowY: "auto",
    height: "100%",
  },
  drawerFlexContainer: { display: "flex", height: "100%" },
  monospaceText: { fontSize: "12px", fontFamily: "monospace" },
  secondaryText: { fontSize: "12px" },
  resultTextArea: (success: boolean, autoWrap: boolean) => ({
    fontFamily: "monospace",
    whiteSpace: autoWrap ? "pre-wrap" : "pre",
    wordBreak: (autoWrap ? "break-all" : "normal") as "break-all" | "normal",
    backgroundColor: success ? "#f6ffed" : "#fff2f0",
    borderColor: success ? "#b7eb8f" : "#ffccc7",
  }),
  fullscreenModal: { top: 20 },
  autoWrapContainer: {
    marginBottom: 16,
    display: "flex",
    justifyContent: "flex-end",
  },
} as const;

// === 主组件 ===
export default function NativePluginPage() {
  // === 状态管理：主列表相关 ===
  const [pluginList, setPluginList] = useState<NativePluginInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchName, setSearchName] = useState<string>("");
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);
  const [allPluginList, setAllPluginList] = useState<NativePluginInfo[]>([]);
  const [templateListForDisplay, setTemplateListForDisplay] = useState<
    NativePluginTemplateInfo[]
  >([]);
  const [templateClassifyCount, setTemplateClassifyCount] = useState<
    Array<{ key: string; value: number }>
  >([]);

  // === 状态管理：分类筛选相关 ===
  const [leftClassifyType, setLeftClassifyType] = useState<"template" | "api">(
    "template"
  );
  const [selectedLeftClassify, setSelectedLeftClassify] = useState<
    string | number | "all"
  >("all");
  const [showTemplatesOnly, setShowTemplatesOnly] = useState<boolean>(false);

  // === 状态管理：模板面板相关 ===
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [templateList, setTemplateList] = useState<NativePluginTemplateInfo[]>(
    []
  );
  const [templateLoading, setTemplateLoading] = useState(false);
  const [selectedClassify, setSelectedClassify] = useState<string>("all");
  const [selectedTemplate, setSelectedTemplate] =
    useState<NativePluginTemplateInfo | null>(null);
  const [templateParams, setTemplateParams] = useState<
    NativePluginConfigFieldTemplate[]
  >([]);
  const [paramsLoading, setParamsLoading] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);
  const [form] = Form.useForm();
  const [templateClassify, setTemplateClassify] =
    useState<TemplateItem[]>(ClassifyList);

  // === 状态管理：编辑相关 ===
  const [editingPlugin, setEditingPlugin] = useState<NativePluginInfo | null>(
    null
  );
  const [editDrawerVisible, setEditDrawerVisible] = useState(false);
  const [editForm] = Form.useForm();
  const [editLoading, setEditLoading] = useState(false);
  const [editParamsLoading, setEditParamsLoading] = useState(false);
  const [editTemplateParams, setEditTemplateParams] = useState<
    NativePluginConfigFieldTemplate[]
  >([]);

  // === 状态管理：运行测试相关 ===
  const [runModalVisible, setRunModalVisible] = useState(false);
  const [runningPlugin, setRunningPlugin] = useState<NativePluginInfo | null>(
    null
  );
  const [runParamsValue, setRunParamsValue] = useState<string>("");
  const [runParamsLoading, setRunParamsLoading] = useState(false);
  const [runLoading, setRunLoading] = useState(false);
  const [runResult, setRunResult] = useState<{
    success: boolean;
    message: string | null | undefined;
  } | null>(null);
  const [runResultOriginalMessage, setRunResultOriginalMessage] = useState<
    string | null | undefined
  >(null);
  const [autoWrap, setAutoWrap] = useState<boolean>(false);
  const [resultFullscreenVisible, setResultFullscreenVisible] = useState(false);

  // === 状态管理：代码编辑器相关 ===
  const [codeEditorVisible, setCodeEditorVisible] = useState(false);
  const [codeEditorFieldKey, setCodeEditorFieldKey] = useState<string | null>(
    null
  );
  const [codeEditorInitialValue, setCodeEditorInitialValue] = useState<
    string
  >("");
  const [codeEditorFormInstance, setCodeEditorFormInstance] = useState<any>(
    null
  );

  const [messageApi, contextHolder] = message.useMessage();

  // === 辅助函数 ===
  /**
   * 将 ClassifyList 的 key 转换为枚举值
   */
  const keyToEnum = useCallback((key: string): NativePluginClassify | null => {
    const enumEntry = Object.entries(NativePluginClassifyObject).find(
      ([_, value]) => value.toLowerCase() === key.toLowerCase()
    );
    return enumEntry ? (enumEntry[1] as NativePluginClassify) : null;
  }, []);

  // 读取 json 字符串，则页面显示时使用
  const getJsonString = function (json: string): string {
    if (json === "" || json === null || json === undefined) {
      return "";
    }

    // 尝试将 字符串解析为 object
    try {
      const obj = JSON.parse(json);
      // 如果还是字符串，则说明本身真的是字符串，则返回解析后端字符串
      if (typeof obj === "string") {
        return obj;
      }

      // 如果是对象，则保留原本的 json 格式
      if (typeof obj === "object") {
        return json;
      }
    } catch (error) {
      return json;
    }

    return json;
  };

  const setJsonString = function (json: any): string {
    if (json === "" || json === null || json === undefined) {
      return "";
    }

    if (typeof json === "string") {
      // 如果是字符串，检查是一级字符串还是二级字符串
      try {
        const obj = JSON.parse(json);

        // 已经是二级字符串，返回最后的结果
        if (typeof obj === "string") {
          return obj;
        }

        return json;
      } catch (error) {
        return json;
      }
    }

    if (typeof json === "object") {
      return JSON.stringify(json, null, 2);
    }

    return JSON.stringify(json, null, 2);
  };

  /**
   * 截断文本，超过指定长度时显示省略号
   */
  const truncateText = useCallback((text: string | null | undefined, maxLength: number = 100): string => {
    if (!text) {
      return "无描述";
    }
    if (text.length <= maxLength) {
      return text;
    }
    return text.substring(0, maxLength) + "...";
  }, []);

  /**
   * 打开代码编辑器
   */
  const handleOpenCodeEditor = useCallback(
    (fieldKey: string, currentValue: string, formInstance: any) => {
      setCodeEditorFieldKey(fieldKey);
      setCodeEditorInitialValue(currentValue || "");
      setCodeEditorFormInstance(formInstance);
      setCodeEditorVisible(true);
    },
    []
  );

  /**
   * 关闭代码编辑器
   */
  const handleCloseCodeEditor = useCallback(() => {
    setCodeEditorVisible(false);
    setCodeEditorFieldKey(null);
    setCodeEditorInitialValue("");
    setCodeEditorFormInstance(null);
  }, []);

  /**
   * 确认代码编辑器内容
   */
  const handleConfirmCodeEditor = useCallback(
    (value: string) => {
      if (codeEditorFieldKey && codeEditorFormInstance) {
        codeEditorFormInstance.setFieldsValue({
          [codeEditorFieldKey]: value,
        });
      }
      handleCloseCodeEditor();
    },
    [codeEditorFieldKey, codeEditorFormInstance, handleCloseCodeEditor]
  );

  /**
   * 渲染模板参数字段表单项
   */
  const renderParamFormItem = useCallback(
    (param: NativePluginConfigFieldTemplate, formInstance?: any) => {
      if (!param.key) return null;

      const fieldType = param.fieldType;
      const isRequired = param.isRequired === true;
      const label = (
        <Space>
          <Typography.Text>{param.key}</Typography.Text>
          {isRequired && <Typography.Text type="danger">*</Typography.Text>}
        </Space>
      );

      // 根据字段类型解析默认值
      let initialValue: any = undefined;
      if (param.exampleValue !== null && param.exampleValue !== undefined) {
        if (fieldType === PluginConfigFieldTypeObject.Boolean) {
          const valueStr = String(param.exampleValue).toLowerCase();
          initialValue = valueStr === "true" || valueStr === "1";
        } else if (
          fieldType === PluginConfigFieldTypeObject.Number ||
          fieldType === PluginConfigFieldTypeObject.Integer
        ) {
          initialValue = Number(param.exampleValue);
        } else {
          initialValue = param.exampleValue;
        }
      }

      // code 类型优先处理
      if (fieldType === PluginConfigFieldTypeObject.Code) {
        return (
          <Form.Item
            key={param.key}
            name={param.key}
            label={label}
            help={param.description || undefined}
            initialValue={initialValue || "// 在这里写代码"}
            rules={
              isRequired
                ? [{ required: true, message: `请输入${param.key}` }]
                : []
            }
          >
            <Form.Item noStyle shouldUpdate>
              {({ getFieldValue }) => {
                const fieldValue = getFieldValue(param.key);
                return (
                  <div>
                    <Input.TextArea
                      rows={6}
                      placeholder="JavaScript 代码"
                      readOnly
                      value={
                        fieldValue ||
                        initialValue ||
                        "// 在这里写代码"
                      }
                      style={{ fontFamily: "monospace", fontSize: "14px" }}
                    />
                    <div style={{ marginTop: 8, textAlign: "right" }}>
                      <Tooltip title="打开代码编辑器">
                        <Button
                          type="link"
                          size="small"
                          icon={<CodeOutlined />}
                          onClick={() => {
                            if (!param.key) return;
                            const currentValue =
                              fieldValue ||
                              initialValue ||
                              "";
                            handleOpenCodeEditor(
                              param.key,
                              currentValue,
                              formInstance
                            );
                          }}
                        >
                          打开代码编辑器
                        </Button>
                      </Tooltip>
                    </div>
                  </div>
                );
              }}
            </Form.Item>
          </Form.Item>
        );
      }

      if (fieldType === PluginConfigFieldTypeObject.Boolean) {
        return (
          <Form.Item
            key={param.key}
            name={param.key}
            label={label}
            help={param.description || undefined}
            valuePropName="checked"
            initialValue={initialValue}
            rules={
              isRequired
                ? [{ required: true, message: `请输入${param.key}` }]
                : []
            }
          >
            <Switch />
          </Form.Item>
        );
      }

      if (
        fieldType === PluginConfigFieldTypeObject.Number ||
        fieldType === PluginConfigFieldTypeObject.Integer
      ) {
        return (
          <Form.Item
            key={param.key}
            name={param.key}
            label={label}
            help={param.description || undefined}
            initialValue={initialValue}
            rules={
              isRequired
                ? [{ required: true, message: `请输入${param.key}` }]
                : []
            }
          >
            <InputNumber style={{ width: "100%" }} />
          </Form.Item>
        );
      }

      if (
        fieldType === PluginConfigFieldTypeObject.Object ||
        fieldType === PluginConfigFieldTypeObject.Map
      ) {
        return (
          <Form.Item
            key={param.key}
            name={param.key}
            label={label}
            help={param.description || undefined}
            initialValue={initialValue}
            rules={
              isRequired
                ? [{ required: true, message: `请输入${param.key}` }]
                : []
            }
          >
            <Input.TextArea rows={4} placeholder="请输入 JSON 格式" />
          </Form.Item>
        );
      }


      // 默认字符串类型
      return (
        <Form.Item
          key={param.key}
          name={param.key}
          label={label}
          help={param.description || undefined}
          initialValue={initialValue}
          rules={
            isRequired
              ? [{ required: true, message: `请输入${param.key}` }]
              : []
          }
        >
          <Input.TextArea
            rows={3}
            placeholder={
              param.exampleValue ? `示例: ${param.exampleValue}` : ""
            }
          />
        </Form.Item>
      );
    },
    [handleOpenCodeEditor]
  );

  // === 数据获取函数 ===
  /**
   * 获取分类列表
   */
  const fetchClassifyList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items || []);
      }
    } catch (error) {
      console.log("Fetch classify list error:", error);
      proxyRequestError(error, messageApi, "获取分类列表失败");
    }
  }, [messageApi]);

  /**
   * 获取所有插件数据（用于计算分类数量）
   */
  const fetchAllPluginList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const requestData: QueryNativePluginListCommand = {};
      const response = await client.api.admin.native_plugin.list.post(
        requestData
      );
      if (response?.items) {
        setAllPluginList(response.items);
      }
    } catch (error) {
      console.log("Fetch all plugin list error:", error);
    }
  }, []);

  /**
   * 获取所有模板的分类数量（用于左侧分类显示）
   */
  const fetchTemplateClassifyCount = useCallback(async () => {
    try {
      const client = GetApiClient();
      const requestData: QueryNativePluginTemplateListCommand = {
        classify: undefined, // 不传分类参数，获取所有分类的数量
      };
      const response = await client.api.admin.native_plugin.template_list.post(
        requestData
      );
      if (response?.classifyCount) {
        // 将 KeyValueOfStringAndInt32[] 转换为简单的数组格式
        const countMap = response.classifyCount
          .filter((item) => item.key && typeof item.value === "number")
          .map((item) => ({
            key: item.key!,
            value: item.value!,
          }));
        setTemplateClassifyCount(countMap);
      } else {
        setTemplateClassifyCount([]);
      }
    } catch (error) {
      console.log("Fetch template classify count error:", error);
      setTemplateClassifyCount([]);
    }
  }, []);

  /**
   * 获取模板列表（用于主表格显示）
   */
  const fetchTemplateListForDisplay = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      // 根据左侧选中的分类来设置筛选条件
      let classify: NativePluginClassify | undefined = undefined;

      if (
        selectedLeftClassify !== "all" &&
        typeof selectedLeftClassify === "string"
      ) {
        const enumValue = keyToEnum(selectedLeftClassify);
        if (enumValue) {
          classify = enumValue;
        }
      }

      const requestData: QueryNativePluginTemplateListCommand = {
        classify: classify || undefined,
      };
      const response = await client.api.admin.native_plugin.template_list.post(
        requestData
      );

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

  /**
   * 获取内置插件列表
   */
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
          classifyId =
            typeof selectedLeftClassify === "number"
              ? selectedLeftClassify
              : undefined;
        } else {
          // 使用模板分类（templatePluginClassify）
          const enumValue =
            typeof selectedLeftClassify === "string"
              ? keyToEnum(selectedLeftClassify)
              : null;
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
      const response = await client.api.admin.native_plugin.list.post(
        requestData
      );

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
  }, [
    messageApi,
    searchName,
    selectedLeftClassify,
    leftClassifyType,
    keyToEnum,
  ]);

  // === 副作用处理 ===
  /**
   * 页面加载时获取数据
   */
  useEffect(() => {
    fetchClassifyList();
    // 页面加载时，fetchPluginList 会获取全部数据并同时更新 allPluginList，避免重复请求
  }, [fetchClassifyList]);

  /**
   * 当筛选条件变化时，重新获取插件列表或模板列表
   */
  useEffect(() => {
    if (leftClassifyType === "template" && showTemplatesOnly) {
      // 模板分类模式下，如果开启了"只看模板"，则获取模板列表
      fetchTemplateListForDisplay();
      // 如果还没有获取过模板分类数量，则获取一次
      if (templateClassifyCount.length === 0) {
        fetchTemplateClassifyCount();
      }
    } else {
      // 否则获取插件列表
      fetchPluginList();
    }
  }, [
    fetchPluginList,
    fetchTemplateListForDisplay,
    fetchTemplateClassifyCount,
    leftClassifyType,
    showTemplatesOnly,
    selectedLeftClassify,
    templateClassifyCount.length,
  ]);

  // === 事件处理函数 ===
  /**
   * 刷新列表
   */
  const handleRefresh = useCallback(async () => {
    // 先刷新全部插件列表（用于左侧分类数量统计）
    await fetchAllPluginList();
    // 再刷新当前筛选的列表（插件或模板）
    if (leftClassifyType === "template" && showTemplatesOnly) {
      // 刷新模板列表和模板分类数量
      await fetchTemplateClassifyCount();
      fetchTemplateListForDisplay();
    } else {
      fetchPluginList();
    }
  }, [
    fetchPluginList,
    fetchTemplateListForDisplay,
    fetchAllPluginList,
    fetchTemplateClassifyCount,
    leftClassifyType,
    showTemplatesOnly,
  ]);

  /**
   * 编辑插件
   */
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
        const detailResponse =
          await client.api.admin.native_plugin.detail.post(
            detailRequest
          );

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
            const paramsResponse =
              await client.api.admin.native_plugin.template_params.post(
                paramsRequest
              );

            if (paramsResponse?.items) {
              setEditTemplateParams(paramsResponse.items);

              // 解析params JSON并设置表单值
              if (detailResponse.params) {
                try {
                  const paramsObj = JSON.parse(detailResponse.params);
                  const initialValues: Record<string, any> = {};
                  paramsResponse.items.forEach((item) => {
                    if (
                      item.key &&
                      paramsObj[item.key] !== undefined &&
                      paramsObj[item.key] !== null
                    ) {
                      const fieldType = item.fieldType;
                      if (
                        fieldType === PluginConfigFieldTypeObject.Number ||
                        fieldType === PluginConfigFieldTypeObject.Integer
                      ) {
                        initialValues[item.key] = Number(paramsObj[item.key]);
                      } else if (
                        fieldType === PluginConfigFieldTypeObject.Boolean
                      ) {
                        const valueStr = String(paramsObj[item.key]);
                        initialValues[item.key] =
                          valueStr === "true" || valueStr === "1";
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

  /**
   * 关闭编辑抽屉
   */
  const handleCloseEditDrawer = useCallback(() => {
    setEditDrawerVisible(false);
    setEditingPlugin(null);
    setEditTemplateParams([]);
    editForm.resetFields();
  }, [editForm]);

  /**
   * 打开运行测试模态窗口
   */
  const handleOpenRunModal = useCallback(
    async (record: NativePluginInfo) => {
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
        const detailResponse =
          await client.api.admin.native_plugin.detail.post(
            detailRequest
          );

        // 更新 runningPlugin 的描述信息
        if (detailResponse) {
          setRunningPlugin({
            ...record,
            description: detailResponse.description || record.description,
          });
        }

        if (detailResponse?.templatePluginKey) {
          // 获取模板参数示例值
          const paramsRequest: QueryNativePluginTemplateParamsCommand = {
            templatePluginKey: detailResponse.templatePluginKey,
          };
          const paramsResponse =
            await client.api.admin.native_plugin.template_params.post(
              paramsRequest
            );

          // 获取运行参数示例值
          const exampleValue = getJsonString(
            paramsResponse?.exampleValue || ""
          );
          setRunParamsValue(exampleValue);
        }
      } catch (error) {
        console.log("Fetch run params error:", error);
        proxyRequestError(error, messageApi, "获取运行参数失败");
      } finally {
        setRunParamsLoading(false);
      }
    },
    [messageApi]
  );

  /**
   * 关闭运行测试模态窗口
   */
  const handleCloseRunModal = useCallback(() => {
    setRunModalVisible(false);
    setRunningPlugin(null);
    setRunParamsValue("");
    setRunResult(null);
    setRunResultOriginalMessage(null);
    setAutoWrap(false); // 重置自动换行状态
    setResultFullscreenVisible(false); // 关闭全屏查看
  }, []);

  /**
   * 处理自动换行切换
   */
  const handleAutoWrapChange = useCallback(
    (checked: boolean) => {
      setAutoWrap(checked);

      // 如果有运行结果，根据开关状态格式化或恢复原始内容
      if (
        runResult &&
        runResultOriginalMessage !== null &&
        runResultOriginalMessage !== undefined
      ) {
        if (checked) {
          // 开启自动换行时，尝试格式化 JSON
          try {
            const parsed = getJsonString(runResultOriginalMessage);
            setRunResult({
              ...runResult,
              message: parsed,
            });
          } catch (error) {
            // 如果解析失败，使用原始消息
            setRunResult({
              ...runResult,
              message: runResultOriginalMessage,
            });
          }
        } else {
          // 关闭自动换行时，恢复原始消息
          setRunResult({
            ...runResult,
            message: runResultOriginalMessage,
          });
        }
      }
    },
    [runResult, runResultOriginalMessage]
  );

  /**
   * 运行插件
   */
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
      const paramsString = setJsonString(runParamsValue);

      const client = GetApiClient();
      const requestData: RunTestNativePluginCommand = {
        templatePluginKey: runningPlugin.templatePluginKey || undefined,
        pluginId: runningPlugin.pluginId || undefined,
        params: paramsString,
      };

      const response = await client.api.admin.native_plugin.run_test.post(
        requestData
      );

      if (response) {
        const originalMessage = response.response!;
        // 保存原始消息
        setRunResultOriginalMessage(originalMessage);

        let message = getJsonString(originalMessage);
        setRunResult({
          success: response.isSuccess!,
          message: message,
        });
        messageApi.success(
          response.isSuccess == true ? "插件运行成功" : "插件运行失败"
        );
      }
    } catch (error) {
      console.log("Run plugin error:", error);

      // 提取详细的错误信息
      let errorMessage = "运行插件时发生错误";
      const businessError = error as any;

      if (businessError?.detail) {
        // 优先使用业务错误详情
        errorMessage = businessError.detail;
      } else if (businessError?.message) {
        // 其次使用错误消息
        errorMessage = businessError.message;
      } else if (error instanceof Error) {
        // 使用 Error 对象的 message
        errorMessage = error.message;
      } else if (typeof error === "string") {
        // 如果是字符串，直接使用
        errorMessage = error;
      } else if (error) {
        // 最后尝试转换为字符串
        errorMessage = String(error);
      }

      // 保存原始错误消息
      setRunResultOriginalMessage(errorMessage);
      setRunResult({
        success: false,
        message: errorMessage,
      });
      proxyRequestError(error, messageApi, "运行插件失败");
    } finally {
      setRunLoading(false);
    }
  }, [runningPlugin, runParamsValue, messageApi, autoWrap]);

  /**
   * 提交编辑
   */
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
        if (
          param.key &&
          values[param.key] !== undefined &&
          values[param.key] !== null
        ) {
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
        config:
          Object.keys(paramsObj).length > 0
            ? JSON.stringify(paramsObj)
            : undefined,
      };

      await client.api.admin.native_plugin.update.post(requestData);

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
  }, [
    editingPlugin,
    editForm,
    editTemplateParams,
    messageApi,
    fetchPluginList,
    fetchAllPluginList,
    handleCloseEditDrawer,
  ]);

  // 删除插件
  const handleDelete = useCallback(
    async (pluginId: number) => {
      try {
        const client = GetApiClient();
        const requestData: DeleteNativePluginCommand = {
          pluginId: pluginId,
        };
        await client.api.admin.native_plugin.deletePath.delete(requestData);

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
  const handleRunTemplate = useCallback(
    async (template: NativePluginTemplateInfo) => {
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
        const paramsResponse =
          await client.api.admin.native_plugin.template_params.post(
            paramsRequest
          );
        const exampleValue = getJsonString(paramsResponse?.exampleValue!);

        setRunParamsValue(exampleValue);
      } catch (error) {
        console.log("Fetch template run params error:", error);
        proxyRequestError(error, messageApi, "获取运行参数失败");
      } finally {
        setRunParamsLoading(false);
      }
    },
    [messageApi]
  );

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
        (template.description &&
          template.description.toLowerCase().includes(searchLower))
    );
  }, [templateListForDisplay, searchName]);

  // 表格列定义
  const columns = useMemo(
    () => [
      {
        title: "插件名称",
        dataIndex: "pluginName",
        key: "pluginName",
        render: (
          pluginName: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 如果是模板（没有 pluginId），使用 name 字段
          if (!("pluginId" in record)) {
            return (
              <Typography.Text strong>
                {(record as NativePluginTemplateInfo).name || "-"}
              </Typography.Text>
            );
          }
          return <Typography.Text strong>{pluginName}</Typography.Text>;
        },
      },
      {
        title: "标题",
        dataIndex: "title",
        key: "title",
        render: (
          title: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 如果是模板（没有 pluginId），使用 name 字段
          if (!("pluginId" in record)) {
            return (record as NativePluginTemplateInfo).name || "-";
          }
          return title || "-";
        },
      },
      {
        title: "模板Key",
        dataIndex: "templatePluginKey",
        key: "templatePluginKey",
        render: (
          templatePluginKey: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 如果是模板（没有 pluginId），使用 key 字段
          if (!("pluginId" in record)) {
            return (
              <Typography.Text type="secondary" style={STYLES.monospaceText}>
                {(record as NativePluginTemplateInfo).key || "-"}
              </Typography.Text>
            );
          }
          return (
            <Typography.Text type="secondary" style={STYLES.monospaceText}>
              {templatePluginKey || "-"}
            </Typography.Text>
          );
        },
      },
      {
        title: "描述",
        dataIndex: "description",
        key: "description",
        width: 300,
        ellipsis: {
          showTitle: false,
        },
        render: (description: string) => (
          <Tooltip placement="topLeft" title={description || "-"}>
            <Typography.Text
              type="secondary"
              style={STYLES.secondaryText}
              ellipsis
            >
              {description || "-"}
            </Typography.Text>
          </Tooltip>
        ),
      },
      {
        title: "是否公开",
        dataIndex: "isPublic",
        key: "isPublic",
        width: 100,
        render: (
          isPublic: boolean,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 如果是模板（没有 pluginId），不显示
          if (!("pluginId" in record)) {
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
        render: (
          createTime: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 如果是模板（没有 pluginId），不显示
          if (!("pluginId" in record)) {
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
        render: (
          createUserName: string,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 如果是模板（没有 pluginId），不显示
          if (!("pluginId" in record)) {
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
        render: (
          _: any,
          record: NativePluginInfo | NativePluginTemplateInfo
        ) => {
          // 判断是否是模板（没有 pluginId 就是模板）
          const isTemplate = !("pluginId" in record);
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
      const response = await client.api.admin.native_plugin.template_list.post(
        requestData
      );
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
              (cv) =>
                cv.key &&
                cv.key.toLowerCase() === templateItem.key.toLowerCase()
            );
            if (countItem && typeof countItem.value === "number") {
              templateItem.count = countItem.value;
            }
          });
        }

        setTemplateClassify(templateItems);
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
  const fetchTemplateParams = useCallback(
    async (templateKey: string, oldParamKeys?: string[]) => {
      setParamsLoading(true);
      try {
        const client = GetApiClient();
        const requestData: QueryNativePluginTemplateParamsCommand = {
          templatePluginKey: templateKey,
        };
        const response =
          await client.api.admin.native_plugin.template_params.post(
            requestData
          );
        if (response?.items) {
          // 先清除所有旧参数的值（如果提供了旧参数keys）
          if (oldParamKeys && oldParamKeys.length > 0) {
            const fieldsToReset: Record<string, undefined> = {};
            oldParamKeys.forEach((key) => {
              fieldsToReset[key] = undefined;
            });
            form.setFieldsValue(fieldsToReset);
          }

          // 设置新参数
          setTemplateParams(response.items);

          // 根据新参数的 exampleValue 设置默认值
          const initialValues: Record<string, any> = {};
          response.items.forEach((param) => {
            if (
              param.key &&
              param.exampleValue !== null &&
              param.exampleValue !== undefined
            ) {
              const fieldType = param.fieldType;
              if (fieldType === PluginConfigFieldTypeObject.Boolean) {
                const valueStr = String(param.exampleValue).toLowerCase();
                initialValues[param.key] =
                  valueStr === "true" || valueStr === "1";
              } else if (
                fieldType === PluginConfigFieldTypeObject.Number ||
                fieldType === PluginConfigFieldTypeObject.Integer
              ) {
                initialValues[param.key] = Number(param.exampleValue);
              } else {
                initialValues[param.key] = param.exampleValue;
              }
            }
          });

          // 设置新参数的默认值
          if (Object.keys(initialValues).length > 0) {
            form.setFieldsValue(initialValues);
          }
        } else {
          setTemplateParams([]);
          // 清除所有旧参数的值（如果提供了旧参数keys）
          if (oldParamKeys && oldParamKeys.length > 0) {
            const fieldsToReset: Record<string, undefined> = {};
            oldParamKeys.forEach((key) => {
              fieldsToReset[key] = undefined;
            });
            form.setFieldsValue(fieldsToReset);
          }
        }
      } catch (error) {
        console.log("Fetch template params error:", error);
        proxyRequestError(error, messageApi, "获取模板参数失败");
        setTemplateParams([]);
        // 清除所有旧参数的值（如果提供了旧参数keys）
        if (oldParamKeys && oldParamKeys.length > 0) {
          const fieldsToReset: Record<string, undefined> = {};
          oldParamKeys.forEach((key) => {
            fieldsToReset[key] = undefined;
          });
          form.setFieldsValue(fieldsToReset);
        }
      } finally {
        setParamsLoading(false);
      }
    },
    [messageApi, form]
  );

  // 点击模板项
  const handleTemplateClick = useCallback(
    (template: NativePluginTemplateInfo) => {
      // 先保存旧参数的keys
      const oldParamKeys = templateParams
        .map((p) => p.key)
        .filter((k): k is string => !!k);

      setSelectedTemplate(template);
      // 设置表单默认值：name 使用模板的 key，title 使用模板的 name
      form.setFieldsValue({
        name: template.key || "",
        title: template.name || "",
      });
      if (template.key) {
        fetchTemplateParams(template.key, oldParamKeys);
      }
    },
    [fetchTemplateParams, form, templateParams]
  );

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
        if (
          param.key &&
          values[param.key] !== undefined &&
          values[param.key] !== null
        ) {
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
        config:
          Object.keys(paramsObj).length > 0
            ? JSON.stringify(paramsObj)
            : undefined,
      };

      const response = await client.api.admin.native_plugin.create.post(
        requestData
      );

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
  }, [
    selectedTemplate,
    form,
    templateParams,
    messageApi,
    fetchPluginList,
    fetchAllPluginList,
    handleCloseDrawer,
  ]);

  return (
    <>
      {contextHolder}
      <div style={STYLES.pageContainer}>
        <Card>
          <div style={STYLES.headerContainer}>
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
          <div style={STYLES.mainLayout}>
            {/* 左侧分类列表 */}
            <div style={STYLES.leftSidebar}>
              {/* 分类类型切换按钮 */}
              <div style={STYLES.classifyToggleContainer}>
                <Button.Group style={{ width: "100%" }}>
                  <Button
                    type={
                      leftClassifyType === "template" ? "primary" : "default"
                    }
                    size="small"
                    onClick={() => {
                      setLeftClassifyType("template");
                      setSelectedLeftClassify("all");
                      setShowTemplatesOnly(false);
                    }}
                    style={STYLES.classifyToggleButton}
                  >
                    模板分类
                  </Button>
                  <Button
                    type={leftClassifyType === "api" ? "primary" : "default"}
                    size="small"
                    onClick={() => {
                      setLeftClassifyType("api");
                      setSelectedLeftClassify("all");
                      setShowTemplatesOnly(false);
                    }}
                    style={STYLES.classifyToggleButton}
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
                        {
                          key: "all" as const,
                          name: "全部",
                          icon: "📋",
                          count: showTemplatesOnly
                            ? templateClassifyCount.reduce(
                                (sum, item) => sum + item.value,
                                0
                              )
                            : allPluginList.length,
                        },
                        ...ClassifyList.map((item) => {
                          let count = 0;
                          if (showTemplatesOnly) {
                            // 只看模板模式下，使用接口返回的分类数量
                            const countItem = templateClassifyCount.find(
                              (cv) =>
                                cv.key &&
                                cv.key.toLowerCase() === item.key.toLowerCase()
                            );
                            count = countItem ? countItem.value : 0;
                          } else {
                            // 插件模式下，使用插件列表计算数量
                            const enumValue = keyToEnum(item.key);
                            count = enumValue
                              ? allPluginList.filter(
                                  (plugin) =>
                                    plugin.templatePluginClassify === enumValue
                                ).length
                              : 0;
                          }
                          return {
                            key: item.key,
                            name: item.name,
                            icon: item.icon,
                            count,
                          };
                        }),
                      ]
                    : [
                        {
                          key: "all" as const,
                          name: "全部",
                          icon: undefined,
                          count: allPluginList.length,
                        },
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
                      ]) as Array<{
                    key: string | number | "all";
                    name: string;
                    icon?: string;
                    count: number;
                  }>
                }
                renderItem={(item) => {
                  const isSelected = selectedLeftClassify === item.key;

                  return (
                    <List.Item
                      style={{
                        ...STYLES.classifyItem,
                        backgroundColor: isSelected
                          ? STYLES.classifyItemSelected.backgroundColor
                          : STYLES.classifyItemUnselected.backgroundColor,
                      }}
                      onClick={() => setSelectedLeftClassify(item.key)}
                    >
                      <Space>
                        {"icon" in item && item.icon && (
                          <span style={STYLES.iconSize}>{item.icon}</span>
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
              <div style={STYLES.searchContainer}>
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
                      if (
                        leftClassifyType !== "template" ||
                        !showTemplatesOnly
                      ) {
                        fetchPluginList();
                      }
                    }}
                    enterButton
                    style={STYLES.searchInput}
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
                  if (!("pluginId" in record)) {
                    return (record as NativePluginTemplateInfo).key || "";
                  }
                  return (
                    (record as NativePluginInfo).pluginId?.toString() || ""
                  );
                }}
                loading={loading}
                pagination={false}
                scroll={{ x: 1500 }}
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
          styles={{ body: STYLES.drawerBody }}
        >
          <Spin spinning={editLoading}>
            <div style={STYLES.drawerContent}>
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
                        {
                          pattern: /^[a-zA-Z_]+$/,
                          message: "插件名称只能包含字母和下划线",
                        },
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
                          <Select
                            placeholder="请选择分类"
                            allowClear
                            style={{ width: "100%" }}
                          >
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
                          label="是否公开"
                          valuePropName="checked"
                        >
                          <Switch
                            checkedChildren="公开"
                            unCheckedChildren="私有"
                          />
                        </Form.Item>
                      </Col>
                    </Row>

                    <Divider>模板参数</Divider>

                    {editTemplateParams.map((param) =>
                      renderParamFormItem(param, editForm)
                    )}
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
          styles={{ body: STYLES.drawerBody }}
        >
          <Spin spinning={templateLoading}>
            <div style={STYLES.drawerFlexContainer}>
              {/* 左侧分类列表 */}
              <div style={STYLES.drawerSidebar}>
                <List
                  size="small"
                  dataSource={[
                    {
                      key: "all",
                      name: "全部",
                      icon: "📋",
                      count: templateList.length,
                      templates: [],
                    },
                    ...templateClassify,
                  ]}
                  renderItem={(item) => {
                    const isSelected = selectedClassify === item.key;

                    return (
                      <List.Item
                        style={{
                          ...STYLES.classifyItem,
                          backgroundColor: isSelected
                            ? STYLES.classifyItemSelected.backgroundColor
                            : STYLES.classifyItemUnselected.backgroundColor,
                        }}
                        onClick={() => setSelectedClassify(item.key)}
                      >
                        <Space>
                          <span style={STYLES.iconSize}>{item.icon}</span>
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
              <div style={STYLES.drawerTemplateList}>
                {selectedClassify ? (
                  <List
                    dataSource={currentTemplates}
                    renderItem={(template: NativePluginTemplateInfo) => (
                      <List.Item
                        style={{
                          ...STYLES.classifyItem,
                          backgroundColor:
                            selectedTemplate?.key === template.key
                              ? STYLES.classifyItemSelected.backgroundColor
                              : STYLES.classifyItemUnselected.backgroundColor,
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
                            <Typography.Text
                              type="secondary"
                              style={STYLES.secondaryText}
                            >
                              {truncateText(template.description, 100)}
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
              <div style={STYLES.drawerForm}>
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
                            {
                              pattern: /^[a-zA-Z_]+$/,
                              message: "插件名称只能包含字母和下划线",
                            },
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
                          rules={[
                            { required: true, message: "请输入插件标题" },
                          ]}
                        >
                          <Input placeholder="请输入插件标题" />
                        </Form.Item>

                        <Form.Item name="description" label="描述">
                          <Input.TextArea
                            rows={3}
                            placeholder="请输入插件描述"
                          />
                        </Form.Item>

                        <Row gutter={16}>
                          <Col span={12}>
                            <Form.Item
                              name="classifyId"
                              label={
                                <Space>
                                  <Typography.Text>分类</Typography.Text>
                                  <Typography.Text type="danger">
                                    *
                                  </Typography.Text>
                                </Space>
                              }
                              rules={[
                                { required: true, message: "请选择分类" },
                              ]}
                            >
                              <Select
                                placeholder="请选择分类"
                                allowClear
                                style={{ width: "100%" }}
                              >
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
                              label="是否公开"
                              valuePropName="checked"
                            >
                              <Switch
                                checkedChildren="公开"
                                unCheckedChildren="私有"
                              />
                            </Form.Item>
                          </Col>
                        </Row>

                        <Divider>模板参数</Divider>

                        {templateParams.map((param) =>
                          renderParamFormItem(param, form)
                        )}
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
          width={1200}
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
              <Form.Item label="如果是字符串则直接输入，如果是对象则输入 JSON 格式，请勿上传太大的文件。">
                {runningPlugin?.description && (
                  <Alert
                    message="插件描述"
                    description={
                      <Typography.Text
                        style={{
                          whiteSpace: "pre-wrap",
                          wordBreak: "break-word",
                        }}
                      >
                        {runningPlugin.description}
                      </Typography.Text>
                    }
                    type="info"
                    showIcon
                    style={{ marginBottom: 16 }}
                  />
                )}
                <Input.TextArea
                  rows={8}
                  value={runParamsValue}
                  onChange={(e) => setRunParamsValue(e.target.value)}
                  placeholder="请输入运行参数（JSON 格式）"
                  style={{ fontFamily: "monospace" }}
                />
              </Form.Item>

              {runResult && (
                <Form.Item
                  label={
                    <Space>
                      <Typography.Text strong>
                        {runResult.success ? "运行成功" : "运行失败"}
                      </Typography.Text>
                      <Button
                        type="link"
                        size="small"
                        icon={<ExpandOutlined />}
                        onClick={() => setResultFullscreenVisible(true)}
                      >
                        全屏查看
                      </Button>
                    </Space>
                  }
                >
                  <Input.TextArea
                    readOnly
                    rows={10}
                    value={runResult.message || ""}
                    style={STYLES.resultTextArea(runResult.success, autoWrap)}
                  />
                </Form.Item>
              )}
            </Form>
          </Spin>
        </Modal>

        {/* 运行结果全屏查看模态窗口 */}
        <Modal
          title={
            <Space>
              <Typography.Text strong>运行结果</Typography.Text>
              {runningPlugin && (
                <Tag color="purple">{runningPlugin.pluginName}</Tag>
              )}
              {runResult && (
                <Tag color={runResult.success ? "success" : "error"}>
                  {runResult.success ? "成功" : "失败"}
                </Tag>
              )}
            </Space>
          }
          open={resultFullscreenVisible}
          onCancel={() => setResultFullscreenVisible(false)}
          width="90%"
          style={STYLES.fullscreenModal}
          footer={[
            <Button
              key="close"
              onClick={() => setResultFullscreenVisible(false)}
            >
              关闭
            </Button>,
          ]}
        >
          {runResult && (
            <Input.TextArea
              readOnly
              rows={30}
              value={runResult.message || ""}
              style={STYLES.resultTextArea(runResult.success, autoWrap)}
            />
          )}
        </Modal>

        {/* 代码编辑器模态窗口 */}
        <CodeEditorModal
          open={codeEditorVisible}
          initialValue={codeEditorInitialValue}
          language="javascript"
          title="代码编辑器"
          onClose={handleCloseCodeEditor}
          onConfirm={handleConfirmCodeEditor}
          width={1200}
          height="70vh"
        />
      </div>
    </>
  );
}
