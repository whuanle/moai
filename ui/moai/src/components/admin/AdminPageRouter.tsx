import { RouteObject } from "react-router";
import OAuthPage from "./oauth/OAuthPage";
import UserManagerPage from "./usermanager/UserManagerPage";
import AiModelPage from "./aimodel/AiModelPage";
import ModelAuthorizationPage from "./aimodel/ModelAuthorizationPage";
import { PluginPageRouters } from "./plugin/PluginPageRouter";
import PluginLayout from "./plugin/PluginLayout";

export const AdminPageRouters: RouteObject[] = [
  {
    path: "oauth",
    Component: OAuthPage,
  },
  {
    path: "usermanager",
    Component: UserManagerPage,
  },
  {
    path: "aimodel",
    Component: AiModelPage,
  },
  {
    path: "modelauthorization",
    Component: ModelAuthorizationPage,
  },
  {
    path: "plugin",
    Component: PluginLayout,
  }
];
