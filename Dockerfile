# ==================== 前端构建阶段 ====================
FROM node:22-slim AS frontend-builder

RUN apt-get update && apt-get install -y \
    python3 \
    make \
    g++ \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

COPY ui/moai/package*.json ./

RUN npm ci

COPY ui/moai/ .

# 设置构建时环境变量（同源部署，使用空字符串）
ENV VITE_ServerUrl=""

# 重新安装依赖以解决 Rollup 可选依赖项问题
RUN rm -rf node_modules package-lock.json && npm install

# 构建应用
RUN npm run build

# ==================== 后端构建阶段 ====================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS backend-builder
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/", "src/"]
RUN dotnet restore "./src/MoAI/MoAI.csproj"
WORKDIR "/src/src/MoAI"
RUN dotnet build "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/build
RUN dotnet publish "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# ==================== 最终运行阶段 ====================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS final
WORKDIR /app

# 创建配置和文件目录
RUN mkdir -p /app/configs /app/files /app/wwwroot

# 复制后端发布文件
COPY --from=backend-builder /app/publish .

# 复制前端构建产物到 wwwroot
COPY --from=frontend-builder /app/dist ./wwwroot

# 复制 entrypoint 脚本
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

ENV MAI_CONFIG=/app/configs/system.json

ENTRYPOINT ["/app/docker-entrypoint.sh"]
