import { useNavigate, useLocation } from "react-router";
import { Layout, Menu, Avatar, Dropdown, message } from "antd";
import type { MenuProps } from "antd";
import {
  AppstoreOutlined,
  BookOutlined,
  TeamOutlined,
  FileTextOutlined,
  ApiOutlined,
  RobotOutlined,
  SettingOutlined,
  UserOutlined,
  DownOutlined,
  LogoutOutlined,
  LinkOutlined,
} from "@ant-design/icons";
import useAppStore from "./stateshare/store";
import "./AppHeader.css";

const { Header } = Layout;

interface AppHeaderProps {
  isAdmin: boolean;
}

export default function AppHeader({ isAdmin }: AppHeaderProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const [messageApi, contextHolder] = message.useMessage();
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);

  // 获取当前选中的菜单项
  const getSelectedKey = (): string => {
    const path = location.pathname;
    
    // 全能AI菜单
    if (path.startsWith("/chat")) {
      return "ai";
    }
    // 管理菜单
    if (path.startsWith("/app/admin/oauth") || path.startsWith("/app/admin/usermanager")) {
      return "admin";
    }
    // 模型菜单
    if (path.startsWith("/app/admin/modelauthorization")) {
      return "model.authorization";
    }
    if (path.startsWith("/app/admin/aimodel")) {
      return "model.list";
    }
    // 插件菜单
    if (path.startsWith("/app/plugin")) {
      return "plugin";
    }
    // 团队菜单
    if (path.startsWith("/app/team")) {
      return "team";
    }
    // 知识库菜单
    if (path.startsWith("/app/wiki")) {
      return "wiki";
    }
    // 提示词菜单
    if (path.startsWith("/app/prompt")) {
      return "prompt";
    }
    // 应用菜单
    if (path.startsWith("/app/application") || path === "/app/index" || path === "/app") {
      return "application";
    }
    
    return "application";
  };

  // 菜单点击处理
  const handleMenuClick: MenuProps["onClick"] = (e) => {
    const key = e.key;
    
    switch (key) {
      case "ai":
        navigate("/chat");
        break;
      case "application":
        navigate("/app/application");
        break;
      case "wiki":
        navigate("/app/wiki/list");
        break;
      case "team":
        navigate("/app/team/list");
        break;
      case "prompt":
        navigate("/app/prompt/list");
        break;
      // 插件菜单 - 默认进入内置插件
      case "plugin":
        navigate("/app/plugin/builtin");
        break;
      // 模型子菜单
      case "model.list":
        navigate("/app/admin/aimodel");
        break;
      case "model.authorization":
        navigate("/app/admin/modelauthorization");
        break;
      // 管理子菜单
      case "admin.oauth":
        navigate("/app/admin/oauth");
        break;
      case "admin.user":
        navigate("/app/admin/usermanager");
        break;
      default:
        break;
    }
  };

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
  const userMenuItems: MenuProps["items"] = [
    {
      key: "usersetting",
      icon: <UserOutlined />,
      label: "个人设置",
      onClick: () => navigate("/app/user/usersetting"),
    },
    {
      key: "oauth",
      icon: <LinkOutlined />,
      label: "第三方账号",
      onClick: () => navigate("/app/user/oauth"),
    },
    { type: "divider" },
    {
      key: "logout",
      icon: <LogoutOutlined />,
      label: "注销登录",
      onClick: handleLogout,
    },
  ];

  // 构建主菜单项
  const getMainMenuItems = (): MenuProps["items"] => {
    const baseItems: MenuProps["items"] = [
      {
        key: "ai",
        icon: <RobotOutlined />,
        label: "全能AI",
      },
      {
        key: "application",
        icon: <AppstoreOutlined />,
        label: "应用",
      },
      {
        key: "wiki",
        icon: <BookOutlined />,
        label: "知识库",
      },
      {
        key: "team",
        icon: <TeamOutlined />,
        label: "团队",
      },
      {
        key: "prompt",
        icon: <FileTextOutlined />,
        label: "提示词",
      },
    ];

    // 管理员菜单
    if (isAdmin) {
      baseItems.push(
        {
          key: "plugin",
          icon: <ApiOutlined />,
          label: "插件",
        },
        {
          key: "model.list",
          icon: <RobotOutlined />,
          label: "模型"
        },
        {
          key: "admin",
          icon: <SettingOutlined />,
          label: "管理",
          children: [
            {
              key: "admin.oauth",
              icon: <LinkOutlined />,
              label: "OAuth",
            },
            {
              key: "admin.user",
              icon: <UserOutlined />,
              label: "用户",
            },
          ],
        }
      );
    }

    return baseItems;
  };

  return (
    <>
      {contextHolder}
      <Header className="app-header">
        <div className="header-left">
          <div className="logo-container" onClick={() => navigate("/app/application")}>
            <img src="/logo.png" width={36} height={36} alt="MoAI Logo" />
            <span className="logo-text">MoAI</span>
          </div>
          <Menu
            mode="horizontal"
            selectedKeys={[getSelectedKey()]}
            items={getMainMenuItems()}
            onClick={handleMenuClick}
            className="main-menu"
          />
        </div>
        <div className="header-right">
          <Dropdown menu={{ items: userMenuItems }} placement="bottomRight" arrow>
            <div className="user-dropdown">
              <Avatar size={32} icon={<UserOutlined />} src={userDetailInfo?.avatar}>
                {userDetailInfo?.avatar}
              </Avatar>
              <span className="user-name">{getUserDisplayName()}</span>
              <DownOutlined className="dropdown-icon" />
            </div>
          </Dropdown>
        </div>
      </Header>
    </>
  );
}
