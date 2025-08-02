# 使用官方 Node.js 18 作为基础镜像
FROM node:18-alpine AS base

# 设置工作目录
WORKDIR /app

# 复制 package.json 和 package-lock.json
COPY package*.json ./

# 安装依赖
RUN npm ci --only=production

# 构建阶段
FROM base AS builder

# 安装所有依赖（包括开发依赖）
RUN npm ci

# 复制源代码
COPY . .

# 设置构建时环境变量，使用占位符
ARG VITE_ServerUrl=__BACKEND_URL_PLACEHOLDER__
ENV VITE_ServerUrl=${VITE_ServerUrl}

# 构建应用（使用宽松的 TypeScript 检查）
RUN npm run build:strict

# 生产阶段
FROM nginx:alpine AS production

# 安装必要的工具
RUN apk add --no-cache bash

# 复制构建产物到 nginx 目录
COPY --from=builder /app/dist /usr/share/nginx/html

# 复制 nginx 配置文件
COPY nginx.conf /etc/nginx/nginx.conf

# 复制启动脚本
COPY docker-entrypoint.sh /docker-entrypoint.sh
RUN chmod +x /docker-entrypoint.sh

# 暴露端口
EXPOSE 80

# 使用自定义启动脚本
ENTRYPOINT ["/docker-entrypoint.sh"] 