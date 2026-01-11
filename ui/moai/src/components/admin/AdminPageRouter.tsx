import { RouteObject } from "react-router";
import OAuthPage from "./oauth/OAuthPage";
import UserManagerPage from "./usermanager/UserManagerPage";
import AiModelPage from "./aimodel/AiModelPage";
import ModelAuthorizationPage from "./aimodel/ModelAuthorizationPage";

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
];
