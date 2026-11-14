#!/usr/bin/env bash
# ============================================================
# 在**目标服务器**上执行：从内网 Zot 拉取新镜像 → 切换 → 健康检查
#
# 调用方式（由 .gitea/workflows/deploy.yml 通过 ssh 管道注入）：
#   { printf 'export MOAI_IMAGE_REF=%q ...\n' ; cat remote-deploy.sh ; } | ssh ... 'bash'
# 也可在服务器上手动执行：
#   MOAI_IMAGE_REF=<registry>/moai:<tag> bash remote-deploy.sh
#
# 环境变量：
#   MOAI_IMAGE_REF        必填，完整镜像引用
#   MOAI_APP_DIR          部署目录，默认 $HOME/moai
#   MOAI_PULL             1=先登录并 docker pull（默认），0=镜像已在本地
#   ZOT_REGISTRY_TARGET   Zot host:port（纯 HTTP 时自动加入 insecure-registries）
#   ZOT_USERNAME / ZOT_PASSWORD   Zot 凭据
#
# 行为：
#   1. 探测 moai 网络：存在则用 docker-compose.reuse.yml 复用
#      PostgreSQL/Redis/RabbitMQ/RustFS（只起 MoAI 自己的库初始化、建桶、沙箱与应用），
#      不存在则用 docker-compose.yml 起完整栈
#   2. 纯 HTTP registry 自动写入 daemon insecure-registries 并 SIGHUP 重载
#   3. 临时 DOCKER_CONFIG 登录并 pull，不污染部署用户 ~/.docker
#   4. 备份 .env → 改写 MOAI_IMAGE；MOAI_PORT 为空时沿用现有容器端口
#   5. docker compose up -d moai（幂等重跑 postgres-init / rustfs-init）
#   6. 轮询宿主机端口，两分钟内不通则回滚 .env 与容器，退出码 1
# ============================================================
set -euo pipefail

IMAGE_REF="${MOAI_IMAGE_REF:?用法: MOAI_IMAGE_REF=<registry>/moai:<tag> bash remote-deploy.sh}"
APP_DIR="${MOAI_APP_DIR:-moai}"
case "$APP_DIR" in
  /*) ;;
  *) APP_DIR="$HOME/$APP_DIR" ;;
esac
PULL="${MOAI_PULL:-1}"

cd "$APP_DIR"
[ -f .env ] || { echo "!! 缺少 ${APP_DIR}/.env，请按 .env.example 准备一次"; exit 1; }
[ -f docker-compose.yml ] || { echo "!! 缺少 ${APP_DIR}/docker-compose.yml"; exit 1; }

set_env() {
  local key="$1" value="$2"
  if grep -q "^${key}=" .env; then
    sed -i "s|^${key}=.*|${key}=${value}|" .env
  else
    printf '%s=%s\n' "$key" "$value" >> .env
  fi
}

get_env() { sed -n "s/^$1=//p" .env | head -1; }

# 复用模式下容器名可能是 moai-postgres，也可能是别的 Mo 系列服务（如 mopl-postgres），
# 与 deploy/deploy-compose.sh 同口径：按 compose service label + moai 网络反查，再兜底固定名。
resolve_service() {
  local service="$1" id name candidate
  id="$(docker ps --filter "label=com.docker.compose.service=${service}" --filter "network=moai" \
        --format '{{.ID}}' | sed -n '1p')"
  if [ -n "$id" ]; then
    name="$(docker inspect --format '{{.Name}}' "$id")"
    printf '%s' "${name#/}"
    return 0
  fi
  for candidate in "moai-${service}" "mopl-${service}"; do
    if docker inspect "$candidate" >/dev/null 2>&1; then printf '%s' "$candidate"; return 0; fi
  done
  return 1
}

service_state() {
  docker inspect -f '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' "$1" 2>/dev/null || true
}

COMPOSE=( -f docker-compose.yml )
if docker network inspect moai >/dev/null 2>&1; then
  [ -f docker-compose.reuse.yml ] || { echo "!! 检测到 moai 网络但缺少 docker-compose.reuse.yml"; exit 1; }
  infra_ok=1
  for service in postgres redis rabbitmq rustfs; do
    container="$(resolve_service "$service" || true)"
    if [ -z "$container" ]; then
      echo "!! moai 网络存在但找不到 ${service} 容器，请先启动共享基础设施（不要另起一套）"
      infra_ok=0; break
    fi
    state="$(service_state "$container")"
    expected=running
    [ "$service" != rustfs ] && expected=healthy
    if [ "$state" != "$expected" ]; then
      echo "!! 共享 ${service} 容器 ${container} 状态为 ${state}，预期 ${expected}"
      infra_ok=0; break
    fi
    list_var="MOAI_$(printf '%s' "$service" | tr '[:lower:]' '[:upper:]')_HOST"
    export "${list_var}=${container}"
    echo "==> 复用 ${service} → ${container}"
  done
  [ "$infra_ok" = 1 ] || exit 1
  COMPOSE=( -f docker-compose.reuse.yml )
  echo "==> 检测到 moai 网络，使用 docker-compose.reuse.yml（不重复启动 PostgreSQL/Redis/RabbitMQ/RustFS）"
else
  echo "==> 未检测到 moai 网络，使用 docker-compose.yml 完整栈"
fi

# ---------- 拉取镜像（Zot 为纯 HTTP LAN registry） ----------
if [ "$PULL" = "1" ]; then
  if [ -n "${ZOT_REGISTRY_TARGET:-}" ]; then
    # 幂等确保 daemon 把 Zot 当 insecure registry（HTTP）
    sudo install -d -m 755 /etc/docker
    if [ -f /etc/docker/daemon.json ]; then
      sudo cp -f /etc/docker/daemon.json "/etc/docker/daemon.json.bak.$(date +%s)"
    else
      printf '{}\n' | sudo tee /etc/docker/daemon.json >/dev/null
    fi
    sudo python3 - "$ZOT_REGISTRY_TARGET" <<'PY'
import json, pathlib, sys
path = pathlib.Path('/etc/docker/daemon.json')
config = json.loads(path.read_text() or '{}')
registries = config.setdefault('insecure-registries', [])
if sys.argv[1] not in registries:
    registries.append(sys.argv[1])
path.write_text(json.dumps(config, indent=2) + '\n')
PY
    sudo dockerd --validate --config-file=/etc/docker/daemon.json >/dev/null
    sudo systemctl kill --signal=HUP --kill-whom=main docker || sudo systemctl reload docker
    for _ in $(seq 1 15); do
      if docker info --format '{{json .RegistryConfig.IndexConfigs}}' | grep -F "${ZOT_REGISTRY_TARGET}" >/dev/null; then
        break
      fi
      sleep 1
    done
    docker info --format '{{json .RegistryConfig.IndexConfigs}}' | grep -F "${ZOT_REGISTRY_TARGET}" >/dev/null \
      || { echo "!! Docker daemon 未识别 ${ZOT_REGISTRY_TARGET} 为 insecure registry"; exit 1; }
  fi

  REGISTRY_HOST="${IMAGE_REF%%/*}"
  DOCKER_CONFIG_DIR="$(mktemp -d)"
  chmod 700 "$DOCKER_CONFIG_DIR"
  trap 'rm -rf "$DOCKER_CONFIG_DIR"' EXIT
  if [ -n "${ZOT_USERNAME:-}" ] && [ -n "${ZOT_PASSWORD:-}" ]; then
    AUTH="$(printf '%s:%s' "$ZOT_USERNAME" "$ZOT_PASSWORD" | base64 | tr -d '\n')"
    printf '{"auths":{"%s":{"auth":"%s"}}}\n' "$REGISTRY_HOST" "$AUTH" > "$DOCKER_CONFIG_DIR/config.json"
    chmod 600 "$DOCKER_CONFIG_DIR/config.json"
  fi
  export DOCKER_CONFIG="$DOCKER_CONFIG_DIR"
  echo "==> 拉取 ${IMAGE_REF}"
  docker pull "$IMAGE_REF"
fi

docker image inspect "$IMAGE_REF" >/dev/null 2>&1 || { echo "!! 目标机没有镜像 ${IMAGE_REF}"; exit 1; }

PREV_IMAGE="$(get_env MOAI_IMAGE)"
cp -f .env ".env.bak.$(date +%Y%m%d-%H%M%S)"
set_env MOAI_IMAGE "$IMAGE_REF"

# 端口沿用：.env 里 MOAI_PORT 为空时不套用 compose 默认值，避免 8080↔8081 漂移
if [ -z "$(get_env MOAI_PORT)" ]; then
  CUR_PORT="$(docker port moai 8080/tcp 2>/dev/null | head -1 | sed 's/.*://')"
  if [ -n "$CUR_PORT" ]; then
    set_env MOAI_PORT "$CUR_PORT"
    echo "==> 沿用现有宿主端口 MOAI_PORT=${CUR_PORT}"
  fi
else
  echo "==> 使用 .env 配置的宿主端口 MOAI_PORT=$(get_env MOAI_PORT)"
fi

echo "==> 切换镜像：${PREV_IMAGE:-<未设置>} → ${IMAGE_REF}"
# 不带 --no-deps：让 postgres-init（建库+扩展）与 rustfs-init（建桶）每次幂等跑一遍
docker compose "${COMPOSE[@]}" up -d moai

# 取宿主机映射端口（兼容 MOAI_PORT=8080 / 复用模式 8081 等）
HOST_PORT="$(docker compose "${COMPOSE[@]}" port moai 8080 2>/dev/null | head -1 | awk -F: '{print $NF}')"
[ -n "$HOST_PORT" ] || HOST_PORT="$(get_env MOAI_PORT)"
[ -n "$HOST_PORT" ] || HOST_PORT=8080

port_open() {
  if command -v curl >/dev/null 2>&1; then
    [ "$(curl -s -o /dev/null -w '%{http_code}' --max-time 3 "http://127.0.0.1:${HOST_PORT}/" 2>/dev/null)" != "000" ]
  else
    timeout 3 bash -c "cat < /dev/null > /dev/tcp/127.0.0.1/${HOST_PORT}" 2>/dev/null
  fi
}

echo "==> 等待 http://127.0.0.1:${HOST_PORT}/ 就绪（最多 120s）"
ok=0
for _ in $(seq 1 60); do
  if port_open; then ok=1; break; fi
  sleep 2
done

if [ "$ok" != "1" ]; then
  echo "!! 健康检查失败，最近日志："
  docker compose "${COMPOSE[@]}" logs --tail=100 moai || true
  if [ -n "$PREV_IMAGE" ]; then
    echo "==> 回滚到 ${PREV_IMAGE}"
    set_env MOAI_IMAGE "$PREV_IMAGE"
    docker compose "${COMPOSE[@]}" up -d moai || true
  fi
  exit 1
fi

echo "==> 部署成功：${IMAGE_REF}（宿主端口 ${HOST_PORT}）"
docker compose "${COMPOSE[@]}" ps moai

# 回收本仓库历史 tag（保留最近 5 个，运行中的镜像 docker 会拒绝删除，忽略即可）
REPO="${IMAGE_REF%:*}"
docker images "$REPO" --format '{{.CreatedAt}} {{.ID}}' \
  | sort -r | awk 'NR>5 {print $2}' | sort -u \
  | xargs -r docker rmi -f >/dev/null 2>&1 || true
