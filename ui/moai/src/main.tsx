import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router";
import { ThemeProvider } from "@lobehub/ui";
import { App, ConfigProvider, theme } from "antd";
import zhCN from "antd/locale/zh_CN";

import "./index.css";
import "./styles/theme-dark.css";
import "./styles/theme-light.css";
import { PageRouterProvider } from "./PageRouter";
import { message } from "antd";
import useAppStore from "./stateshare/store";

message.config({
  maxCount: 8,
});

// 主题配置组件 - 深空探索暗色主题
function ThemedApp() {
  // 默认使用暗色主题
  const themeMode = 'dark';

  return (
    <ConfigProvider
      locale={zhCN}
      theme={{
        algorithm: theme.darkAlgorithm,
        token: {
          // 主色调 - 星云蓝
          colorPrimary: "#4A9EFF",
          colorInfo: "#4A9EFF",
          colorSuccess: "#00E676",
          colorWarning: "#FFB300",
          colorError: "#FF5252",
          
          // 背景色
          colorBgBase: "#0F1419",
          colorBgContainer: "#242B3D",
          colorBgElevated: "#1A1F2E",
          colorBgLayout: "#0F1419",
          colorBgSpotlight: "#2D3548",
          
          // 文字颜色
          colorText: "#E3E8EF",
          colorTextSecondary: "#8B95A5",
          colorTextTertiary: "#5A6270",
          colorTextQuaternary: "#3D4451",
          
          // 边框
          colorBorder: "#2D3548",
          colorBorderSecondary: "#242B3D",
          
          // 圆角
          borderRadius: 6,
          borderRadiusLG: 8,
          borderRadiusSM: 4,
          
          // 字体
          fontSize: 14,
          fontFamily: "-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif",
        },
        components: {
          Button: {
            colorPrimary: "#4A9EFF",
            colorPrimaryHover: "#6BB0FF",
            colorPrimaryActive: "#2E8AFF",
            primaryColor: "#0F1419",
            defaultBorderColor: "#2D3548",
            defaultColor: "#E3E8EF",
            borderRadius: 6,
            controlHeight: 36,
            paddingContentHorizontal: 20,
            fontWeight: 500,
          },
          Menu: {
            itemBg: "transparent",
            subMenuItemBg: "transparent",
            itemColor: "#8B95A5",
            itemHoverColor: "#4A9EFF",
            itemSelectedColor: "#4A9EFF",
            itemSelectedBg: "rgba(74, 158, 255, 0.12)",
          },
          Input: {
            colorBgContainer: "#1A1F2E",
            colorBorder: "#2D3548",
            colorText: "#E3E8EF",
            colorTextPlaceholder: "#5A6270",
          },
          Card: {
            colorBgContainer: "#242B3D",
            colorBorderSecondary: "#2D3548",
          },
          Table: {
            colorBgContainer: "#242B3D",
            colorBorderSecondary: "#2D3548",
            headerBg: "#1A1F2E",
            headerColor: "#E3E8EF",
          },
          Modal: {
            contentBg: "#242B3D",
            headerBg: "#242B3D",
          },
          Tabs: {
            itemColor: "#8B95A5",
            itemHoverColor: "#4A9EFF",
            itemSelectedColor: "#4A9EFF",
            inkBarColor: "#4A9EFF",
          },
        },
      }}
    >
      <ThemeProvider
        themeMode={themeMode}
        theme={{
          token: {
            colorPrimary: "#4A9EFF",
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
