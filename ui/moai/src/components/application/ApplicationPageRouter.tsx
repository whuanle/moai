import { RouteObject } from "react-router";
import ApplicationPage from "./ApplicationPage";
import ApplicationClassifyPage from "./ApplicationClassifyPage";
import ApplicationListPage from "./ApplicationListPage";
import AppChatPage from "./Apps/AppCommon/AppChatPage";

export const ApplicationPageRouters: RouteObject[] = [
  {
    path: "application",
    children: [
      {
        path: "",
        Component: ApplicationPage,
      },
      {
        path: "classify",
        Component: ApplicationClassifyPage,
      },
      {
        path: "list/:classifyId",
        Component: ApplicationListPage,
      },
      {
        path: "chat/:appId",
        Component: AppChatPage,
      },
    ],
  },
];
