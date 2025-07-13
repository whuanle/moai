import { RouteObject } from "react-router";
import WikiPage from "./WikiPage";
import WikiSettings from "./WikiSettings";
import WikiDocument from "./WikiDocument";
import WikiLayout from "./WikiLayout";
import DocumentEmbedding from "./DocumentEmbedding";
import WikiSearch from "./WikiSearch";
import WikiUser from "./WikiUser";

export const WikiPageRouters: RouteObject[] = [
  {
    path: "wiki",
    children: [
      {
        path: "list",
        Component: WikiPage,
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
        Component: WikiPage,
      },
    ],
  },
];
