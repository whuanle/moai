import { RouteObject } from "react-router";
import OAuth from "./OAuth";
import UserManager from "./UserManager";
import SystemAIModel from "./SystemAiModel";
import SystemPluginList from "./PluginList";

export const AdminPageRouters: RouteObject[] = [
  {
    path: "oauth",
    Component: OAuth,
  },
  {
    path: "usermanager",
    Component: UserManager,
  },
  {
    path: "aimodel",
    Component: SystemAIModel,
  },
  {
    path: "plugin",
    Component: SystemPluginList,
  }
];
