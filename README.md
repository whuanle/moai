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

### Docker Compose 一键部署

1. 克隆项目

```bash
git clone https://github.com/AIDotNet/MoAI.git
cd MoAI
```

2. 创建环境配置文件

```bash
cp .env.example .env
```

3. 编辑 `.env` 文件，配置必要参数

```env
# 数据库配置
POSTGRES_USER=postgres
POSTGRES_PASSWORD=moai123456
POSTGRES_DB=moai

# Redis 配置 (使用默认即可)

# RabbitMQ 配置
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest

# MoAI 服务配置 - 修改为你的实际访问地址
MOAI_SERVER_URL=http://your-domain:8080
MOAI_WEBUI_URL=http://your-domain:8080
MOAI_AES_KEY=your_aes_key_here

# MoAI 服务端口，容器暴露的端口
MOAI_PORT=8080
```

4. 启动服务

```bash
docker-compose up -d
```

5. 访问服务

- 后端 API: `http://localhost:8080`



### 服务组件

Docker Compose 包含以下服务：

| 服务 | 说明 | 默认端口 |
|------|------|----------|
| moai | MoAI 后端服务 | 8080 |
| postgres | PostgreSQL + pgvector | 5432 |
| redis | Redis 缓存 | 6379 |
| rabbitmq | RabbitMQ 消息队列 | 5672 / 15672 |

### 自定义配置

如需自定义配置，可挂载配置文件：

```bash
docker run -d \
  -v /your/config/path:/app/configs \
  -e MAI_CONFIG=/app/configs/system.yaml \
  -p 8080:8080 \
  registry.cn-hangzhou.aliyuncs.com/whuanle/moai:latest
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
cd ui/moai

# 安装依赖
npm install

# 开发模式
npm run dev

# 构建
npm run build
```

## 配置说明

详细配置请参考 `configs/` 目录下的模板文件，主要配置项：

- **Server**: 服务端访问地址
- **AES**: 敏感数据加密密钥
- **Database**: 数据库连接配置
- **Redis**: 缓存服务配置
- **Wiki**: 向量数据库配置
- **Message**: 消息队列配置
- **Storage**: 文件存储配置

## License

[MIT License](LICENSE.txt)

---

<p align="center">
  如果这个项目对你有帮助，欢迎 ⭐ Star 支持！
</p>
