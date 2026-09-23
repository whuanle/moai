<p align="center">
  <img src="moai_docs/static/img/logo.png" width="120" alt="MoAI Logo">
</p>

<h1 align="center">MoAI</h1>

<p align="center">
  <strong>开源 AI 应用平台 - 构建你的智能助手</strong>
</p>

<p align="center">
  <a href="https://moai.anyai.wiki">📖 文档</a> •
  <a href="#快速开始">🚀 快速开始</a> •
  <a href="#功能特性">✨ 功能特性</a>
</p>

---

## 简介

MoAI 是一个功能丰富的开源 AI 应用平台，支持多种主流 AI 模型接入，提供知识库管理、插件扩展、工作流自动化等能力，帮助你快速构建企业级 AI 应用。

## 功能特性

🤖 **多模型支持**
- OpenAI、Anthropic、HuggingFace、Mistral 等主流模型
- 统一的模型管理和调用接口
- 支持自定义模型接入

📚 **知识库管理**
- 文档向量化与语义搜索
- 支持多种文档格式 (PDF、Word、Markdown 等)
- 基于 pgvector 的高效向量存储

🔌 **插件系统**
- 原生插件、自定义插件、工具插件
- 灵活的插件开发框架
- 支持 MCP 协议

💬 **AI 对话**
- 多轮对话上下文管理
- 提示词模板管理
- 流式响应支持

👥 **团队协作**
- 多用户权限管理
- OAuth2.0 登录 (飞书、钉钉、企业微信)
- 团队资源共享

📁 **文件存储**
- 本地存储、S3、MinIO
- 阿里云 OSS、腾讯云 COS
- 统一的存储抽象层

⚙️ **工作流自动化**
- 可视化工作流编排
- 丰富的节点类型
- 定时任务支持

## 技术栈

| 后端 | 前端 |
|------|------|
| .NET 9 / ASP.NET Core | React 19 / TypeScript |
| Entity Framework Core | Ant Design / LobeHub UI |
| MediatR (CQRS) | Redux Toolkit / Zustand |
| Semantic Kernel | Vite 6 |

## 快速开始

### Docker 部署

支持两种方式，完整说明见 [docs/deployment/docker.md](./docs/deployment/docker.md)。

**方式一：Docker Compose 一体部署**（postgres+pgvector / redis / rabbitmq / rustfs / opensandbox-server / moai）

```bash
cp .env.example .env          # 按需修改基础设施账号/端口；MOAI_HOST 留空会自动探测
bash deploy/deploy-compose.sh # 自动探测主机 + 预拉沙箱镜像 + 拉取/构建 + 启动
```

**方式二：Docker 单容器**（仅前后端；外部提供 PostgreSQL/Redis/RabbitMQ/OSS）

```bash
docker run -d --name moai -p 8080:8080 \
  --add-host host.docker.internal:host-gateway \
  -v "$(pwd)/configs/system.json:/app/configs/system.json:ro" \
  -v moai_files:/app/files \
  whuanle/moai:latest
```

> 无论哪种方式，都必须把 `configs/system.json` 映射进容器（`MAI_FILE` 默认 `/app/configs/system.json`）。前端已编译进后端 `wwwroot` 同源托管，无需单独部署前端。

### 服务组件（Compose）

| 服务 | 说明 | 默认端口 |
|------|------|----------|
| moai | MoAI 服务（前端 + 后端） | 8080 |
| postgres | PostgreSQL + pgvector（`pgvector/pgvector:pg16`，不可用裸 postgres） | 5432 |
| redis | Redis 缓存 | 6379 |
| rabbitmq | RabbitMQ 消息队列 | 5672 / 15672 |
| rustfs | RustFS 对象存储（S3 兼容，替代 MinIO） | 9000 / 9001 |
| opensandbox-server | OpenSandbox 沙箱服务（可选能力） | 18123 |

图数据库（Memgraph/Neo4j）不内置，知识图谱按需接入，见部署文档。

### 镜像发布

```bash
bash deploy/publish.sh          # 构建并推送 whuanle/moai:<tag> 与 :latest
```

## 文档

完整文档请访问：**https://moai.anyai.wiki**

文档包含：
- 快速入门指南
- Docker 部署教程
- AI 模型配置
- 插件开发指南
- 知识库使用说明
- API 参考

## 本地开发

### 后端

```bash
# 构建
dotnet build MoAI.sln

# 运行
dotnet run --project src/MoAI/MoAI.csproj
```

### 前端

```bash
cd ui

# 安装依赖
npm install

# 开发模式
npm run dev

# 构建
npm run build
```

## 配置说明

详细配置请参考 `configs/system.json`（模板）与 [docs/deployment/docker.md](./docs/deployment/docker.md)，主要配置项：

- **Server / WebUI**: 服务端与前端访问地址
- **AES**: 敏感数据加密密钥
- **Database**: PostgreSQL（pgvector）连接配置
- **Redis**: 缓存服务配置
- **RabbitMQ**: 消息队列配置
- **Storage**: S3 兼容对象存储配置（RustFS/MinIO/OSS/S3）
- **OpenSandBox**: 沙箱服务地址、镜像与超时
- **OTLP**: 可观测性上报（可选）

## License

[MIT License](LICENSE.txt)

---

<p align="center">
  如果这个项目对你有帮助，欢迎 ⭐ Star 支持！
</p>
