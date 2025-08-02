import { RouteObject } from "react-router";
import OAuth from "./OAuth";
import UserManager from "./UserManager";
import SystemAIModel from "./SystemAiModel";
import SystemPluginList from "./PluginList";
import SystemSettings from "./SystemSettings";

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
  },
  {
    path: "settings",
    Component: SystemSettings,
  }
];
