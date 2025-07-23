import { RouteObject } from "react-router";
import PromptLayout from "./PromptLayout";
import PromptList from "./PromptList";

export const PromptPageRouters: RouteObject[] = [
  {
    path: "list",
    Component: PromptList,
  },
  {
    path: ":promptId/edit",
    Component: PromptLayout,
  },
];
