import { useNavigate, useLocation } from "react-router";
import { Layout, Menu, Avatar, Dropdown, message } from "antd";
import type { MenuProps } from "antd";
import { useState, useEffect, useRef, useCallback } from "react";
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
import gsap from "gsap";
import useAppStore from "./stateshare/store";
import AuthModal from "./components/common/AuthModal";
import ThemeToggle from "./components/common/ThemeToggle";
import "./AppHeader.css";

const { Header } = Layout;

interface AppHeaderProps {
  isAdmin: boolean;
}

export default function AppHeader({ isAdmin }: AppHeaderProps) {
  const navigate = useNavigate();
  const location = useLocation();
  const [messageApi, contextHolder] = message.useMessage();
  const [authModalOpen, setAuthModalOpen] = useState(false);
  const userDetailInfo = useAppStore((state) => state.userDetailInfo);
  
  // 滑动指示器状态
  const [indicatorStyle, setIndicatorStyle] = useState({ left: 0, width: 0 });
  const menuRef = useRef<HTMLDivElement>(null);
  const shimmerTimelineRef = useRef<gsap.core.Timeline | null>(null);

  // 获取当前选中的菜单项
  const getSelectedKey = useCallback((): string => {
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
  }, [location.pathname]);

  // 更新滑动指示器位置
  const updateIndicator = useCallback((key: string) => {
    if (!menuRef.current) return;
    
    const menuItem = menuRef.current.querySelector(`[data-menu-id="${key}"]`) as HTMLElement;
    if (menuItem) {
      const menuRect = menuRef.current.getBoundingClientRect();
      const itemRect = menuItem.getBoundingClientRect();
      setIndicatorStyle({
        left: itemRect.left - menuRect.left,
        width: itemRect.width,
      });
    }
  }, []);

  // 监听路由变化，更新指示器位置
  useEffect(() => {
    const selectedKey = getSelectedKey();
    updateIndicator(selectedKey);
  }, [getSelectedKey, updateIndicator]);

  // 窗口大小变化时重新计算位置
  useEffect(() => {
    const handleResize = () => {
      const selectedKey = getSelectedKey();
      updateIndicator(selectedKey);
    };
    
    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, [getSelectedKey, updateIndicator]);

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

  // 处理菜单项悬停 - GSAP 字符闪光动画
  const handleMenuItemHover = useCallback((key: string) => {
    // 清除之前的动画
    if (shimmerTimelineRef.current) {
      shimmerTimelineRef.current.kill();
    }

    const menuItem = menuRef.current?.querySelector(`[data-menu-id="${key}"]`) as HTMLElement;
    if (!menuItem) return;

    // 将文本拆分成字符
    const text = menuItem.textContent || "";
    const chars = text.split("");
    
    // 清空原内容并用 span 包裹每个字符
    menuItem.innerHTML = "";
    const charElements = chars.map((char) => {
      const span = document.createElement("span");
      span.textContent = char;
      span.style.display = "inline-block";
      span.style.position = "relative";
      menuItem.appendChild(span);
      return span;
    });

    // 创建 GSAP 时间线动画
    const timeline = gsap.timeline();
    shimmerTimelineRef.current = timeline;

    // 逐个字符闪光 - 柔和的高光效果
    charElements.forEach((char, index) => {
      timeline.to(
        char,
        {
          color: "#a8d5ff", // 淡蓝白色，不是纯白
          textShadow: "0 0 4px rgba(168, 213, 255, 0.4)", // 柔和的发光
          scale: 1.05, // 轻微放大
          duration: 0.2,
          ease: "power2.out",
        },
        index * 0.06 // 每个字符延迟 0.06 秒
      );
      
      // 闪光后恢复原色
      timeline.to(
        char,
        {
          color: "var(--color-primary)",
          textShadow: "none",
          scale: 1,
          duration: 0.2,
          ease: "power2.in",
        },
        index * 0.06 + 0.15
      );
    });
  }, []);

  // 处理菜单项离开 - 恢复原始文本
  const handleMenuItemLeave = useCallback((key: string) => {
    if (shimmerTimelineRef.current) {
      shimmerTimelineRef.current.kill();
    }

    const menuItem = menuRef.current?.querySelector(`[data-menu-id="${key}"]`) as HTMLElement;
    if (!menuItem) return;

    // 恢复原始文本（移除 span 包裹）
    const text = menuItem.textContent || "";
    menuItem.innerHTML = text;
  }, []);

  // 构建主菜单项
  const getMainMenuItems = (): MenuProps["items"] => {
    const baseItems: MenuProps["items"] = [
      {
        key: "ai",
        icon: <RobotOutlined />,
        label: <span data-menu-id="ai">全能AI</span>,
        onMouseEnter: () => handleMenuItemHover("ai"),
        onMouseLeave: () => handleMenuItemLeave("ai"),
      },
      {
        key: "application",
        icon: <AppstoreOutlined />,
        label: <span data-menu-id="application">应用</span>,
        onMouseEnter: () => handleMenuItemHover("application"),
        onMouseLeave: () => handleMenuItemLeave("application"),
      },
      {
        key: "wiki",
        icon: <BookOutlined />,
        label: <span data-menu-id="wiki">知识库</span>,
        onMouseEnter: () => handleMenuItemHover("wiki"),
        onMouseLeave: () => handleMenuItemLeave("wiki"),
      },
      {
        key: "team",
        icon: <TeamOutlined />,
        label: <span data-menu-id="team">团队</span>,
        onMouseEnter: () => handleMenuItemHover("team"),
        onMouseLeave: () => handleMenuItemLeave("team"),
      },
      {
        key: "prompt",
        icon: <FileTextOutlined />,
        label: <span data-menu-id="prompt">提示词</span>,
        onMouseEnter: () => handleMenuItemHover("prompt"),
        onMouseLeave: () => handleMenuItemLeave("prompt"),
      },
    ];

    // 管理员菜单
    if (isAdmin) {
      baseItems.push(
        {
          key: "plugin",
          icon: <ApiOutlined />,
          label: <span data-menu-id="plugin">插件</span>,
          onMouseEnter: () => handleMenuItemHover("plugin"),
          onMouseLeave: () => handleMenuItemLeave("plugin"),
        },
        {
          key: "model.list",
          icon: <RobotOutlined />,
          label: <span data-menu-id="model.list">模型</span>,
          onMouseEnter: () => handleMenuItemHover("model.list"),
          onMouseLeave: () => handleMenuItemLeave("model.list"),
        },
        {
          key: "admin",
          icon: <SettingOutlined />,
          label: <span data-menu-id="admin">管理</span>,
          onMouseEnter: () => handleMenuItemHover("admin"),
          onMouseLeave: () => handleMenuItemLeave("admin"),
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
      {/* 认证弹窗 */}
      <AuthModal
        open={authModalOpen}
        onClose={() => setAuthModalOpen(false)}
        defaultTab="login"
        onSuccess={() => window.location.reload()}
      />
      <Header className="app-header">
        <div className="header-left">
          <div className="logo-container" onClick={() => navigate("/app/application")}>
            <img src="/logo.png" width={36} height={36} alt="MoAI Logo" />
            <span className="logo-text">MoAI</span>
          </div>
          <div className="menu-wrapper" ref={menuRef}>
            <Menu
              mode="horizontal"
              selectedKeys={[getSelectedKey()]}
              items={getMainMenuItems()}
              onClick={handleMenuClick}
              className="main-menu"
            />
            {/* 滑动指示器 */}
            <div 
              className="menu-indicator" 
              style={{
                transform: `translateX(${indicatorStyle.left}px)`,
                width: `${indicatorStyle.width}px`,
              }}
            />
          </div>
        </div>
        <div className="header-right">
          {/* 主题切换按钮 - 在用户头像左边 */}
          <ThemeToggle />
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
