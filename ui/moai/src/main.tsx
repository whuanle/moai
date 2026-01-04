import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router";
import { ThemeProvider } from "@lobehub/ui";
import { App, ConfigProvider, theme } from "antd";
import zhCN from "antd/locale/zh_CN";

import "./index.css";
import { PageRouterProvider } from "./PageRouter";
import { message } from "antd";
import useAppStore from "./stateshare/store";

message.config({
  maxCount: 8,
});

// 主题配置组件
function ThemedApp() {
  const themeMode = useAppStore((state) => state.themeMode);
  const isDark = themeMode === 'dark';

  return (
    <ConfigProvider
      locale={zhCN}
      theme={{
        algorithm: isDark ? theme.darkAlgorithm : theme.defaultAlgorithm,
        token: {
          colorPrimary: "#1677ff",
          borderRadius: 6,
          colorBgContainer: isDark ? "#141414" : "#ffffff",
          colorBgLayout: isDark ? "#000000" : "#f0f2f5",
        },
        components: {
          Button: {
            colorPrimary: "#1677ff",
            colorPrimaryHover: "#4096ff",
            colorPrimaryActive: "#0958d9",
            primaryColor: "#ffffff",
            defaultBorderColor: "#d9d9d9",
            defaultColor: "#595959",
            borderRadius: 6,
            controlHeight: 36,
            paddingContentHorizontal: 20,
          },
          Menu: {
            itemBg: "transparent",
            subMenuItemBg: "transparent",
          },
        },
      }}
    >
      <ThemeProvider
        themeMode={themeMode}
        theme={{
          token: {
            colorPrimary: "#1677ff",
          },
        }}
      >
        <App>
          <RouterProvider router={PageRouterProvider} />
        </App>
      </ThemeProvider>
    </ConfigProvider>
  );
}

createRoot(document.getElementById("root")!).render(<ThemedApp />);
