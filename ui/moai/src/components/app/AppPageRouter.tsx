import { RouteObject } from "react-router";
import AiAssistant from "./AiAssistant";

export const AppPageRouters: RouteObject[] = [
  {
    path: "application",
    children: [
      {
        path: "assistant",
        Component: AiAssistant,
      },
    ],
  },
];
  