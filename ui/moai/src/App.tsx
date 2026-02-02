import { useEffect, useState } from "react";
import { useNavigate, Routes, Route, Navigate } from "react-router";
import { Layout, message } from "antd";
import "./App.css";
import {
  CheckToken,
  RefreshServerInfo,
  GetUserDetailInfo,
} from "./InitService";
import { GetApiClient } from "./components/ServiceClient";
import useAppStore from "./stateshare/store";
import AppHeader from "./AppHeader";

// 页面组件导入
import OAuthPage from "./components/admin/oauth/OAuthPage";
import UserManagerPage from "./components/admin/usermanager/UserManagerPage";
import UserSetting from "./components/user/UserSetting";
import BindOAuth from "./components/user/BindOAuth";
import AiModelPage from "./components/admin/aimodel/AiModelPage";
import ModelAuthorizationPage from "./components/admin/aimodel/ModelAuthorizationPage";
import PromptClassEditPage from "./components/prompt/PromptClassPage";
import PromptListPage from "./components/prompt/PromptListPage";
import PromptCreatePage from "./components/prompt/PromptCreatePage";
import PromptEditPage from "./components/prompt/PromptEditPage";
import PluginLayout from "./components/admin/plugin/PluginLayout";
import WikiListPage from "./components/wiki/WikiListPage";
import WikiLayout from "./components/wiki/WikiLayout";
import WikiSettings from "./components/wiki/WikiSettings";
import WikiDocument from "./components/wiki/WikiDocument";
import DocumentEmbedding from "./components/wiki/DocumentEmbedding";
import WikiSearch from "./components/wiki/WikiSearch";
import CrawlerListPage from "./components/wiki/plugins/crawler/CrawlerListPage";
import CrawlerDetailPage from "./components/wiki/plugins/crawler/CrawlerDetailPage";
import FeishuListPage from "./components/wiki/plugins/feishu/FeishuListPage";
import FeishuDetailPage from "./components/wiki/plugins/feishu/FeishuDetailPage";
import BatchListPage from "./components/wiki/BatchListPage";
import OpenApiListPage from "./components/wiki/plugins/openapi/OpenApiListPage";
import PaddleocrPage from "./components/wiki/plugins/paddleocr/PaddleocrPage";
import TeamListPage from "./components/team/TeamListPage";
import TeamLayout from "./components/team/TeamLayout";
import TeamMembers from "./components/team/TeamMembers";
import TeamSettings from "./components/team/TeamSettings";
import ApplicationPage from "./components/application/ApplicationPage";
import ApplicationClassifyPage from "./components/application/ApplicationClassifyPage";
import ApplicationListPage from "./components/application/ApplicationListPage";
import AppChatPage from "./components/application/Apps/chatapp/AppChatPage";
import UserLayout from "./components/user/UserLayout";
import TeamWikiWrapper from "./components/team/TeamWikiWrapper";
import TeamApps from "./components/team/apps/TeamApps";
import TeamApplicationListPage from "./components/team/apps/TeamApplicationListPage";
import TeamIntegration from "./components/team/TeamIntegration";
import McpConfigPage from "./components/wiki/McpConfigPage";
import PromptViewPage from "./components/prompt/PromptViewPage";
import PluginClassPage from "./components/admin/plugin/classify/PluginClassPage";
import PluginAuthorizationPage from "./components/admin/plugin/authorization/PluginAuthorizationPage";
import NativePluginPage from "./components/admin/plugin/builtin/NativePluginPage";
import CreatePluginPage from "./components/admin/plugin/builtin/CreatePluginPage";
import PluginCustomPage from "./components/admin/plugin/custom/PluginCustomPage";
import AppConfigCommon from "./components/team/apps/chatapp/ChatAppConfig";
import { WorkflowEditor } from "./components/team/apps/workflow";
import { TeamPluginsPage } from "./components/team/plugins";

const { Content } = Layout;

function App() {
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const [isAdmin, setIsAdmin] = useState(false);

  useEffect(() => {
    const client = GetApiClient();
    const fetchData = async () => {
      await RefreshServerInfo(client);
      const isVerify = await CheckToken();
      if (!isVerify) {
        messageApi.success("身份已失效，正在重定向到登录页面");
        setTimeout(() => {
          navigate("/login");
        }, 1000);
      } else {
        const detailInfo = await GetUserDetailInfo();
        if (detailInfo) {
          useAppStore.getState().setUserDetailInfo(detailInfo);
          setIsAdmin(detailInfo.isAdmin === true);
        }
      }
    };
    fetchData();

    // 每分钟检查一次 token
    const checkTokenInterval = setInterval(async () => {
      const isTokenValid = await CheckToken();
      if (!isTokenValid) {
        messageApi.error("Token已过期，请重新登录");
        useAppStore.getState().clearUserInfo();
        useAppStore.getState().clearUserDetailInfo();
        navigate("/login");
      }
    }, 1000 * 60);

    return () => {
      clearInterval(checkTokenInterval);
    };
  }, []);

  return (
    <>
      {contextHolder}
      <Layout className="app-layout">
        <AppHeader isAdmin={isAdmin} />
        <Content className="app-content">
          <Routes>
            <Route index element={<Navigate to="application" replace />} />
            <Route path="index" element={<Navigate to="/app/application" replace />} />
            
            {/* 应用 */}
            <Route path="application">
              <Route index element={<ApplicationPage />} />
              <Route path="classify" element={<ApplicationClassifyPage />} />
              <Route path="chat/:appId" element={<AppChatPage />} />
            </Route>
            
            {/* 知识库 */}
            <Route path="wiki">
              <Route index element={<Navigate to="list" replace />} />
              <Route path="list" element={<WikiListPage />} />
              <Route path=":id" element={<WikiLayout />}>
                <Route path="settings" element={<WikiSettings />} />
                <Route index element={<WikiDocument />} />
                <Route path="document" element={<WikiDocument />} />
                <Route path="document/:documentId/embedding" element={<DocumentEmbedding />} />
                <Route path="search" element={<WikiSearch />} />
                <Route path="plugin/crawler/:configId" element={<CrawlerDetailPage />} />
                <Route path="plugin/crawler" element={<CrawlerListPage />} />
                <Route path="plugin/feishu/:configId" element={<FeishuDetailPage />} />
                <Route path="plugin/feishu" element={<FeishuListPage />} />
                <Route path="plugin/openapi" element={<OpenApiListPage />} />
                <Route path="plugin/paddleocr" element={<PaddleocrPage />} />
                <Route path="mcp" element={<McpConfigPage />} />
                <Route path="batch" element={<BatchListPage />} />
              </Route>
            </Route>
            
            {/* 团队 */}
            <Route path="team">
              <Route index element={<Navigate to="list" replace />} />
              <Route path="list" element={<TeamListPage />} />
              {/* 应用配置调试页面 - 独立路由，不显示团队菜单 */}
              <Route path=":id/manage_app/chat/:appId" element={<AppConfigCommon />} />
              <Route path=":id/manage_app/workflow/:appId" element={<WorkflowEditor />} />
              <Route path=":id" element={<TeamLayout />}>
                <Route index element={<TeamWikiWrapper />} />
                <Route path="wiki" element={<TeamWikiWrapper />} />
                <Route path="applist" element={<TeamApplicationListPage />} />
                <Route path="manage_apps" element={<TeamApps />} />
                <Route path="integration" element={<TeamIntegration />} />
                <Route path="members" element={<TeamMembers />} />
                <Route path="settings" element={<TeamSettings />} />
                <Route path="plugins" element={ <TeamPluginsPage />} />
              </Route>
            </Route>
            
            {/* 提示词 */}
            <Route path="prompt">
              <Route index element={<Navigate to="list" replace />} />
              <Route path="list" element={<PromptListPage />} />
              <Route path="class" element={<PromptClassEditPage />} />
              <Route path="create" element={<PromptCreatePage />} />
              <Route path=":promptId/edit" element={<PromptEditPage />} />
              <Route path=":promptId/view" element={<PromptViewPage />} />
            </Route>
            
            {/* 插件 */}
            <Route path="plugin" element={<PluginLayout />}>
              <Route index element={<Navigate to="builtin" replace />} />
              <Route path="custom" element={<PluginCustomPage />} />
              <Route path="builtin" element={<NativePluginPage />} />
              <Route path="builtin/create" element={<CreatePluginPage />} />
              <Route path="classify" element={<PluginClassPage />} />
              <Route path="authorization" element={<PluginAuthorizationPage />} />
            </Route>
            
            {/* 管理员功能 */}
            <Route path="admin">
              <Route index element={<Navigate to="oauth" replace />} />
              <Route path="oauth" element={<OAuthPage />} />
              <Route path="usermanager" element={<UserManagerPage />} />
              <Route path="aimodel" element={<AiModelPage />} />
              <Route path="modelauthorization" element={<ModelAuthorizationPage />} />
            </Route>
            
            {/* 用户设置 */}
            <Route path="user" element={<UserLayout />}>
              <Route index element={<Navigate to="usersetting" replace />} />
              <Route path="usersetting" element={<UserSetting />} />
              <Route path="oauth" element={<BindOAuth />} />
            </Route>
          </Routes>
        </Content>
      </Layout>
    </>
  );
}

export default App;
