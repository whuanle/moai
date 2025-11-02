import { RouteObject } from "react-router";
import OAuthPage from "./oauth/OAuthPage";
import UserManagerPage from "./usermanager/UserManagerPage";
import AiModelPage from "./aimodel/AiModelPage";
import PromptClassPage from "./promptclass/PromptClassPage";
import PluginClassPage from "./pluginClass/PluginClassPage";

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
    path: "promptclass",
    Component: PromptClassPage,
  },
  {
    path: "pluginclass",
    Component: PluginClassPage,
  }
];
