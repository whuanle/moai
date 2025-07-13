import { RouteObject } from "react-router";
import OAuth from "./OAuth";
import UserManager from "./UserManager";
import AIModel from "./AiModel";

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
    Component: AIModel,
  },
];
