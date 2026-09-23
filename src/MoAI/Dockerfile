# 后端专用 Dockerfile（不含前端静态资源），仅供 Visual Studio 调试或后端单独构建使用。
# 完整部署请使用仓库根目录 Dockerfile（前端编译进 wwwroot，由 .NET 同源托管）。

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
WORKDIR /app
EXPOSE 8080

# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0.203 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/", "src/"]
RUN dotnet restore "./src/MoAI/MoAI.csproj"
WORKDIR "/src/src/MoAI"
RUN dotnet build "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
RUN mkdir -p /app/configs /app/files
COPY --from=publish /app/publish .
COPY configs/system.example.json /app/configs/system.json.template
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh
ENV MAI_FILE=/app/configs/system.json
ENTRYPOINT ["/app/docker-entrypoint.sh"]
