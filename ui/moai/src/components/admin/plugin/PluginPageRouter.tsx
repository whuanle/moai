import { RouteObject } from "react-router";
import PluginList from "./PluginList";


export const PluginPageRouters: RouteObject[] = [
  {
    path: "list",
    Component: PluginList,
  },
];
