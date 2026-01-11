import { RouteObject } from "react-router";
import PaddleocrPage from "./PaddleocrPage";

export const PaddleocrPageRouters: RouteObject[] = [
  {
    path: "plugin/paddleocr",
    Component: PaddleocrPage,
  },
];
