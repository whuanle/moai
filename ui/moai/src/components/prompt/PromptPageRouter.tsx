import { RouteObject } from "react-router";
import PromptListPage from "./PromptListPage";
import PromptCreatePage from "./PromptCreatePage";
import PromptEditPage from "./PromptEditPage";
import PromptViewPage from "./PromptViewPage";

export const PromptPageRouters: RouteObject[] = [
  {
    path: "prompt",
    children: [
      {
        path: "list",
        Component: PromptListPage,
      },
      {
        path: "create",
        Component: PromptCreatePage,
      },
      {
        path: ":promptId/view",
        Component: PromptViewPage,
      },
      {
        path: ":promptId/edit",
        Component: PromptEditPage,
      },
    ],
  },
];
