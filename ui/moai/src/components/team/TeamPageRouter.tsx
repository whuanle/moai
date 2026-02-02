import { RouteObject } from "react-router";
import TeamListPage from "./TeamListPage";
import TeamLayout from "./TeamLayout";
import TeamMembers from "./TeamMembers";
import TeamSettings from "./TeamSettings";
import TeamWikiWrapper from "./TeamWikiWrapper";
import TeamApps from "./apps/TeamApps";
import TeamApplicationListPage from "./apps/TeamApplicationListPage";
import AppConfigCommon from "./apps/chatapp/ChatAppConfig";
import { WorkflowEditor } from "./apps/workflow";
import TeamIntegration from "./TeamIntegration";
import { TeamPluginsPage } from "./plugins";

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
            path: "members",
            Component: TeamMembers,
          },
          {
            path: "manage_apps",
            Component: TeamApps,
          },
          {
            path: "applist",
            Component: TeamApplicationListPage,
          },
          {
            path: "apps/:appId/config",
            Component: AppConfigCommon,
          },
          {
            path: "apps/:appId/workflow",
            Component: WorkflowEditor,
          },
          {
            path: "plugins",
            Component: TeamPluginsPage,
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
