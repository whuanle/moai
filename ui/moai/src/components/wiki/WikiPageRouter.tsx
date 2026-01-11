import { RouteObject } from "react-router";
import WikiSettings from "./WikiSettings";
import WikiDocument from "./WikiDocument";
import WikiLayout from "./WikiLayout";
import DocumentEmbedding from "./DocumentEmbedding";
import WikiSearch from "./WikiSearch";
import WikiListPage from "./WikiListPage";
import BatchListPage from "./BatchListPage";
import { CrawlerPageRouters } from "./plugins/crawler/CrawlerPageRouter";
import { FeishuPageRouters } from "./plugins/feishu/FeishuPageRouter";
import { OpenApiPageRouters } from "./plugins/openapi/OpenApiPageRouter";
import { PaddleocrPageRouters } from "./plugins/paddleocr/PaddleocrPageRouter";

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
            path: "document",
            Component: WikiDocument,
          },
          ...CrawlerPageRouters,
          ...FeishuPageRouters,
          ...OpenApiPageRouters,
          ...PaddleocrPageRouters,
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
            path: "batch",
            Component: BatchListPage,
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
