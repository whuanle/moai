import { RouteObject } from "react-router";
import CrawlerListPage from "./CrawlerListPage";
import CrawlerDetailPage from "./CrawlerDetailPage";

export const CrawlerPageRouters: RouteObject[] = [
  {
    path: "plugin/crawler/:configId",
    Component: CrawlerDetailPage,
  },
  {
    path: "plugin/crawler",
    Component: CrawlerListPage,
  },
];

