/**
 * 插件数据管理 Hook
 */
import { useState, useCallback, useEffect, useMemo, useRef } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../../ServiceClient";
import { proxyRequestError } from "../../../../../helper/RequestError";
import type {
  NativePluginInfo,
  PluginClassifyItem,
  NativePluginTemplateInfo,
  NativePluginConfigFieldTemplate,
  KeyValueBool,
} from "../../../../../apiClient/models";
import { TemplateItem, ClassifyList } from "../TemplatePlugin";
// 排序状态类型
export interface TableSortState {
  field: string | null;
  order: 'ascend' | 'descend' | null;
}

// JSON 字符串处理
export const getJsonString = (json: string): string => {
  if (!json) return "";
  try {
    const obj = JSON.parse(json);
    return typeof obj === "string" ? obj : json;
  } catch {
    return json;
  }
};

export const setJsonString = (json: any): string => {
  if (!json) return "";
  if (typeof json === "string") {
    try {
      const obj = JSON.parse(json);
      return typeof obj === "string" ? obj : json;
    } catch {
      return json;
    }
  }
  return JSON.stringify(json, null, 2);
};

// 将表格排序状态转换为后端 API 需要的 orderByFields 格式
const buildOrderByFields = (sortState: TableSortState): KeyValueBool[] | undefined => {
  if (!sortState.field || !sortState.order) {
    return undefined;
  }
  return [{
    key: sortState.field,
    value: sortState.order === 'ascend' // true 为升序，false 为降序
  }];
};

export function usePluginData() {
  const [messageApi, contextHolder] = message.useMessage();

  // 状态
  const [pluginList, setPluginList] = useState<NativePluginInfo[]>([]);
  const [allPluginList, setAllPluginList] = useState<NativePluginInfo[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchName, setSearchName] = useState("");
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);
  const [selectedClassify, setSelectedClassify] = useState<number | "all">("all");
  const [sortState, setSortState] = useState<TableSortState>({ field: null, order: null });

  // 使用 ref 存储最新值，避免作为依赖导致重复请求
  const searchNameRef = useRef(searchName);
  const selectedClassifyRef = useRef(selectedClassify);
  const sortStateRef = useRef(sortState);

  // 同步 ref
  searchNameRef.current = searchName;
  selectedClassifyRef.current = selectedClassify;
  sortStateRef.current = sortState;

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
      proxyRequestError(error, messageApi, "获取分类列表失败");
    }
  }, [messageApi]);

  // 获取所有插件（用于统计分类数量）
  const fetchAllPluginList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.admin.native_plugin.list.post({});
      if (response?.items) {
        setAllPluginList(response.items);
      }
    } catch (error) {
      console.error("获取全部插件列表失败:", error);
    }
  }, []);

  // 获取插件列表（带筛选和排序）- 使用 ref 读取最新值
  const fetchPluginList = useCallback(
    async (classify?: number | "all") => {
      setLoading(true);
      setPluginList([]); // 先清空列表
      try {
        const client = GetApiClient();
        const targetClassify = classify ?? selectedClassifyRef.current;
        const classifyId = targetClassify !== "all" ? targetClassify : undefined;
        const keyword = searchNameRef.current || undefined;

        const response = await client.api.admin.native_plugin.list.post({
          keyword,
          classifyId,
          orderByFields: buildOrderByFields(sortStateRef.current),
        });

        if (response?.items) {
          setPluginList(response.items);
          // 如果是全部且无搜索，同时更新 allPluginList
          if (targetClassify === "all" && !keyword) {
            setAllPluginList(response.items);
          }
        } else {
          setPluginList([]);
        }
      } catch (error) {
        console.error("获取插件列表失败:", error);
        proxyRequestError(error, messageApi, "获取内置插件列表失败");
        setPluginList([]);
      } finally {
        setLoading(false);
      }
    },
    [messageApi]
  );

  // 刷新（点击查找按钮时调用）
  const handleRefresh = useCallback(async () => {
    const currentClassify = selectedClassifyRef.current;
    const currentKeyword = searchNameRef.current;
    
    if (currentClassify !== "all" || currentKeyword) {
      await fetchAllPluginList();
    }
    fetchPluginList();
  }, [fetchPluginList, fetchAllPluginList]);

  // 选择分类
  const handleClassifySelect = useCallback(
    (key: number | "all") => {
      setSelectedClassify(key);
      // 直接传入新的分类值，不依赖 ref
      fetchPluginList(key);
    },
    [fetchPluginList]
  );

  // 处理排序变化
  const handleSortChange = useCallback(
    (newSortState: TableSortState) => {
      setSortState(newSortState);
      // 更新 ref 后立即请求
      sortStateRef.current = newSortState;
      fetchPluginList();
    },
    [fetchPluginList]
  );

  // 计算每个分类的插件数量
  const pluginCountByClassify = useMemo(() => {
    const countMap: Record<number, number> = {};
    allPluginList.forEach((plugin) => {
      if (plugin.classifyId) {
        countMap[plugin.classifyId] = (countMap[plugin.classifyId] || 0) + 1;
      }
    });
    return countMap;
  }, [allPluginList]);

  // 初始化
  useEffect(() => {
    fetchClassifyList();
    fetchPluginList();
  }, []);

  return {
    messageApi,
    contextHolder,
    pluginList,
    allPluginList,
    loading,
    searchName,
    setSearchName,
    classifyList,
    selectedClassify,
    pluginCountByClassify,
    handleClassifySelect,
    sortState,
    setSortState: handleSortChange,
    fetchPluginList,
    fetchAllPluginList,
    handleRefresh,
  };
}

export function useTemplateData(messageApi: ReturnType<typeof message.useMessage>[0]) {
  const [templateList, setTemplateList] = useState<NativePluginTemplateInfo[]>([]);
  const [templateClassify, setTemplateClassify] = useState<TemplateItem[]>(ClassifyList);
  const [templateLoading, setTemplateLoading] = useState(false);
  const [selectedClassify, setSelectedClassify] = useState("all");
  const [selectedTemplate, setSelectedTemplate] = useState<NativePluginTemplateInfo | null>(null);
  const [templateParams, setTemplateParams] = useState<NativePluginConfigFieldTemplate[]>([]);
  const [paramsLoading, setParamsLoading] = useState(false);

  // 获取模板列表
  const fetchTemplateList = useCallback(async () => {
    setTemplateLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.admin.native_plugin.template_list.post({});
      
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
      setSelectedClassify("all");
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
        const response = await client.api.admin.native_plugin.template_params.post({
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

  // 选择模板
  const handleTemplateSelect = useCallback(
    (template: NativePluginTemplateInfo) => {
      setSelectedTemplate(template);
      if (template.key) {
        fetchTemplateParams(template.key);
      }
    },
    [fetchTemplateParams]
  );

  // 重置
  const resetTemplateState = useCallback(() => {
    setSelectedClassify("all");
    setSelectedTemplate(null);
    setTemplateList([]);
    setTemplateParams([]);
  }, []);

  return {
    templateList,
    templateClassify,
    templateLoading,
    selectedClassify,
    setSelectedClassify,
    selectedTemplate,
    templateParams,
    paramsLoading,
    fetchTemplateList,
    handleTemplateSelect,
    resetTemplateState,
  };
}
