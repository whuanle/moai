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
import PromptClassPage from "./components/admin/promptclass/PromptClassPage";
import PromptClassEditPage from "./components/prompt/PromptClassPage";
import PromptListPage from "./components/prompt/PromptListPage";
import PromptCreatePage from "./components/prompt/PromptCreatePage";
import PromptEditPage from "./components/prompt/PromptEditPage";
import PluginClassPage from "./components/admin/pluginClass/PluginClassPage";
import PluginManagerPage from "./components/admin/plugin/PluginManagerPage";
import NativePluginPage from "./components/admin/nativeplugin/NativePluginPage";
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
import TeamListPage from "./components/team/TeamListPage";
import TeamLayout from "./components/team/TeamLayout";
import TeamMembers from "./components/team/TeamMembers";
import TeamSettings from "./components/team/TeamSettings";
import ApplicationPage from "./components/application/ApplicationPage";
import UserLayout from "./components/user/UserLayout";
import TeamWikiWrapper from "./components/team/TeamWikiWrapper";
import TeamApps from "./components/team/TeamApps";
import TeamIntegration from "./components/team/TeamIntegration";
import McpConfigPage from "./components/wiki/McpConfigPage";
import PromptViewPage from "./components/prompt/PromptViewPage";

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
            <Route path="application" element={<ApplicationPage />} />
            
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
                <Route path="plugin/mcp" element={<McpConfigPage />} />
                <Route path="batch" element={<BatchListPage />} />
              </Route>
            </Route>
            
            {/* 团队 */}
            <Route path="team">
              <Route index element={<Navigate to="list" replace />} />
              <Route path="list" element={<TeamListPage />} />
              <Route path=":id" element={<TeamLayout />}>
                <Route index element={<TeamWikiWrapper />} />
                <Route path="wiki" element={<TeamWikiWrapper />} />
                <Route path="apps" element={<TeamApps />} />
                <Route path="integration" element={<TeamIntegration />} />
                <Route path="members" element={<TeamMembers />} />
                <Route path="settings" element={<TeamSettings />} />
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
            
            {/* 管理员功能 */}
            <Route path="admin">
              <Route index element={<Navigate to="oauth" replace />} />
              <Route path="oauth" element={<OAuthPage />} />
              <Route path="usermanager" element={<UserManagerPage />} />
              <Route path="aimodel" element={<AiModelPage />} />
              <Route path="promptclass" element={<PromptClassPage />} />
              <Route path="pluginclass" element={<PluginClassPage />} />
              <Route path="plugin" element={<PluginManagerPage />} />
              <Route path="internalplugin" element={<NativePluginPage />} />
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
