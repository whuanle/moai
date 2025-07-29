import { RouteObject } from "react-router";
import PromptLayout from "./PromptLayout";
import PromptList from "./PromptList";
import PromptContent from "./PromptContent";
import PromptEdit from "./PromptEdit";
import PromptCreate from "./PromptCreate";
import PromptClassManage from "./PromptClassManage";

export const PromptPageRouters: RouteObject[] = [
  {
    path: "list",
    Component: PromptList,
  },
  {
    path: "create",
    Component: PromptCreate,
  },
  {
    path: "class",
    Component: PromptClassManage,
  },
  {
    path: ":promptId/edit",
    Component: PromptEdit,
  },
  {
    path: ":promptId/content",
    Component: PromptContent,
  }
];
