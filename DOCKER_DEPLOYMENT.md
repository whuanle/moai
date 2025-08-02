# Docker 部署说明

## 概述

本项目使用 Docker Compose 实现前后端分开部署，前端能够动态使用后端地址。

## 服务说明

### 后端服务 (moaiapi)
- **容器名**: `moaiapi`
- **端口映射**: `5000:8080`
- **配置目录**: `./configs` 映射到 `/app/config`
- **环境变量**: `MAI_FILE=/app/config/system.json`

### 前端服务 (miaifront)
- **容器名**: `miaifront`
- **端口映射**: `3000:80`
- **动态配置**: 通过环境变量 `BACKEND_URL` 动态设置后端地址

## 部署步骤

### 生产环境部署

#### 1. 准备配置文件

确保 `configs` 目录存在并包含必要的配置文件：

```bash
mkdir -p configs
# 将配置文件复制到 configs 目录
cp src/MoAI/configs/* configs/
```

#### 2. 设置环境变量

复制环境变量示例文件：

```bash
cp env.example .env
```

根据需要修改 `.env` 文件中的 `BACKEND_URL`：

```bash
# 在 Docker 网络内部使用服务名
BACKEND_URL=http://moaiapi:8080

# 或者使用外部地址（如果前端需要从外部访问后端）
# BACKEND_URL=http://localhost:5000
```

#### 3. 启动服务

```bash
# 构建并启动所有服务
docker-compose up -d --build

# 查看服务状态
docker-compose ps

# 查看日志
docker-compose logs -f
```

#### 4. 访问应用

- **前端**: http://localhost:3000
- **后端**: http://localhost:5000

### 开发环境部署

#### 1. 启动开发环境

```bash
# 使用开发环境配置启动
docker-compose -f docker-compose.dev.yml up -d --build

# 查看开发环境服务状态
docker-compose -f docker-compose.dev.yml ps

# 查看开发环境日志
docker-compose -f docker-compose.dev.yml logs -f
```

#### 2. 开发环境特性

- **热重载**: 前端和后端都支持代码热重载
- **源码映射**: 源代码目录挂载到容器中
- **开发工具**: 包含完整的开发依赖和工具

#### 3. 访问开发环境

- **前端**: http://localhost:3000
- **后端**: http://localhost:5000

## 环境变量配置

### 主要环境变量

| 变量名 | 说明 | 默认值 | 示例 |
|--------|------|--------|------|
| `BACKEND_URL` | 后端服务地址 | `http://localhost:5000` | `http://moaiapi:8080` |
| `MAI_FILE` | 后端配置文件路径 | `/app/config/system.json` | `/app/config/system.json` |

### 配置示例

#### 开发环境
```bash
BACKEND_URL=http://localhost:5000
```

#### 生产环境（Docker 内部网络）
```bash
BACKEND_URL=http://moaiapi:8080
```

#### 生产环境（外部访问）
```bash
BACKEND_URL=http://your-domain.com:5000
```

## 动态配置原理

前端应用在构建时使用占位符 `__BACKEND_URL_PLACEHOLDER__`，在容器启动时通过启动脚本动态替换为实际的后端地址。

### 启动流程

1. 容器启动时执行 `docker-entrypoint.sh`
2. 脚本读取 `BACKEND_URL` 环境变量
3. 使用 `sed` 命令替换构建产物中的占位符
4. 启动 nginx 服务

## 快速部署

### 使用部署脚本

#### Linux/macOS
```bash
# 给脚本添加执行权限
chmod +x deploy.sh

# 运行部署脚本
./deploy.sh
```

#### Windows
```cmd
# 运行部署脚本
deploy.bat
```

### 手动部署

```bash
# 启动服务
docker-compose up -d

# 停止服务
docker-compose down

# 重新构建并启动
docker-compose up -d --build

# 查看日志
docker-compose logs -f moaiapi
docker-compose logs -f miaifront

# 进入容器
docker-compose exec moaiapi sh
docker-compose exec miaifront sh

# 清理
docker-compose down -v
docker system prune -f
```

## 故障排除

### 前端无法连接后端

1. 检查 `BACKEND_URL` 环境变量是否正确
2. 确认后端服务是否正常运行
3. 检查网络连接

```bash
# 检查后端服务状态
docker-compose ps moaiapi

# 查看后端日志
docker-compose logs moaiapi

# 测试网络连接
docker-compose exec miaifront wget -qO- http://moaiapi:8080/health
```

### 配置文件问题

1. 确认 `configs` 目录存在且包含必要文件
2. 检查文件权限

```bash
# 检查配置文件
ls -la configs/

# 检查容器内的配置文件
docker-compose exec moaiapi ls -la /app/config/
```

## 扩展配置

### 添加数据库服务

可以在 `docker-compose.yml` 中添加数据库服务：

```yaml
services:
  # ... 现有服务 ...
  
  mysql:
    image: mysql:8.0
    environment:
      MYSQL_ROOT_PASSWORD: your_password
      MYSQL_DATABASE: moai
    volumes:
      - mysql_data:/var/lib/mysql
    networks:
      - moai-network

volumes:
  mysql_data:
```

### 添加反向代理

可以使用 nginx 作为反向代理：

```yaml
services:
  # ... 现有服务 ...
  
  nginx-proxy:
    image: nginx:alpine
    ports:
      - "80:80"
    volumes:
      - ./nginx-proxy.conf:/etc/nginx/nginx.conf
    depends_on:
      - moaiapi
      - miaifront
    networks:
      - moai-network
``` 