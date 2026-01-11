import { RouteObject } from "react-router";
import PluginLayout from "./PluginLayout";
import NativePluginPage from "./builtin/NativePluginPage";
import CreatePluginPage from "./builtin/CreatePluginPage";
import PluginCustomPage from "./custom/PluginCustomPage";
import PluginClassPage from "./classify/PluginClassPage";
import PluginAuthorizationPage from "./authorization/PluginAuthorizationPage";

export const PluginPageRouters: RouteObject[] = [
  {
    path: "plugin",
    Component: PluginLayout,
    children: [
      {
        index: true,
        Component: NativePluginPage,
      },
      {
        path: "builtin",
        Component: NativePluginPage,
      },
      {
        path: "builtin/create",
        Component: CreatePluginPage,
      },
      {
        path: "custom",
        Component: PluginCustomPage,
      },
      {
        path: "classify",
        Component: PluginClassPage,
      },
      {
        path: "authorization",
        Component: PluginAuthorizationPage,
      },
    ],
  },
];
