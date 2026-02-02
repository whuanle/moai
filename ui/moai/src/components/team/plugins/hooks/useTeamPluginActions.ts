/**
 * 团队插件操作 Hook
 */
import { useState, useCallback } from "react";
import { Form, message } from "antd";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError, proxyFormRequestError } from "../../../../helper/RequestError";
import type {
  PluginBaseInfoItem,
  KeyValueString,
} from "../../../../apiClient/models";
import { PluginTypeObject } from "../../../../apiClient/models";

interface KeyValueItem {
  key: string;
  value: string;
}

export function useTeamPluginActions(teamId: number | undefined, onSuccess: () => void) {
  const [messageApi, contextHolder] = message.useMessage();

  const [mcpModalVisible, setMcpModalVisible] = useState(false);
  const [mcpForm] = Form.useForm();
  const [mcpLoading, setMcpLoading] = useState(false);

  const [editMcpModalVisible, setEditMcpModalVisible] = useState(false);
  const [editMcpForm] = Form.useForm();
  const [editMcpLoading, setEditMcpLoading] = useState(false);

  const [openApiModalVisible, setOpenApiModalVisible] = useState(false);
  const [editOpenApiModalVisible, setEditOpenApiModalVisible] = useState(false);

  const [functionModalVisible, setFunctionModalVisible] = useState(false);
  const [currentPlugin, setCurrentPlugin] = useState<PluginBaseInfoItem | null>(null);

  const processKeyValueArray = useCallback((values: KeyValueItem[]): KeyValueString[] => {
    return values?.filter((item) => item.key && item.value).map((item) => ({ key: item.key, value: item.value })) || [];
  }, []);

  const buildHeaderWithTransportMode = useCallback((headerValues: KeyValueItem[], httpTransportMode?: string): KeyValueString[] => {
    const headers = processKeyValueArray(headerValues);
    if (httpTransportMode) {
      headers.push({ key: ".HttpTransportMode", value: httpTransportMode });
    }
    return headers;
  }, [processKeyValueArray]);

  const extractTransportModeFromHeader = useCallback((headers: KeyValueString[]): { transportMode?: string; filteredHeaders: KeyValueItem[] } => {
    const transportModeItem = headers.find(item => item.key === ".HttpTransportMode");
    const filteredHeaders = headers.filter(item => item.key !== ".HttpTransportMode").map(item => ({ key: item.key || "", value: item.value || "" }));
    return {
      transportMode: transportModeItem?.value || undefined,
      filteredHeaders,
    };
  }, []);

  const openMcpModal = useCallback(() => {
    mcpForm.resetFields();
    setMcpModalVisible(true);
  }, [mcpForm]);

  const closeMcpModal = useCallback(() => {
    setMcpModalVisible(false);
    mcpForm.resetFields();
  }, [mcpForm]);

  const submitMcp = useCallback(async () => {
    if (!teamId) return;
    try {
      const values = await mcpForm.validateFields();
      setMcpLoading(true);

      const client = GetApiClient();
      const response = await client.api.team.plugin.import_mcp.post({
        teamId,
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: buildHeaderWithTransportMode(values.header, values.httpTransportMode),
        query: processKeyValueArray(values.query),
        classifyId: values.classifyId,
      });

      if (response?.value) {
        messageApi.success("MCP插件导入成功");
        closeMcpModal();
        onSuccess();
      }
    } catch (error) {
      console.error("导入MCP插件失败:", error);
      proxyFormRequestError(error, messageApi, mcpForm, "导入MCP插件失败");
    } finally {
      setMcpLoading(false);
    }
  }, [teamId, mcpForm, buildHeaderWithTransportMode, processKeyValueArray, messageApi, closeMcpModal, onSuccess]);

  const openEditModal = useCallback(async (record: PluginBaseInfoItem) => {
    if (!teamId) return;
    setCurrentPlugin(record);

    if (record.type === PluginTypeObject.OpenApi) {
      setEditOpenApiModalVisible(true);
    } else {
      try {
        const client = GetApiClient();
        const response = await client.api.team.plugin.detail.post({ teamId, pluginId: record.pluginId });

        if (response) {
          const { transportMode, filteredHeaders } = extractTransportModeFromHeader(response.header || []);
          editMcpForm.setFieldsValue({
            name: response.pluginName,
            title: response.title,
            serverUrl: response.server,
            description: response.description,
            classifyId: response.classifyId,
            httpTransportMode: transportMode,
            header: filteredHeaders,
            query: response.query?.map((item) => ({ key: item.key, value: item.value })) || [],
          });
          setEditMcpModalVisible(true);
        }
      } catch (error) {
        console.error("获取插件详情失败:", error);
        proxyRequestError(error, messageApi, "获取插件详情失败");
      }
    }
  }, [teamId, editMcpForm, extractTransportModeFromHeader, messageApi]);

  const closeEditMcpModal = useCallback(() => {
    setEditMcpModalVisible(false);
    editMcpForm.resetFields();
    setCurrentPlugin(null);
  }, [editMcpForm]);

  const submitEditMcp = useCallback(async () => {
    if (!teamId) return;
    try {
      const values = await editMcpForm.validateFields();
      setEditMcpLoading(true);

      const client = GetApiClient();
      await client.api.team.plugin.update_mcp.post({
        teamId,
        pluginId: currentPlugin?.pluginId,
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: buildHeaderWithTransportMode(values.header, values.httpTransportMode),
        query: processKeyValueArray(values.query),
        classifyId: values.classifyId,
      });

      messageApi.success("插件更新成功");
      closeEditMcpModal();
      onSuccess();
    } catch (error) {
      console.error("更新插件失败:", error);
      proxyFormRequestError(error, messageApi, editMcpForm, "更新插件失败");
    } finally {
      setEditMcpLoading(false);
    }
  }, [teamId, editMcpForm, currentPlugin, buildHeaderWithTransportMode, processKeyValueArray, messageApi, closeEditMcpModal, onSuccess]);

  const openOpenApiModal = useCallback(() => setOpenApiModalVisible(true), []);
  const closeOpenApiModal = useCallback(() => setOpenApiModalVisible(false), []);
  const closeEditOpenApiModal = useCallback(() => {
    setEditOpenApiModalVisible(false);
    setCurrentPlugin(null);
  }, []);

  const deletePlugin = useCallback(async (pluginId: number) => {
    if (!teamId) return;
    try {
      const client = GetApiClient();
      await client.api.team.plugin.deletePath.delete({ teamId, pluginId });
      messageApi.success("插件删除成功");
      onSuccess();
    } catch (error) {
      console.error("删除插件失败:", error);
      proxyRequestError(error, messageApi, "删除插件失败");
    }
  }, [teamId, messageApi, onSuccess]);

  const viewFunctions = useCallback((record: PluginBaseInfoItem) => {
    setCurrentPlugin(record);
    setFunctionModalVisible(true);
  }, []);

  const closeFunctionModal = useCallback(() => {
    setFunctionModalVisible(false);
    setCurrentPlugin(null);
  }, []);

  return {
    contextHolder,
    mcpModalVisible, mcpForm, mcpLoading, openMcpModal, closeMcpModal, submitMcp,
    editMcpModalVisible, editMcpForm, editMcpLoading, closeEditMcpModal, submitEditMcp,
    openApiModalVisible, openOpenApiModal, closeOpenApiModal,
    editOpenApiModalVisible, closeEditOpenApiModal,
    currentPlugin, openEditModal, deletePlugin,
    functionModalVisible, viewFunctions, closeFunctionModal,
    onSuccess,
  };
}
