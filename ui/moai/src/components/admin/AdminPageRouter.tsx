
import { RouteObject } from "react-router";
import OAuth from "./OAuth";
import UserManager from "./UserManager";

export const AdminPageRouters: RouteObject[] = [{
  path: "oauth",
  Component: OAuth,
}, {
  path: "usermanager",
  Component: UserManager,
}];
