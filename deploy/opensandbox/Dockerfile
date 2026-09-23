# ============================================================
# OpenSandbox 生命周期服务镜像（自打包兜底方案）
#
# 官方已提供现成镜像，优先使用（无需本文件）：
#   docker.io/opensandbox/server:latest
#   ghcr.io/opensandbox-group/opensandbox/server:latest
#   sandbox-registry.cn-zhangjiakou.cr.aliyuncs.com/opensandbox/server:release-1.1.0
#
# 仅在无法访问上述仓库时，用本文件自行打包：
#   docker build -t your-registry/opensandbox-server:latest deploy/opensandbox
# 然后设置 .env：OPENSANDBOX_SERVER_IMAGE=your-registry/opensandbox-server:latest
# ============================================================
FROM python:3.12-slim

# 可选：锁定 PyPI 上的 opensandbox-server 版本（留空安装最新）
ARG OPENSANDBOX_SERVER_VERSION=
ARG PIP_INDEX_URL=

RUN if [ -n "$PIP_INDEX_URL" ]; then \
        pip config set global.index-url "$PIP_INDEX_URL"; \
    fi \
    && if [ -n "$OPENSANDBOX_SERVER_VERSION" ]; then \
        pip install --no-cache-dir "opensandbox-server==$OPENSANDBOX_SERVER_VERSION"; \
    else \
        pip install --no-cache-dir opensandbox-server; \
    fi

ENV SANDBOX_CONFIG_PATH=/etc/opensandbox/config.toml

EXPOSE 8090

ENTRYPOINT ["opensandbox-server"]
CMD ["--config", "/etc/opensandbox/config.toml"]
