/**
 * 团队自定义插件数据管理 Hook
 */
import { useState, useCallback, useEffect, useRef, useMemo } from "react";
import { message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type {
  PluginBaseInfoItem,
  PluginClassifyItem,
  PluginType,
} from "../../../../apiClient/models";
import { PluginTypeObject } from "../../../../apiClient/models";

export interface TableSortState {
  field: string | null;
  order: 'ascend' | 'descend' | null;
}

export function useTeamPluginData(teamId: number | undefined) {
  const [messageApi, contextHolder] = message.useMessage();

  const [allPluginList, setAllPluginList] = useState<PluginBaseInfoItem[]>([]);
  const [pluginList, setPluginList] = useState<PluginBaseInfoItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [searchName, setSearchName] = useState("");
  const [filterType, setFilterType] = useState<PluginType | undefined>(undefined);
  const [selectedClassify, setSelectedClassify] = useState<number | "all">("all");
  const [classifyList, setClassifyList] = useState<PluginClassifyItem[]>([]);
  const [currentUser, setCurrentUser] = useState<{ userId?: number } | null>(null);
  const [sortState, setSortState] = useState<TableSortState>({ field: null, order: null });

  const searchNameRef = useRef(searchName);
  const filterTypeRef = useRef(filterType);
  const selectedClassifyRef = useRef(selectedClassify);

  searchNameRef.current = searchName;
  filterTypeRef.current = filterType;
  selectedClassifyRef.current = selectedClassify;

  const pluginCountByClassify = useMemo(() => {
    const countMap: Record<number, number> = {};
    allPluginList.forEach((plugin) => {
      if (plugin.classifyId != null) {
        countMap[plugin.classifyId] = (countMap[plugin.classifyId] || 0) + 1;
      }
    });
    return countMap;
  }, [allPluginList]);

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

  const fetchAllPluginList = useCallback(async () => {
    if (!teamId) return;
    try {
      const client = GetApiClient();
      const response = await client.api.team.plugin.list.post({ teamId });
      if (response?.items) {
        setAllPluginList(response.items);
      }
    } catch (error) {
      console.error("获取全部插件列表失败:", error);
    }
  }, [teamId]);

  const fetchPluginList = useCallback(async () => {
    if (!teamId) return;
    setLoading(true);
    try {
      const client = GetApiClient();
      const response = await client.api.team.plugin.list.post({
        teamId,
        name: searchNameRef.current || undefined,
        type: filterTypeRef.current
          ? (filterTypeRef.current as typeof PluginTypeObject[keyof typeof PluginTypeObject])
          : undefined,
      });

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
  }, [teamId, messageApi]);

  const handleClassifySelect = useCallback((key: number | "all") => {
    setSelectedClassify(key);
  }, []);

  const handleResetFilters = useCallback(() => {
    setSearchName("");
    setFilterType(undefined);
    setSelectedClassify("all");
    setSortState({ field: null, order: null });
  }, []);

  const handleSortChange = useCallback(
    (newSortState: TableSortState) => {
      setSortState(newSortState);
      fetchPluginList();
    },
    [fetchPluginList]
  );

  useEffect(() => {
    if (teamId) {
      fetchCurrentUser();
      fetchClassifyList();
      fetchAllPluginList();
      fetchPluginList();
    }
  }, [teamId]);

  useEffect(() => {
    if (teamId) {
      fetchPluginList();
    }
  }, [selectedClassify, teamId]);

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
