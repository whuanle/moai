/**
 * 团队自定义插件管理页面
 */
import { Input, Select, Button, Space, Spin } from "antd";
import { SearchOutlined, ReloadOutlined, ClearOutlined, PlusOutlined, ApiOutlined } from "@ant-design/icons";
import { useParams, useOutletContext } from "react-router";
import { useTeamPluginData } from "./hooks/useTeamPluginData";
import { useTeamPluginActions } from "./hooks/useTeamPluginActions";
import TeamPluginTable from "./components/TeamPluginTable";
import TeamClassifySidebar from "./components/TeamClassifySidebar";
import { TeamMcpPluginModal, TeamOpenApiImportModal, TeamOpenApiEditModal, TeamFunctionListModal } from "./components/TeamPluginModals";
import { PluginTypeObject } from "../../../apiClient/models";
import type { QueryTeamListQueryResponseItem, TeamRole } from "../../../apiClient/models";
import "./TeamPluginsPage.css";

interface OutletContextType {
  teamInfo: QueryTeamListQueryResponseItem | null;
  myRole: TeamRole;
  refreshTeamInfo: () => void;
}

export default function TeamPluginsPage() {
  const { id } = useParams();
  const teamId = id ? parseInt(id) : undefined;
  const context = useOutletContext<OutletContextType>();

  const {
    contextHolder: dataContextHolder,
    allPluginList, pluginList, loading, searchName, setSearchName,
    filterType, setFilterType, selectedClassify, classifyList,
    pluginCountByClassify, currentUser, sortState, setSortState,
    fetchPluginList, fetchAllPluginList, handleClassifySelect, handleResetFilters,
  } = useTeamPluginData(teamId);

  const actions = useTeamPluginActions(teamId, async () => {
    await fetchAllPluginList();
    fetchPluginList();
  });

  // 如果没有 teamId，显示加载状态
  if (!teamId) {
    return (
      <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: 200 }}>
        <Spin tip="加载中..." />
      </div>
    );
  }

  return (
    <>
      {dataContextHolder}
      {actions.contextHolder}
      <div className="team-plugin-layout">
        <TeamClassifySidebar
          classifyList={classifyList}
          allPluginCount={allPluginList.length}
          pluginCountByClassify={pluginCountByClassify}
          selectedClassify={selectedClassify}
          onSelect={handleClassifySelect}
        />
        <div className="team-plugin-content">
          <div className="team-plugin-toolbar">
            <div className="team-plugin-toolbar-left">
              <Input
                placeholder="搜索插件名称"
                allowClear
                prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
                value={searchName}
                onChange={(e) => setSearchName(e.target.value)}
                onPressEnter={() => fetchPluginList()}
                className="team-plugin-search-input"
              />
              <Select
                placeholder="插件类型"
                allowClear
                value={filterType}
                onChange={(v) => { setFilterType(v); fetchPluginList(); }}
                style={{ width: 120 }}
              >
                <Select.Option value={PluginTypeObject.Mcp}>MCP</Select.Option>
                <Select.Option value={PluginTypeObject.OpenApi}>OpenAPI</Select.Option>
              </Select>
              <Space size="small">
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

          <TeamPluginTable
            dataSource={pluginList}
            loading={loading}
            classifyList={classifyList}
            currentUserId={currentUser?.userId}
            sortState={sortState}
            onSortChange={setSortState}
            onView={actions.viewFunctions}
            onEdit={actions.openEditModal}
            onDelete={actions.deletePlugin}
          />
        </div>

        <TeamMcpPluginModal
          open={actions.mcpModalVisible}
          loading={actions.mcpLoading}
          form={actions.mcpForm}
          onOk={actions.submitMcp}
          onCancel={actions.closeMcpModal}
        />

        <TeamMcpPluginModal
          open={actions.editMcpModalVisible}
          loading={actions.editMcpLoading}
          form={actions.editMcpForm}
          onOk={actions.submitEditMcp}
          onCancel={actions.closeEditMcpModal}
          isEdit
        />

        <TeamOpenApiImportModal
          open={actions.openApiModalVisible}
          teamId={teamId}
          onSuccess={actions.onSuccess}
          onCancel={actions.closeOpenApiModal}
        />

        <TeamOpenApiEditModal
          open={actions.editOpenApiModalVisible}
          teamId={teamId}
          plugin={actions.currentPlugin}
          onSuccess={actions.onSuccess}
          onCancel={actions.closeEditOpenApiModal}
        />

        <TeamFunctionListModal
          open={actions.functionModalVisible}
          teamId={teamId}
          plugin={actions.currentPlugin}
          onCancel={actions.closeFunctionModal}
        />
      </div>
    </>
  );
}
