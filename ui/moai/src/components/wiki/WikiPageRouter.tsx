import { RouteObject } from "react-router";
import WikiSettings from "./WikiSettings";
import WikiDocument from "./WikiDocument";
import WikiLayout from "./WikiLayout";
import DocumentEmbedding from "./DocumentEmbedding";
import WikiSearch from "./WikiSearch";
import WikiUser from "./WikiUser";
import WikiCrawle from "./WikiCrawle";
import WikiCrawleConfig from "./crawle/WikiCrawleConfig";
import WikiCrawleLayout from "./crawle/WikiCrawleLayout";
import WikiCrawleDocument from "./crawle/WikiCrawleDocument";
import WikiCrawleTask from "./crawle/WikiCrawleTask";
import WikiListPage from "./WikiListPage";

export const WikiPageRouters: RouteObject[] = [
  {
    path: "wiki",
    children: [
      {
        path: "list",
        Component: WikiListPage,
      },
      {
        path: ":id",
        Component: WikiLayout,
        children: [
          {
            path: "list",
            Component: WikiDocument,
          },
          {
            path: "crawle",
            Component: WikiCrawle,
          },
          {
            path: "crawle/{:crawleId}",
            Component: WikiCrawleLayout,
            children: [
              {
                path: "config",
                Component: WikiCrawleConfig,
              },
              {
                path: "task",
                Component: WikiCrawleTask,
              },
              {
                path: "document",
                Component: WikiCrawleDocument,
              }
            ]
          },
          {
            path: "settings",
            Component: WikiSettings,
          },
          {
            path: "document/:documentId/embedding",
            Component: DocumentEmbedding,
          },
          {
            path: "search",
            Component: WikiSearch,
          },
          {
            path: "user",
            Component: WikiUser,
          },
        ],
      },
      {
        path: "",
        Component: WikiListPage,
      },
    ],
  },
];
