import { RouteObject } from "react-router";
import TeamListPage from "./TeamListPage";
import TeamLayout from "./TeamLayout";
import TeamMembers from "./TeamMembers";
import TeamSettings from "./TeamSettings";
import TeamWikiWrapper from "./TeamWikiWrapper";
import TeamApps from "./TeamApps";
import TeamIntegration from "./TeamIntegration";

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
            path: "apps",
            Component: TeamApps,
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
