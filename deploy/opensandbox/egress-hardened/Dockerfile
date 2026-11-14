# ============================================================
# OpenSandbox Egress 加固镜像（可选平台强制基线）
#
# 在官方 egress 镜像上叠加 /var/egress/rules/deny.always（平台级强制黑名单：
# 优先级高于沙箱创建请求携带的 networkPolicy，应用侧无法覆盖，每分钟热加载）。
#
# 构建并推送：
#   docker build -t your-registry/opensandbox/egress:hardened deploy/opensandbox/egress-hardened
# 启用：取消 deploy/opensandbox/sandbox.toml [egress] image 的加固镜像注释行，
#       重启 opensandbox-server 后新建的沙箱自动携带。
# 规则文件支持通配符域名（*.example.com）、IP 与 CIDR 网段，# 开头为注释。
# ============================================================
FROM opensandbox/egress:v1.1.7

COPY deny.always /var/egress/rules/deny.always
