/**
 * 自定义插件数据管理 Hook
 */
import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../../ServiceClient";
import { proxyRequestError } from "../../../../../helper/RequestError";
import type {
  PluginBaseInfoItem,
  PluginClassifyItem,
  QueryCustomPluginBaseListCommand,
  PluginTypeObject,
  KeyValueBool,
} from "../../../../../apiClient/models";

// 排序状态类型
export interface TableSortState {
  field: string | null;
  order: 'ascend' | 'descend' | null;
}

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

export function useCustomPluginData() {
  const [messageApi, contextHolder] = message.useMessage();

  // 状态
  const [allPluginList, setAllPluginList] = useState<PluginBaseInfoItem[]>([]);
  const [pluginList, setPluginList] = useState<PluginBaseInfoItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchName, setSearchName] = useState("");
  const [filterType, setFilterType] = useState<string | undefined>(undefined);
  const [selectedClassify, setSelectedClassify] = useState<number | "all">("all");
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);
  const [currentUser, setCurrentUser] = useState<{ userId?: number } | null>(null);
  const [sortState, setSortState] = useState<TableSortState>({ field: null, order: null });

  // 使用 ref 存储最新值
  const searchNameRef = useRef(searchName);
  const filterTypeRef = useRef(filterType);
  const selectedClassifyRef = useRef(selectedClassify);
  const sortStateRef = useRef(sortState);

  searchNameRef.current = searchName;
  filterTypeRef.current = filterType;
  selectedClassifyRef.current = selectedClassify;
  sortStateRef.current = sortState;

  // 计算每个分类的插件数量
  const pluginCountByClassify = useMemo(() => {
    const countMap: Record<number, number> = {};
    allPluginList.forEach((plugin) => {
      if (plugin.classifyId != null) {
        countMap[plugin.classifyId] = (countMap[plugin.classifyId] || 0) + 1;
      }
    });
    return countMap;
  }, [allPluginList]);

  // 获取当前用户信息
  const fetchCurrentUser = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.common.userinfo.get();
      if (response) {
        setCurrentUser({ userId: response.userId ?? undefined });
      }
    } catch (error) {
      console.log("Fetch current user error:", error);
    }
  }, []);

  // 获取分类列表
  const fetchClassifyList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.plugin.classify_list.get();
      if (response?.items) {
        setClassifyList(response.items || []);
      }
    } catch (error) {
      console.error("获取分类列表失败:", error);
      proxyRequestError(error, messageApi, "获取分类列表失败");
    }
  }, [messageApi]);

  // 获取全部插件列表（用于统计）
  const fetchAllPluginList = useCallback(async () => {
    try {
      const client = GetApiClient();
      const response = await client.api.admin.custom_plugin.plugin_list.post({});
      if (response?.items) {
        setAllPluginList(response.items);
      }
    } catch (error) {
      console.error("获取全部插件列表失败:", error);
    }
  }, []);

  // 获取插件列表（带筛选和排序）
  const fetchPluginList = useCallback(async () => {
    setLoading(true);
    try {
      const client = GetApiClient();
      const requestData: QueryCustomPluginBaseListCommand = {
        name: searchNameRef.current || undefined,
        type: filterTypeRef.current
          ? (filterTypeRef.current as typeof PluginTypeObject[keyof typeof PluginTypeObject])
          : undefined,
        orderByFields: buildOrderByFields(sortStateRef.current),
      };
      const response = await client.api.admin.custom_plugin.plugin_list.post(requestData);

      if (response?.items) {
        let filteredItems = response.items;
        if (selectedClassifyRef.current !== "all") {
          filteredItems = filteredItems.filter(
            (item) => item.classifyId === selectedClassifyRef.current
          );
        }
        setPluginList(filteredItems);
      }
    } catch (error) {
      console.error("获取插件列表失败:", error);
      proxyRequestError(error, messageApi, "获取插件列表失败");
    } finally {
      setLoading(false);
    }
  }, [messageApi]);

  // 处理分类选择
  const handleClassifySelect = useCallback((key: number | "all") => {
    setSelectedClassify(key);
  }, []);

  // 重置筛选条件
  const handleResetFilters = useCallback(() => {
    setSearchName("");
    setFilterType(undefined);
    setSelectedClassify("all");
    setSortState({ field: null, order: null });
  }, []);

  // 处理排序变化
  const handleSortChange = useCallback(
    (newSortState: TableSortState) => {
      setSortState(newSortState);
      sortStateRef.current = newSortState;
      fetchPluginList();
    },
    [fetchPluginList]
  );

  // 初始化
  useEffect(() => {
    fetchCurrentUser();
    fetchClassifyList();
    fetchAllPluginList();
    fetchPluginList();
  }, []);

  // 分类变化时重新获取列表
  useEffect(() => {
    fetchPluginList();
  }, [selectedClassify]);

  return {
    messageApi,
    contextHolder,
    allPluginList,
    pluginList,
    loading,
    searchName,
    setSearchName,
    filterType,
    setFilterType,
    selectedClassify,
    classifyList,
    pluginCountByClassify,
    currentUser,
    sortState,
    setSortState: handleSortChange,
    fetchPluginList,
    fetchAllPluginList,
    handleClassifySelect,
    handleResetFilters,
  };
}
