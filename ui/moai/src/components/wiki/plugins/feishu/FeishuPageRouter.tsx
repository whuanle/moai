import { RouteObject } from "react-router";
import FeishuListPage from "./FeishuListPage";
import FeishuDetailPage from "./FeishuDetailPage";

export const FeishuPageRouters: RouteObject[] = [
  {
    path: "plugin/feishu/:configId",
    Component: FeishuDetailPage,
  },
  {
    path: "plugin/feishu",
    Component: FeishuListPage,
  },
];

