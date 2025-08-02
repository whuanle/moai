import { createRoot } from "react-dom/client";
import { RouterProvider } from "react-router";
import { ThemeProvider } from "@lobehub/ui";
import { App, ConfigProvider } from "antd";

import "./index.css";
import { PageRouterProvider } from "./PageRouter";
import { message } from "antd";

message.config({
  maxCount: 8,
});

createRoot(document.getElementById("root")!).render(
  <ConfigProvider>
    <ThemeProvider
      theme={{
        token: {
          colorPrimary: "#fff",
        },
      }}
    >
      <App>
        <RouterProvider router={PageRouterProvider} />
      </App>
    </ThemeProvider>
  </ConfigProvider>
);
