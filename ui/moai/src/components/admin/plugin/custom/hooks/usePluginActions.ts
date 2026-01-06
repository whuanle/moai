/**
 * 插件操作 Hook - 处理增删改查
 */
import { useState, useCallback } from "react";
import { Form, message } from "antd";
import { GetApiClient } from "../../../../ServiceClient";
import { proxyRequestError, proxyFormRequestError } from "../../../../../helper/RequestError";
import type {
  PluginBaseInfoItem,
  ImportMcpServerPluginCommand,
  UpdateMcpServerPluginCommand,
  KeyValueString,
} from "../../../../../apiClient/models";
import { PluginTypeObject } from "../../../../../apiClient/models";

interface KeyValueItem {
  key: string;
  value: string;
}

export function usePluginActions(onSuccess: () => void) {
  const [messageApi, contextHolder] = message.useMessage();

  // MCP 模态框状态
  const [mcpModalVisible, setMcpModalVisible] = useState(false);
  const [mcpForm] = Form.useForm();
  const [mcpLoading, setMcpLoading] = useState(false);

  // 编辑 MCP 模态框状态
  const [editMcpModalVisible, setEditMcpModalVisible] = useState(false);
  const [editMcpForm] = Form.useForm();
  const [editMcpLoading, setEditMcpLoading] = useState(false);

  // OpenAPI 模态框状态
  const [openApiModalVisible, setOpenApiModalVisible] = useState(false);
  const [editOpenApiModalVisible, setEditOpenApiModalVisible] = useState(false);

  // 函数列表模态框状态
  const [functionModalVisible, setFunctionModalVisible] = useState(false);

  // 当前操作的插件
  const [currentPlugin, setCurrentPlugin] = useState<PluginBaseInfoItem | null>(null);

  // 工具函数
  const processKeyValueArray = useCallback((values: KeyValueItem[]): KeyValueString[] => {
    return values?.filter((item) => item.key && item.value).map((item) => ({ key: item.key, value: item.value })) || [];
  }, []);

  // 打开导入 MCP 模态框
  const openMcpModal = useCallback(() => {
    mcpForm.resetFields();
    setMcpModalVisible(true);
  }, [mcpForm]);

  // 关闭导入 MCP 模态框
  const closeMcpModal = useCallback(() => {
    setMcpModalVisible(false);
    mcpForm.resetFields();
  }, [mcpForm]);

  // 提交导入 MCP
  const submitMcp = useCallback(async () => {
    try {
      const values = await mcpForm.validateFields();
      setMcpLoading(true);

      const requestData: ImportMcpServerPluginCommand = {
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: processKeyValueArray(values.header),
        query: processKeyValueArray(values.query),
        isPublic: values.isPublic,
        classifyId: values.classifyId,
      };

      const client = GetApiClient();
      const response = await client.api.admin.custom_plugin.import_mcp.post(requestData);

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
  }, [mcpForm, processKeyValueArray, messageApi, closeMcpModal, onSuccess]);

  // 打开编辑模态框
  const openEditModal = useCallback(async (record: PluginBaseInfoItem) => {
    setCurrentPlugin(record);

    if (record.type === PluginTypeObject.OpenApi) {
      setEditOpenApiModalVisible(true);
    } else {
      // MCP 类型需要先获取详情填充表单
      try {
        const client = GetApiClient();
        const response = await client.api.admin.custom_plugin.plugin_detail.post({ pluginId: record.pluginId });

        if (response) {
          editMcpForm.setFieldsValue({
            name: response.pluginName,
            title: response.title,
            serverUrl: response.server,
            description: response.description,
            isPublic: response.isPublic,
            classifyId: response.classifyId,
            header: response.header?.map((item) => ({ key: item.key, value: item.value })) || [],
            query: response.query?.map((item) => ({ key: item.key, value: item.value })) || [],
          });
          setEditMcpModalVisible(true);
        }
      } catch (error) {
        console.error("获取插件详情失败:", error);
        proxyRequestError(error, messageApi, "获取插件详情失败");
      }
    }
  }, [editMcpForm, messageApi]);

  // 关闭编辑 MCP 模态框
  const closeEditMcpModal = useCallback(() => {
    setEditMcpModalVisible(false);
    editMcpForm.resetFields();
    setCurrentPlugin(null);
  }, [editMcpForm]);

  // 提交编辑 MCP
  const submitEditMcp = useCallback(async () => {
    try {
      const values = await editMcpForm.validateFields();
      setEditMcpLoading(true);

      const requestData: UpdateMcpServerPluginCommand = {
        pluginId: currentPlugin?.pluginId,
        name: values.name,
        title: values.title,
        description: values.description,
        serverUrl: values.serverUrl,
        header: processKeyValueArray(values.header),
        query: processKeyValueArray(values.query),
        isPublic: values.isPublic,
        classifyId: values.classifyId,
      };

      const client = GetApiClient();
      await client.api.admin.custom_plugin.update_mcp.post(requestData);

      messageApi.success("插件更新成功");
      closeEditMcpModal();
      onSuccess();
    } catch (error) {
      console.error("更新插件失败:", error);
      proxyFormRequestError(error, messageApi, editMcpForm, "更新插件失败");
    } finally {
      setEditMcpLoading(false);
    }
  }, [editMcpForm, currentPlugin, processKeyValueArray, messageApi, closeEditMcpModal, onSuccess]);

  // OpenAPI 模态框操作
  const openOpenApiModal = useCallback(() => setOpenApiModalVisible(true), []);
  const closeOpenApiModal = useCallback(() => setOpenApiModalVisible(false), []);
  const closeEditOpenApiModal = useCallback(() => {
    setEditOpenApiModalVisible(false);
    setCurrentPlugin(null);
  }, []);

  // 删除插件
  const deletePlugin = useCallback(async (pluginId: number) => {
    try {
      const client = GetApiClient();
      await client.api.admin.custom_plugin.delete_plugin.delete({ pluginId });
      messageApi.success("插件删除成功");
      onSuccess();
    } catch (error) {
      console.error("删除插件失败:", error);
      proxyRequestError(error, messageApi, "删除插件失败");
    }
  }, [messageApi, onSuccess]);

  // 查看函数列表
  const viewFunctions = useCallback((record: PluginBaseInfoItem) => {
    setCurrentPlugin(record);
    setFunctionModalVisible(true);
  }, []);

  // 关闭函数列表模态框
  const closeFunctionModal = useCallback(() => {
    setFunctionModalVisible(false);
    setCurrentPlugin(null);
  }, []);

  return {
    contextHolder,
    // MCP 导入
    mcpModalVisible, mcpForm, mcpLoading, openMcpModal, closeMcpModal, submitMcp,
    // MCP 编辑
    editMcpModalVisible, editMcpForm, editMcpLoading, closeEditMcpModal, submitEditMcp,
    // OpenAPI
    openApiModalVisible, openOpenApiModal, closeOpenApiModal,
    editOpenApiModalVisible, closeEditOpenApiModal,
    // 通用操作
    currentPlugin, openEditModal, deletePlugin,
    // 函数列表
    functionModalVisible, viewFunctions, closeFunctionModal,
    // 刷新回调
    onSuccess,
  };
}
