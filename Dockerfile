FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/", "src/"]
RUN dotnet restore "./src/MoAI/MoAI.csproj"
WORKDIR "/src/src/MoAI"
RUN dotnet build "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./MoAI.csproj" -c $BUILD_CONFIGURATION -o /app/publish

FROM base AS final
WORKDIR /app

# 创建配置和文件目录
RUN mkdir -p /app/configs /app/files

# 复制 entrypoint 脚本
COPY docker-entrypoint.sh /app/docker-entrypoint.sh
RUN chmod +x /app/docker-entrypoint.sh

COPY --from=publish /app/publish .

ENV MAI_CONFIG=/app/configs/system.json
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["/app/docker-entrypoint.sh"]
