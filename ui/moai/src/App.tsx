import { useEffect, useState } from "react";
import {
  useNavigate,
  Link,
  useLocation,
  Outlet,
  Route,
  Routes,
  Navigate,
} from "react-router";
import {
  Layout,
  Menu,
  message,
  Modal,
  Card,
  Avatar,
  Space,
  Tag,
  Spin,
  Flex,
  Typography,
  Dropdown,
  Button,
} from "antd";
import {
  TeamOutlined,
  HomeOutlined,
  AppstoreOutlined,
  DashboardOutlined,
  RobotOutlined,
  AppstoreAddOutlined,
  BookOutlined,
  ApiOutlined,
  SettingOutlined,
  FileTextOutlined,
  UserOutlined,
  DownOutlined,
  LogoutOutlined,
  FolderOutlined,
} from "@ant-design/icons";
import "./App.css";
import {
  CheckToken,
  RefreshServerInfo,
  GetUserDetailInfo,
} from "./InitService";
import { GetApiClient } from "./components/ServiceClient";
import useAppStore from "./stateshare/store";
import Dashboard from "./components/dashboard/Dashboard";
import OAuthPage from "./components/admin/oauth/OAuthPage";
import UserManagerPage from "./components/admin/usermanager/UserManagerPage";
import UserSetting from "./components/user/UserSetting";
// import WikiPage from "./components/wiki/WikiPage";
// import WikiLayout from "./components/wiki/WikiLayout";
// import WikiSettings from "./components/wiki/WikiSettings";
// import WikiDocument from "./components/wiki/WikiDocument";
// import DocumentEmbedding from "./components/wiki/DocumentEmbedding";
// import WikiSearch from "./components/wiki/WikiSearch";
// import WikiUser from "./components/wiki/WikiUser";

import BindOAuth from "./components/user/BindOAuth";
import AiModelPage from "./components/admin/aimodel/AiModelPage";
import PromptClassPage from "./components/admin/promptclass/PromptClassPage";
import PromptListPage from "./components/prompt/PromptListPage";
import PromptCreatePage from "./components/prompt/PromptCreatePage";
import PromptEditPage from "./components/prompt/PromptEditPage";
import PluginClassPage from "./components/admin/pluginClass/PluginClassPage";
import PluginList from "./components/plugin/PluginList";
import PluginManagerPage from "./components/admin/plugin/PluginManagerPage";

const { Sider, Content, Footer } = Layout;
const { Title } = Typography;

function App() {
  const [messageApi, contextHolder] = message.useMessage();
  const navigate = useNavigate();
  const location = useLocation();

  const [isAdmin, setIsAdmin] = useState(false);
  const [headerVisible, setHeaderVisible] = useState(true);
  const serverInfo = useAppStore.getState().getServerInfo();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);

  // 获取当前选中的菜单项
  const getSelectedKey = () => {
    const path = location.pathname;
    if (path.startsWith("/app/admin/aimodel")) {
      return "admin.aimodel";
    }
    if (path.startsWith("/app/admin/promptclass")) {
      return "admin.promptclass";
    }
    if (path.startsWith("/app/admin/pluginclass")) {
      return "admin.pluginclass";
    }
    if (path.startsWith("/app/admin/oauth")) {
      return "admin.oauth";
    }
    if (path.startsWith("/app/admin/usermanager")) {
      return "admin.usermanager";
    }
    if (path.startsWith("/app/admin/plugin")) {
      return "admin.plugin";
    }
    if (path.startsWith("/app/admin")) {
      return "admin";
    }
    if (path.startsWith("/app/user/usersetting")) {
      return "user.setting";
    }
    if (path.startsWith("/app/user/oauth")) {
      return "user.oauth";
    }
    if (path.startsWith("/app/user")) {
      return "user";
    }
    if (path.startsWith("/app/wiki")) {
      return "wiki";
    }
    if (path.startsWith("/app/prompt")) {
      return "prompt";
    }
    return "home";
  };

  useEffect(() => {
    let client = GetApiClient();
    const fetchData = async () => {
      await RefreshServerInfo(client);
      var isVerify = await CheckToken();
      if (!isVerify) {
        messageApi.success("身份已失效，正在重定向到登录页面");
        setTimeout(() => {
          navigate("/login");
        }, 1000);
      } else {
        // 获取用户详细信息
        const detailInfo = await GetUserDetailInfo();
        if (detailInfo) {
          useAppStore.getState().setUserDetailInfo(detailInfo);
          // 设置管理员状态
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

  // 获取用户显示名称
  const getUserDisplayName = () => {
    return userDetailInfo?.nickName || userDetailInfo?.userName || "用户";
  };

  // 注销登录
  const handleLogout = () => {
    useAppStore.getState().clearUserInfo();
    useAppStore.getState().clearUserDetailInfo();
    messageApi.success("已注销登录");
    navigate("/login");
  };

  // 用户头像下拉菜单项
  const userMenuItems = [
    {
      key: "logout",
      icon: <LogoutOutlined />,
      label: "注销登录",
      onClick: handleLogout,
    },
  ];

  // 动态生成菜单项，根据管理员权限显示或隐藏管理面板
  const getMenuItems = () => {
    const baseItems = [
      {
        key: "home",
        icon: <HomeOutlined />,
        label: <Link to="/app/index">首页</Link>,
      },
      {
        key: "wiki",
        icon: <BookOutlined />,
        label: <Link to="/app/wiki/list">知识库</Link>,
      },
      {
        key: "prompt",
        icon: <BookOutlined />,
        label: <Link to="/app/prompt/list">提示词</Link>,
      },
      {
        key: "user",
        icon: <UserOutlined />,
        label: "个人中心",
        children: [
          {
            key: "user.setting",
            icon: <UserOutlined />,
            label: <Link to="/app/user/usersetting">个人信息</Link>,
          },
          {
            key: "user.oauth",
            icon: <ApiOutlined />,
            label: <Link to="/app/user/oauth">第三方账号</Link>,
          },
        ],
      },
      {
        key: "application",
        icon: <AppstoreOutlined />,
        label: "应用",
        children: [
          {
            key: "application.assistant",
            icon: <RobotOutlined />,
            label: <Link to="/app/application/assistant">AI助手</Link>,
          },
        ],
      },
    ];

    // 只有管理员才能看到管理面板
    if (isAdmin) {
      baseItems.push({
        key: "admin",
        icon: <SettingOutlined />,
        label: "管理面板",
        children: [
          {
            key: "admin.oauth",
            icon: <ApiOutlined />,
            label: <Link to="/app/admin/oauth">OAuth</Link>,
          },
          {
            key: "admin.usermanager",
            icon: <TeamOutlined />,
            label: <Link to="/app/admin/usermanager">用户管理</Link>,
          },
          {
            key: "admin.aimodel",
            icon: <RobotOutlined />,
            label: <Link to="/app/admin/aimodel">AI模型</Link>,
          },
          {
            key: "admin.promptclass",
            icon: <FolderOutlined />,
            label: <Link to="/app/admin/promptclass">提示词分类</Link>,
          },
          {
            key: "admin.pluginclass",
            icon: <AppstoreOutlined />,
            label: <Link to="/app/admin/pluginclass">插件分类</Link>,
          },
          {
            key: "admin.plugin",
            icon: <AppstoreOutlined />,
            label: <Link to="/app/admin/plugin">插件</Link>,
          }
        ],
      });
    }

    return baseItems;
  };

  return (
    <>
      {contextHolder}
      <Layout className="layout">
        {headerVisible && (
          <Layout.Header
            className="header"
            style={{
              display: "flex",
              alignItems: "center",
              justifyContent: "space-between",
            }}
          >
            <div className="logo-container">
              <img
                src="/logo.png"
                width={45}
                height={45}
                style={{ objectFit: "contain" }}
                alt="MoAI Logo"
              />
              <p>MoAI</p>
            </div>
            <Dropdown
              menu={{ items: userMenuItems }}
              placement="bottomRight"
              arrow
            >
              <div
                style={{
                  cursor: "pointer",
                  display: "flex",
                  alignItems: "center",
                  padding: "4px 8px",
                  borderRadius: "6px",
                  transition: "background-color 0.2s",
                }}
                onMouseEnter={(e) =>
                  (e.currentTarget.style.backgroundColor = "#f5f5f5")
                }
                onMouseLeave={(e) =>
                  (e.currentTarget.style.backgroundColor = "transparent")
                }
              >
                <Avatar
                  size={32}
                  icon={<UserOutlined />}
                  style={{ marginRight: 8 }}
                >
                  {getUserDisplayName().charAt(0).toUpperCase()}
                </Avatar>
                <span style={{ marginRight: 4, color: "#333" }}>
                  {getUserDisplayName()}
                </span>
                <DownOutlined style={{ fontSize: "12px", color: "#666" }} />
              </div>
            </Dropdown>
          </Layout.Header>
        )}
        <Layout>
          <Sider
            width={200}
            className="sider"
            collapsible
            onCollapse={(collapsed) => {
              setHeaderVisible(!collapsed);
            }}
          >
            <Menu
              mode="inline"
              className="menu-inline"
              selectedKeys={[getSelectedKey()]}
              items={getMenuItems()}
            />
          </Sider>
          <Layout style={{ padding: "0 0px 0px" }}>
            <Content className="content">
              <Routes>
                <Route index element={<Dashboard />} />
                <Route path="index" element={<Dashboard />} />
                <Route path="admin">
                  <Route index element={<Navigate to="oauth" replace />} />
                  <Route path="oauth" element={<OAuthPage />} />
                  <Route path="usermanager" element={<UserManagerPage />} />
                  <Route path="aimodel" element={<AiModelPage />} />
                  <Route path="promptclass" element={<PromptClassPage />} />
                  <Route path="pluginclass" element={<PluginClassPage />} />
                  <Route path="plugin" element={<PluginManagerPage />} />
                </Route>
                <Route path="user">
                  <Route
                    index
                    element={<Navigate to="usersetting" replace />}
                  />
                  <Route path="usersetting" element={<UserSetting />} />
                  <Route path="oauth" element={<BindOAuth />} />
                </Route>
                {/* <Route path="wiki">
                  <Route index element={<Navigate to="list" replace />} />
                  <Route path="list" element={<WikiPage />} />
                  <Route path=":id" element={<WikiLayout />}>
                    <Route path="settings" element={<WikiSettings />} />
                    <Route index element={<WikiDocument />} />
                    <Route path="document" element={<WikiDocument />} />
                    <Route path="document/:documentId/embedding" element={<DocumentEmbedding />} />
                    <Route path="search" element={<WikiSearch />} />
                    <Route path="user" element={<WikiUser />} />
                    <Route path="crawle" element={<WikiCrawle />} />
                    <Route path="crawle/:crawleId" element={<WikiCrawleLayout />}>
                      <Route index element={<Navigate to="config" replace />} />
                      <Route path="config" element={<WikiCrawleConfig />} />
                      <Route path="document" element={<WikiCrawleDocument />} />
                      <Route path="task" element={<WikiCrawleTask />} />
                    </Route>
                  </Route>
                </Route> */}
                {/* <Route path="plugin">
                  <Route index element={<Navigate to="list" replace />} />
                </Route> */}
                <Route path="prompt">
                  <Route index element={<Navigate to="list" replace />} />
                  <Route path="list" element={<PromptListPage />} />
                  <Route path="create" element={<PromptCreatePage />} />
                  <Route path=":promptId/edit" element={<PromptEditPage />} />
                  {/* <Route path="class" element={<PromptClassManage />} />

                  <Route path=":promptId/content" element={<PromptContent />} /> */}
                </Route>
                {/* <Route path="application">
                  <Route path="assistant" element={<AiAssistant />} />
                </Route> */}
              </Routes>
            </Content>
            {/* <Footer className="footer">moai ©2025</Footer> */}
          </Layout>
        </Layout>
      </Layout>
    </>
  );
}

export default App;
