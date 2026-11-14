---
paths:
  - "Dockerfile"
  - "docker-compose*.yml"
  - ".github/**"
---
# Infra rules
- Dockerfile: multi-stage (`sdk:10.0` → `aspnet:10.0`), non-root `USER $APP_UID`, port 8080, no secrets baked in.
- Compose is for local dev/demo only; production config comes from environment variables.
- GitHub workflows: least-privilege `permissions:` per job, pinned action versions, no secrets in logs.
- AI review workflows run only for same-repo PRs and skip cleanly when the API key secret is absent.
- CI must pass without any secret: it runs `./scripts/verify.sh` and `docker build`.
