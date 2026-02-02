---
sidebar_position: 1
---

# Docker 部署指南

本文档介绍如何使用 Docker 和 Docker Compose 部署 MoAI 服务。

部署完成后，默认管理员账户密码为：

```
admin
abcd123456
```





## 前置要求

- Docker 20.10+
- Docker Compose 2.0+（使用 Docker Compose 部署时需要）

## 方式一：Docker Compose 部署（推荐）

Docker Compose 会自动部署 PostgreSQL（含 pgvector）、Redis、RabbitMQ 和 MoAI 服务。

### 1. 配置环境变量

```bash
# 复制环境变量模板
cp .env.example .env

# 编辑配置文件
vim .env
```

主要配置项：

```bash
# MoAI Docker Compose 环境变量配置
# 复制此文件为 .env 并根据实际情况修改

# ============================================
# MoAI 服务配置 (必须配置)
# ============================================

# 外部访问地址 - 根据你的部署环境修改
# 例如: http://your-domain.com:8080 或 http://192.168.1.100:8080
MOAI_SERVER_URL=http://localhost:8080
MOAI_WEBUI_URL=http://localhost:8080

# AES 加密密钥 - 生产环境请修改为随机字符串
MOAI_AES_KEY=moai_aes_key_2024

# MoAI 服务端口
MOAI_PORT=8080

# ============================================
# PostgreSQL 数据库配置
# ============================================
POSTGRES_USER=postgres
POSTGRES_PASSWORD=moai123456
POSTGRES_DB=moai
POSTGRES_PORT=5432

# ============================================
# Redis 配置
# ============================================
REDIS_PORT=6379

# ============================================
# RabbitMQ 配置
# ============================================
RABBITMQ_USER=guest
RABBITMQ_PASSWORD=guest
RABBITMQ_PORT=5672
RABBITMQ_MANAGEMENT_PORT=15672

# ============================================
# OpenTelemetry (OTLP) 配置 (可选)
# ============================================
# 启用 OTLP 后可将 Trace 和 Metrics 发送到 Jaeger、Zipkin、Grafana 等
# 留空则禁用
OTLP_TRACE=http://127.0.0.1:4012/v1/traces
OTLP_METRICS=http://127.0.0.1:4012/v1/metrics
# Protocol: 0=grpc, 1=http/protobuf
OTLP_PROTOCOL=0
```

### 2. 启动服务

```bash
# 构建并启动所有服务
docker-compose up -d

# 查看服务状态
docker-compose ps

# 查看日志
docker-compose logs -f moai
```

### 3. 停止服务

```bash
# 停止服务
docker-compose down

# 停止并删除数据卷（谨慎操作）
docker-compose down -v
```

### 4. 更新服务

```bash
# 重新构建并启动
docker-compose up -d --build
```

## 方式二：单独 Docker 部署

如果你已有 PostgreSQL、Redis、RabbitMQ 服务，可以单独部署 MoAI。

在部署 MoAI 之前，请确保已准备好以下依赖服务：

| 服务       | 版本要求 | 说明                                               |
| ---------- | -------- | -------------------------------------------------- |
| PostgreSQL | 16+      | 推荐使用 pgvector/pgvector:pg16 镜像，支持向量存储 |
| Redis      | 7+       | 用于缓存和会话管理                                 |
| RabbitMQ   | 3+       | 消息队列（可选，可使用内存队列）                   |

### 1. 构建镜像

```bash
docker build -t moai .
```

### 2. 运行容器

```bash
docker run -d \
  --name moai-api \
  -p 8080:8080 \
  -v moai_files:/app/files \
  -e MOAI_SERVER_URL=http://your-domain.com:8080 \
  -e MOAI_WEBUI_URL=http://your-domain.com:8080 \
  -e MOAI_AES_KEY=your_random_key_here \
  -e POSTGRES_HOST=your-postgres-host \
  -e POSTGRES_PORT=5432 \
  -e POSTGRES_USER=postgres \
  -e POSTGRES_PASSWORD=your_password \
  -e POSTGRES_DB=moai \
  -e REDIS_HOST=your-redis-host \
  -e REDIS_PORT=6379 \
  -e RABBITMQ_HOST=your-rabbitmq-host \
  -e RABBITMQ_PORT=5672 \
  -e RABBITMQ_USER=guest \
  -e RABBITMQ_PASSWORD=guest \
  moai
```

### 3. 管理容器

```bash
# 查看日志
docker logs -f moai-api

# 停止容器
docker stop moai-api

# 启动容器
docker start moai-api

# 删除容器
docker rm moai-api
```

## 环境变量说明

| 变量名 | 说明 | 默认值 |
|--------|------|--------|
| `MOAI_SERVER_URL` | 后端服务外部访问地址 | `http://localhost:8080` |
| `MOAI_WEBUI_URL` | 前端访问地址 | `http://localhost:8080` |
| `MOAI_AES_KEY` | AES 加密密钥 | `moai_aes_key_2024` |
| `POSTGRES_HOST` | PostgreSQL 主机 | `postgres` |
| `POSTGRES_PORT` | PostgreSQL 端口 | `5432` |
| `POSTGRES_USER` | PostgreSQL 用户名 | `postgres` |
| `POSTGRES_PASSWORD` | PostgreSQL 密码 | `moai123456` |
| `POSTGRES_DB` | PostgreSQL 数据库名 | `moai` |
| `REDIS_HOST` | Redis 主机 | `redis` |
| `REDIS_PORT` | Redis 端口 | `6379` |
| `RABBITMQ_HOST` | RabbitMQ 主机 | `rabbitmq` |
| `RABBITMQ_PORT` | RabbitMQ 端口 | `5672` |
| `RABBITMQ_USER` | RabbitMQ 用户名 | `guest` |
| `RABBITMQ_PASSWORD` | RabbitMQ 密码 | `guest` |

## 数据持久化

Docker Compose 部署会创建以下数据卷：

| 卷名 | 说明 |
|------|------|
| `postgres_data` | PostgreSQL 数据 |
| `redis_data` | Redis 数据 |
| `rabbitmq_data` | RabbitMQ 数据 |
| `moai_files` | MoAI 上传文件 |

## PostgreSQL pgvector 扩展

Docker Compose 部署使用 `pgvector/pgvector:pg16` 镜像，启动时会自动初始化 vector 扩展。

如果单独部署 PostgreSQL，需要手动安装 pgvector 并执行：

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

## 常见问题

### 服务启动失败

检查依赖服务是否正常：

```bash
# 查看所有服务状态
docker-compose ps

# 查看具体服务日志
docker-compose logs postgres
docker-compose logs redis
docker-compose logs rabbitmq
```

### 数据库连接失败

1. 确认 PostgreSQL 服务已启动且健康
2. 检查数据库连接配置是否正确
3. 确认 pgvector 扩展已安装

### 文件上传目录权限问题

```bash
# 检查卷挂载
docker volume inspect moai_files
```
