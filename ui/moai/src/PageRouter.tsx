import { createBrowserRouter, RouterProvider } from "react-router";

import Home from "./Home";
import App from "./App";
import { DashboardPageRouter } from "./components/dashboard/AdminPageRouter";
import { LoginPageRouters } from "./components/login/LoginPageRouters";

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
    ],
  },
  ...LoginPageRouters,
]);
