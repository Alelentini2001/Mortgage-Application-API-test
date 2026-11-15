---
paths:
  - "Dockerfile"
  - "docker-compose*.yml"
  - "compose*.yaml"
  - ".dockerignore"
  - ".github/**"
---
# Infra rules
- Dockerfile: multi-stage (`sdk:10.0` → `aspnet:10.0`), non-root `USER $APP_UID`, port 8080, no secrets baked in.
- Compose is for local dev/demo only; production config comes from environment variables.
- GitHub workflows: least-privilege `permissions:` per job, pinned action versions, no secrets in logs.
- No AI review in CI: AI reviews run locally on Alessandro's subscription and are posted as PR comments.
- CI must pass without any secret: it runs `./scripts/verify.sh` and `docker build`.
