/**
 * 自定义插件管理页面
 */
import { Input, Select, Button, Space } from "antd";
import { SearchOutlined, ReloadOutlined, ClearOutlined, PlusOutlined, ApiOutlined } from "@ant-design/icons";
import { useCustomPluginData } from "./hooks/useCustomPluginData";
import { usePluginActions } from "./hooks/usePluginActions";
import PluginTable from "./components/PluginTable";
import ClassifySidebar from "./components/ClassifySidebar";
import { McpPluginModal, OpenApiImportModal, OpenApiEditModal, FunctionListModal } from "./components/PluginModals";
import { PluginTypeObject } from "../../../../apiClient/models";
import "./PluginCustomPage.css";

export default function PluginCustomPage() {
  // 数据管理
  const {
    contextHolder: dataContextHolder,
    allPluginList, pluginList, loading, searchName, setSearchName,
    filterType, setFilterType, selectedClassify, classifyList,
    pluginCountByClassify, currentUser, fetchPluginList, fetchAllPluginList,
    handleClassifySelect, handleResetFilters,
  } = useCustomPluginData();

  // 操作管理
  const actions = usePluginActions(async () => {
    await fetchAllPluginList();
    fetchPluginList();
  });

  return (
    <>
      {dataContextHolder}
      {actions.contextHolder}
      <div className="plugin-custom-layout">
        <ClassifySidebar
          classifyList={classifyList}
          allPluginCount={allPluginList.length}
          pluginCountByClassify={pluginCountByClassify}
          selectedClassify={selectedClassify}
          onSelect={handleClassifySelect}
        />
        <div className="plugin-content">
          {/* 筛选工具栏 */}
          <div className="plugin-toolbar">
            <div className="plugin-toolbar-left">
              <Input
                placeholder="搜索插件名称"
                allowClear
                prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                value={searchName}
                onChange={(e) => setSearchName(e.target.value)}
                onPressEnter={() => fetchPluginList()}
                className="plugin-search-input"
              />
              <Select
                placeholder="插件类型"
                allowClear
                value={filterType}
                onChange={(v) => { setFilterType(v); fetchPluginList(); }}
                style={{ width: 130 }}
              >
                <Select.Option value={PluginTypeObject.Mcp}>MCP</Select.Option>
                <Select.Option value={PluginTypeObject.OpenApi}>OpenAPI</Select.Option>
              </Select>
              <Space size="middle">
                <Button icon={<ReloadOutlined />} onClick={fetchPluginList} loading={loading}>
                  刷新
                </Button>
                <Button icon={<ClearOutlined />} onClick={handleResetFilters}>
                  重置
                </Button>
              </Space>
            </div>
            <Space>
              <Button type="primary" icon={<PlusOutlined />} onClick={actions.openMcpModal}>
                导入 MCP
              </Button>
              <Button type="primary" icon={<ApiOutlined />} onClick={actions.openOpenApiModal}>
                导入 OpenAPI
              </Button>
            </Space>
          </div>

          {/* 数据表格 */}
          <PluginTable
            dataSource={pluginList}
            loading={loading}
            classifyList={classifyList}
            currentUserId={currentUser?.userId}
            onView={actions.viewFunctions}
            onEdit={actions.openEditModal}
            onDelete={actions.deletePlugin}
          />
        </div>

        {/* MCP 模态框 */}
        <McpPluginModal
          open={actions.mcpModalVisible}
          loading={actions.mcpLoading}
          form={actions.mcpForm}
          onOk={actions.submitMcp}
          onCancel={actions.closeMcpModal}
        />

        <McpPluginModal
          open={actions.editMcpModalVisible}
          loading={actions.editMcpLoading}
          form={actions.editMcpForm}
          onOk={actions.submitEditMcp}
          onCancel={actions.closeEditMcpModal}
          isEdit
        />

        {/* OpenAPI 模态框 - 自管理状态 */}
        <OpenApiImportModal
          open={actions.openApiModalVisible}
          onSuccess={actions.onSuccess}
          onCancel={actions.closeOpenApiModal}
        />

        <OpenApiEditModal
          open={actions.editOpenApiModalVisible}
          plugin={actions.currentPlugin}
          onSuccess={actions.onSuccess}
          onCancel={actions.closeEditOpenApiModal}
        />

        {/* 函数列表模态框 */}
        <FunctionListModal
          open={actions.functionModalVisible}
          plugin={actions.currentPlugin}
          onCancel={actions.closeFunctionModal}
        />
      </div>
    </>
  );
}
