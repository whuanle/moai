import { RouteObject } from "react-router";
import TeamListPage from "./TeamListPage";
import TeamLayout from "./TeamLayout";
import TeamMembers from "./TeamMembers";
import TeamSettings from "./TeamSettings";
import TeamWikiWrapper from "./TeamWikiWrapper";
import TeamApps from "./apps/TeamApps";
import AppConfigCommon from "./apps/AppConfigCommon";
import TeamIntegration from "./TeamIntegration";
import AppStorePage from "./appstore/AppStorePage";
import AppChatPage from "./appstore/AppChatPage";

export const TeamPageRouters: RouteObject[] = [
  {
    path: "team",
    children: [
      {
        path: "list",
        Component: TeamListPage,
      },
      {
        path: ":id",
        Component: TeamLayout,
        children: [
          {
            index: true,
            Component: TeamWikiWrapper,
          },
          {
            path: "wiki",
            Component: TeamWikiWrapper,
          },
          {
            path: "appstore",
            Component: AppStorePage,
          },
          {
            path: "appstore/:appId",
            Component: AppChatPage,
          },
          {
            path: "members",
            Component: TeamMembers,
          },
          {
            path: "apps",
            Component: TeamApps,
          },
          {
            path: "apps/:appId/config",
            Component: AppConfigCommon,
          },
          {
            path: "integration",
            Component: TeamIntegration,
          },
          {
            path: "settings",
            Component: TeamSettings,
          },
        ],
      },
      {
        path: "",
        Component: TeamListPage,
      },
    ],
  },
];
