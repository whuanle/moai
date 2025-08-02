# Docker 部署说明

本项目提供了完整的 Docker 部署方案，包括多阶段构建的 Dockerfile 和 docker-compose 配置。

## 文件说明

- `Dockerfile` - 多阶段构建的 Docker 镜像定义
- `docker-compose.yml` - Docker Compose 配置文件
- `nginx.conf` - Nginx 服务器配置
- `.dockerignore` - Docker 构建时忽略的文件列表

## 快速开始

### 使用 Docker Compose（推荐）

1. **基本启动**：
```bash
docker-compose up --build
```

2. **指定后端服务地址**：
```bash
# 方法1：使用环境变量文件
echo "VITE_ServerUrl=http://your-backend-server:5000" > .env
docker-compose up --build

# 方法2：使用命令行参数
VITE_ServerUrl=http://your-backend-server:5000 docker-compose up --build
```

3. 访问应用：
   - 打开浏览器访问 `http://localhost:3000`

4. 停止服务：
```bash
docker-compose down
```

### 使用 Docker 命令

1. **构建镜像**：
```bash
# 使用默认后端地址
docker build -t moai-frontend .

# 指定后端服务地址
docker build --build-arg VITE_ServerUrl=http://your-backend-server:5000 -t moai-frontend .
```

2. **运行容器**：
```bash
docker run -p 3000:80 moai-frontend
```

3. 访问应用：
   - 打开浏览器访问 `http://localhost:3000`

## 构建阶段说明

Dockerfile 使用多阶段构建：

1. **base 阶段**：安装 Node.js 和生产依赖
2. **builder 阶段**：安装所有依赖并构建应用
3. **production 阶段**：使用 Nginx 服务静态文件

## 配置说明

### 环境变量配置

本项目使用 Vite 构建，环境变量在构建时被注入到前端代码中。重要说明：

- **构建时注入**：`VITE_ServerUrl` 等环境变量在 Docker 构建时被注入，不能在运行时修改
- **必须以 VITE_ 开头**：只有以 `VITE_` 开头的环境变量才会被 Vite 处理
- **重新构建**：修改环境变量后需要重新构建镜像

### Nginx 配置特性

- 静态资源缓存优化
- Gzip 压缩
- SPA 路由支持
- 安全头设置
- API 代理配置（已注释，可根据需要启用）

### 环境变量

- `NODE_ENV=production` - 生产环境配置

## 自定义配置

### 修改端口

在 `docker-compose.yml` 中修改端口映射：
```yaml
ports:
  - "8080:80"  # 将 3000 改为 8080
```

### 启用 API 代理

1. 取消注释 `nginx.conf` 中的 API 代理配置
2. 修改 `proxy_pass` 地址为实际的后端服务地址
3. 在 `docker-compose.yml` 中添加后端服务依赖

### 配置后端服务地址

1. **使用环境变量文件**：
   创建 `.env` 文件：
   ```bash
   VITE_ServerUrl=http://your-backend-server:5000
   ```

2. **使用命令行参数**：
   ```bash
   VITE_ServerUrl=http://your-backend-server:5000 docker-compose up --build
   ```

3. **在 docker-compose.yml 中直接设置**：
   ```yaml
   build:
     args:
       VITE_ServerUrl: http://your-backend-server:5000
   ```

### 添加其他环境变量

在 `docker-compose.yml` 中添加环境变量：
```yaml
environment:
  - NODE_ENV=production
  - REACT_APP_API_URL=http://backend:8080
```

## 生产部署建议

1. 使用 Docker Registry 存储镜像
2. 配置 HTTPS（使用 Let's Encrypt 或自签名证书）
3. 设置反向代理（如 Traefik）
4. 配置日志收集
5. 设置健康检查

## 故障排除

### 构建失败

1. 检查 Node.js 版本兼容性
2. 清理 Docker 缓存：`docker system prune -a`
3. 检查 `.dockerignore` 配置

### 应用无法访问

1. 检查端口映射是否正确
2. 查看容器日志：`docker logs <container_id>`
3. 检查防火墙设置

### 静态资源 404

1. 确认构建产物路径正确
2. 检查 Nginx 配置中的 `root` 路径
3. 验证文件权限 