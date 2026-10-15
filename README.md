<p align="center">
  <img src="ui/public/logo.png" width="110" alt="MoAI Logo">
</p>


<h1 align="center">MoAI</h1>

<p align="center">
  <strong>轻松搭建企业 Agent 应用的开源平台</strong>
</p>

<p align="center">
  <a href="https://moai.whuanle.cn/">官方网站</a> •
  <a href="https://docs.whuanle.cn/zh/moai">文档中心</a> •
  <a href="https://github.com/whuanle/moai">GitHub</a>
</p>

<p align="center">
  <img alt="license" src="https://img.shields.io/badge/License-Apache--2.0-blue">
  <img alt=".NET" src="https://img.shields.io/badge/.NET-10-purple">
  <img alt="React" src="https://img.shields.io/badge/React-19-61dafb">
</p>

---

## MoAI 是什么

MoAI 不是一个大语言模型，而是一个把**模型、企业文档和业务工具组合起来**的开源平台。选好模型，绑定知识与工具，调试后发布，就能搭建出知识问答、经营分析、客户服务等 AI 助手——发布给员工使用、接入现有业务系统，或嵌入对外网站，不需要从零开发整套平台。

把企业知识、业务系统和工具连接起来，搭建能**查资料、分析数据、执行任务**的 AI 助手。用过 Dify、FastGPT？试试 MoAI，让企业 Agent 搭建更简单、更顺手。

> 想先看效果？官网有一支 2 分钟宣传片：[moai.whuanle.cn](https://moai.whuanle.cn/)



## 作者微信

<img src="images/weix.png" alt="weix" style="zoom: 33%;" />



## 平台能力

### 🤖 Agent 与流程应用

自由推理与确定性编排，各得其所。Agent 应用自主规划与调用工具，流程应用在可视化画布上编排节点；从配置、调试到发布，在一个平台完成应用构建闭环。

### 📚 知识：建立连接

知识库提供文档语义检索（RAG），知识图谱组织业务实体关系，让回答既有依据，也有业务上下文——既读懂内容，也理解关系。

### 🔌 连接业务，执行行动

通过 MCP、OpenAPI 与插件接入业务系统，让现有系统成为 AI 的工具；在隔离沙箱中执行代码、处理文件，扩展应用的行动力。

### 🛡️ 企业级边界

团队资源隔离、角色权限、内容脱敏与接入范围控制，为团队协作和对外交付保留清晰边界，安全地把 AI 交到员工和客户手里。

## 业务实践

从企业内部分析，到面向客户的服务，看看 MoAI 如何进入真实的业务场景（完整案例见[文档中心 · 应用实践](https://docs.whuanle.cn/zh/moai)）：

| 场景 | 做什么 |
| --- | --- |
| [经营分析](https://docs.whuanle.cn/zh/moai/2.apply/1.data_p) | 接入业务数据库，Agent 分析销售趋势、组织图表、产出报告 |
| [机票查询](https://docs.whuanle.cn/zh/moai/2.apply/7.flight_query) | 航线台账进知识图谱、政策文档进知识库，双数据源回答比价与咨询 |
| [物流服务](https://docs.whuanle.cn/zh/moai/2.apply/8.logistics_query) | 一段代码嵌入官网，访客免登录查件、比价、问理赔 |
| [运维排查](https://docs.whuanle.cn/zh/moai/2.apply/3.dev_troubleshooting) | 接入观测平台，AI 查日志、追链路，给出带证据的排查报告 |

## 开放架构

模块清晰，扩展自由；适配既有技术生态，部署在自己的环境。

- **模块化单体**：.NET 10 + React 19 + PostgreSQL/pgvector，CQRS 分层，便于按领域扩展与二次开发
- **开放生态**：OpenAI 兼容模型入口、MCP 与 OpenAPI 插件接入、pgvector 向量存储、OpenSandbox 代码沙箱
- **自有基础设施**：支持 Docker、Docker Compose 和 Kubernetes 部署，基础设施与数据由你管理

## 快速开始

```bash
git clone https://github.com/whuanle/moai.git
cd moai
cp .env.example .env   # 先在 .env 配置账号与服务地址
bash deploy/deploy-compose.sh
```

一条命令拉起 MoAI 与常用依赖。部署方式（Docker / Compose / Kubernetes）与完整配置说明见[文档中心 · 安装](https://docs.whuanle.cn/zh/moai/1.install/2.docker-compose)。

## 文档

完整的安装、使用与实践文档：**https://docs.whuanle.cn/zh/moai**

- **安装** —— Docker / Docker Compose / Kubernetes 部署与配置参考
- **应用实践** —— 数据分析、机票查询、物流服务等端到端业务案例
- **使用指南** —— 模型、知识库、知识图谱、插件、应用接入等功能详解

## License

[Apache License 2.0](LICENSE.txt)

---

<p align="center">
  开放的能力，无限的业务可能 · <a href="https://moai.whuanle.cn/">Build with MoAI</a>
</p>

<p align="center">
  如果这个项目对你有帮助，欢迎 ⭐ Star 支持！
</p>
