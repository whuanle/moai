import { createBrowserRouter } from "react-router";

import Home from "./Home";
import App from "./App";
import { DashboardPageRouter } from "./components/dashboard/AdminPageRouter";
import { LoginPageRouters } from "./components/login/LoginPageRouters";
import { AdminPageRouters } from "./components/admin/AdminPageRouter";
import { UserPageRouters } from "./components/user/UserPageRouter";
import { WikiPageRouters } from "./components/wiki/WikiPageRouter";
import { PluginPageRouters } from "./components/plugin/PluginPageRouter";
import AiAssistant from "./components/app/AiAssistant";

// 在此集合所有页面的路由，每个子模块的路由从模块下的 PageRouter 导出

export const PageRouterProvider = createBrowserRouter([
  {
    path: "/",
    Component: Home,
  },
  {
    path: "/app/*",
    Component: App,
    children: [
      DashboardPageRouter,
      ...AdminPageRouters,
      ...UserPageRouters,
      ...WikiPageRouters,
      ...PluginPageRouters,
    ],
  },
  // AI 助手独立页面（不带菜单）
  {
    path: "/chat",
    Component: AiAssistant,
  },
  {
    path: "/chat/:chatId",
    Component: AiAssistant,
  },
  ...LoginPageRouters,
]);
