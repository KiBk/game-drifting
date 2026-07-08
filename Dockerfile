FROM nginx:stable-alpine

LABEL org.opencontainers.image.title="Convoy Rally"
LABEL org.opencontainers.image.description="Unity WebGL game served by Nginx"

COPY deploy/nginx.conf /etc/nginx/conf.d/default.conf
COPY deploy/webgl/ /usr/share/nginx/html/

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
  CMD wget --quiet --tries=1 --spider http://127.0.0.1/healthz || exit 1
