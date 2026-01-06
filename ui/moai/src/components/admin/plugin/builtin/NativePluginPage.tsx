/**
 * 内置插件管理页面
 */
import { useState, useCallback } from "react";
import { useNavigate } from "react-router";
import { Button, Space, Input, Typography } from "antd";
import { PlusOutlined, ReloadOutlined, SearchOutlined, ApiOutlined } from "@ant-design/icons";
import { GetApiClient } from "../../../ServiceClient";
import { proxyRequestError } from "../../../../helper/RequestError";
import type { NativePluginInfo, NativePluginTemplateInfo } from "../../../../apiClient/models";
import { PluginTypeObject } from "../../../../apiClient/models";
import CodeEditorModal from "../../../common/CodeEditorModal";
import SortPopover, { SortField } from "../../../common/SortPopover";
import {
  ClassifySidebar,
  PluginTable,
  CreatePluginDrawer,
  EditPluginDrawer,
  RunTestModal,
} from "./components";
import type { EditTarget, RunTarget } from "./components";
import { usePluginData } from "./hooks";
import "./NativePluginPage.css";

const sortFields: SortField[] = [
  { key: "templatePluginKey", label: "模板Key" },
  { key: "pluginName", label: "插件名称" },
  { key: "title", label: "标题" },
];

export default function NativePluginPage() {
  const {
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
    setSortState,
    fetchPluginList,
    fetchAllPluginList,
    handleRefresh,
  } = usePluginData();

  // 抽屉/模态框状态
  const [createDrawerVisible, setCreateDrawerVisible] = useState(false);
  const [editDrawerVisible, setEditDrawerVisible] = useState(false);
  const [editTarget, setEditTarget] = useState<EditTarget | null>(null);
  const [runModalVisible, setRunModalVisible] = useState(false);
  const [runTarget, setRunTarget] = useState<RunTarget | null>(null);

  // 代码编辑器状态
  const [codeEditorVisible, setCodeEditorVisible] = useState(false);
  const [codeEditorFieldKey, setCodeEditorFieldKey] = useState<string | null>(null);
  const [codeEditorInitialValue, setCodeEditorInitialValue] = useState("");
  const [codeEditorFormInstance, setCodeEditorFormInstance] = useState<any>(null);

  const navigate = useNavigate();

  // === 创建插件 ===
  const handleOpenCreateDrawer = useCallback(() => {
    setCreateDrawerVisible(true);
  }, []);

  const handleCloseCreateDrawer = useCallback(() => {
    setCreateDrawerVisible(false);
  }, []);

  const handleSwitchToFullPage = useCallback(() => {
    setCreateDrawerVisible(false);
    navigate("/app/plugin/builtin/create");
  }, [navigate]);

  const handleCreateSuccess = useCallback(async () => {
    await fetchAllPluginList();
    fetchPluginList();
  }, [fetchAllPluginList, fetchPluginList]);

  // === 编辑插件 ===
  const handleEdit = useCallback((record: NativePluginInfo) => {
    const isToolPlugin = record.pluginType === PluginTypeObject.ToolPlugin;
    setEditTarget(
      isToolPlugin
        ? { templatePluginKey: record.templatePluginKey || undefined }
        : { pluginId: record.pluginId || undefined }
    );
    setEditDrawerVisible(true);
  }, []);

  const handleCloseEditDrawer = useCallback(() => {
    setEditDrawerVisible(false);
    setEditTarget(null);
  }, []);

  const handleEditSuccess = useCallback(async () => {
    await fetchAllPluginList();
    fetchPluginList();
  }, [fetchAllPluginList, fetchPluginList]);

  // === 删除插件 ===
  const handleDelete = useCallback(
    async (pluginId: number) => {
      try {
        const client = GetApiClient();
        await client.api.admin.native_plugin.deletePath.delete({ pluginId });
        messageApi.success("内置插件删除成功");
        await fetchAllPluginList();
        fetchPluginList();
      } catch (error) {
        console.error("删除插件失败:", error);
        proxyRequestError(error, messageApi, "删除内置插件失败");
      }
    },
    [messageApi, fetchPluginList, fetchAllPluginList]
  );

  // === 运行测试 ===
  const handleOpenRunModal = useCallback((record: NativePluginInfo) => {
    setRunTarget({
      pluginId: record.pluginId || undefined,
      pluginName: record.pluginName || "",
      templatePluginKey: record.templatePluginKey || undefined,
    });
    setRunModalVisible(true);
  }, []);

  const handleRunTemplate = useCallback((template: NativePluginTemplateInfo) => {
    setRunTarget({
      pluginName: template.name || "",
      templatePluginKey: template.key || undefined,
    });
    setRunModalVisible(true);
  }, []);

  const handleCloseRunModal = useCallback(() => {
    setRunModalVisible(false);
    setRunTarget(null);
  }, []);

  // === 代码编辑器 ===
  const handleOpenCodeEditor = useCallback((fieldKey: string, currentValue: string, formInstance: any) => {
    setCodeEditorFieldKey(fieldKey);
    setCodeEditorInitialValue(currentValue || "");
    setCodeEditorFormInstance(formInstance);
    setCodeEditorVisible(true);
  }, []);

  const handleCloseCodeEditor = useCallback(() => {
    setCodeEditorVisible(false);
    setCodeEditorFieldKey(null);
    setCodeEditorInitialValue("");
    setCodeEditorFormInstance(null);
  }, []);

  const handleConfirmCodeEditor = useCallback(
    (value: string) => {
      if (codeEditorFieldKey && codeEditorFormInstance) {
        codeEditorFormInstance.setFieldsValue({ [codeEditorFieldKey]: value });
      }
      handleCloseCodeEditor();
    },
    [codeEditorFieldKey, codeEditorFormInstance, handleCloseCodeEditor]
  );

  return (
    <>
      {contextHolder}
      <div className="native-plugin-layout">
        <ClassifySidebar
          classifyList={classifyList}
          allPluginCount={allPluginList.length}
          pluginCountByClassify={pluginCountByClassify}
          selectedClassify={selectedClassify}
          onSelect={handleClassifySelect}
        />
        <div className="plugin-content">
          <div className="plugin-toolbar">
            <div className="plugin-toolbar-left">
              <Input
                placeholder="搜索插件名称、模板Key或描述..."
                allowClear
                prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                value={searchName}
                onChange={(e) => setSearchName(e.target.value)}
                onPressEnter={() => handleRefresh()}
                className="plugin-search-input"
              />·
              <Space size="middle">
                <Button icon={<ReloadOutlined />} onClick={handleRefresh} loading={loading}>
                  刷新
                </Button>
                <SortPopover fields={sortFields} value={sortState} onChange={setSortState} />
                <Button type="primary" icon={<PlusOutlined />} onClick={handleOpenCreateDrawer}>
                  新增插件
                </Button>
              </Space>
            </div>
          </div>
          <PluginTable
            dataSource={pluginList}
            loading={loading}
            onEdit={handleEdit}
            onRun={handleOpenRunModal}
            onDelete={handleDelete}
            onRunTemplate={handleRunTemplate}
          />
        </div>
      </div>

      <CreatePluginDrawer
        open={createDrawerVisible}
        onClose={handleCloseCreateDrawer}
        onSuccess={handleCreateSuccess}
        onSwitchToFullPage={handleSwitchToFullPage}
        onOpenCodeEditor={handleOpenCodeEditor}
      />

      <EditPluginDrawer
        open={editDrawerVisible}
        target={editTarget}
        onClose={handleCloseEditDrawer}
        onSuccess={handleEditSuccess}
        onOpenCodeEditor={handleOpenCodeEditor}
      />

      <RunTestModal
        open={runModalVisible}
        target={runTarget}
        onClose={handleCloseRunModal}
      />

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
    </>
  );
}
