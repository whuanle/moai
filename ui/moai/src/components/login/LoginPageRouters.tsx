import { RouteObject } from "react-router";
import Login from "./Login";
import Register from "./Register";
import OAuthLogin from "./OAuthLogin";


export const LoginPageRouters: RouteObject[] = [
  {
    path: "/login",
    Component: Login,
  },
  {
    path: "/register",
    Component: Register,
  },
  {
    path: "/oauth_login",
    Component: OAuthLogin,
  },
];
