import { RouteObject } from "react-router";
import PromptLayout from "./PromptLayout";
import PromptListPage from "./PromptListPage";
import PromptCreatePage from "./PromptCreatePage";
import PromptEditPage from "./PromptEditPage";

export const PromptPageRouters: RouteObject[] = [
  {
    path: "list",
    Component: PromptListPage,
  },
  {
    path: "create",
    Component: PromptCreatePage,
  },
  {
    path: ":promptId/edit",
    Component: PromptEditPage,
  },
  // {
  //   path: "class",
  //   Component: PromptClassManage,
  // },
  // {
  //   path: ":promptId/content",
  //   Component: PromptContent,
  // }
];
