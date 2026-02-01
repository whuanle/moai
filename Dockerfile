# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器，以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# ==================== 前端构建阶段 ====================
FROM node:22-slim AS frontend-build

# 安装必要的构建工具
RUN apt-get update && apt-get install -y \
    python3 \
    make \
    g++ \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# 复制前端项目文件
COPY ui/moai/package*.json ./

# 安装依赖
RUN npm ci || (rm -rf node_modules package-lock.json && npm install)

# 复制前端源代码
COPY ui/moai/ .

# 设置构建时环境变量（API 使用相对路径）
ARG VITE_ServerUrl=/api
ENV VITE_ServerUrl=${VITE_ServerUrl}

# 构建前端
RUN npm run build:docker

# ==================== 后端基础镜像 ====================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080

# ==================== 后端构建阶段 ====================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/", "src/"]
RUN dotnet restore "./src/MoAI/MoAI.csproj"
COPY . .
WORKDIR "/src/src/MoAI"
RUN dotnet build "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# ==================== 后端发布阶段 ====================
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# ==================== 最终镜像 ====================
FROM base AS final
WORKDIR /app

# 复制后端发布文件
COPY --from=publish /app/publish .

# 复制前端静态文件到 wwwroot
COPY --from=frontend-build /app/dist ./wwwroot

ENTRYPOINT ["dotnet", "MoAI.dll"]