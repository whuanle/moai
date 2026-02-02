import type { ReactNode } from "react";
import clsx from "clsx";
import Heading from "@theme/Heading";
import styles from "./styles.module.css";

type FeatureItem = {
  title: string;
  Svg: React.ComponentType<React.ComponentProps<"svg">>;
  description: ReactNode;
};

const FeatureList: FeatureItem[] = [
  {
    title: "简单易用",
    Svg: require("@site/static/img/undraw_docusaurus_mountain.svg").default,
    description: (
      <>
        不需要专业知识即可快速搭建各类 AI 应用，
        提供直观的用户界面和操作流程，让每个人都能轻松上手。 
      </>
    ),
  },
  {
    title: "插件系统",
    Svg: require("@site/static/img/undraw_docusaurus_tree.svg").default,
    description: (
      <>
        强大而灵活的插件系统，轻松扩展功能以满足各种需求。
        内置多种常用插件，支持自定义开发，支持接入第三方 MCP 服务，支持导入
        OpenAPI 扩展插件系统！
      </>
    ),
  },
  {
    title: "知识库",
    Svg: require("@site/static/img/undraw_docusaurus_react.svg").default,
    description: (
      <>
        灵活强大的知识库系统，支持多种数据源接入，轻松管理和利用知识资源。
        支持向量数据库，支持文件系统，支持在线文档，支持网页内容抓取等多种数据源。
      </>
    ),
  },
  {
    title: "团队",
    Svg: require("@site/static/img/undraw_docusaurus_react.svg").default,
    description: (
      <>
        支持多用户协作与权限管理，提升团队工作效率与安全性。
        支持团队成员分工协作，支持权限分级管理，保障数据安全与隐私。
      </>
    ),
  },
  {
    title: "模型管理",
    Svg: require("@site/static/img/undraw_docusaurus_react.svg").default,
    description: (
      <>便捷的模型管理功能，支持多种模型接入与切换，满足不同应用需求。</>
    ),
  },
  {
    title: "OAuth2.0",
    Svg: require("@site/static/img/undraw_docusaurus_react.svg").default,
    description: (
      <>支持飞书、钉钉等多种 OAuth2.0 认证方式，支持 OpenConnectionID 接入，保障安全便捷的用户登录体验。</>
    ),
  },
];

function Feature({ title, Svg, description }: FeatureItem) {
  return (
    <div className={clsx("col col--4")}>
      <div className="text--center">
        <Svg className={styles.featureSvg} role="img" />
      </div>
      <div className="text--center padding-horiz--md">
        <Heading as="h3">{title}</Heading>
        <p>{description}</p>
      </div>
    </div>
  );
}

export default function HomepageFeatures(): ReactNode {
  return (
    <section className={styles.features}>
      <div className="container">
        <div className="row">
          {FeatureList.map((props, idx) => (
            <Feature key={idx} {...props} />
          ))}
        </div>
      </div>
    </section>
  );
}
