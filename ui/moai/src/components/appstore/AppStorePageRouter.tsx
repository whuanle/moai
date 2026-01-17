import { RouteObject } from "react-router";
import AppStorePage from "./AppStorePage";
import AppChatPage from "../application/Apps/AppCommon/AppChatPage";

export const AppStorePageRouters: RouteObject[] = [
  {
    path: "appstore",
    children: [
      {
        path: ":teamId",
        Component: AppStorePage,
      },
      {
        path: ":appId",
        Component: AppChatPage,
      },
    ],
  },
];
