import { RouteObject } from "react-router";
import UserSetting from "./UserSetting";
import BindOAuth from "./BindOAuth";
export const UserPageRouters: RouteObject[] = [
  {
    path: "usersetting",
    Component: UserSetting,
  },
  {
    path: "oauth",
    Component: BindOAuth,
  },
];
