import { RouteObject } from "react-router";
import OpenApiListPage from "./OpenApiListPage";

export const OpenApiPageRouters: RouteObject[] = [
  {
    path: "plugin/openapi",
    Component: OpenApiListPage,
  },
];
