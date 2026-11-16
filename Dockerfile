# Multi-stage build: the SDK image compiles, the smaller ASP.NET Core runtime image runs.
# Pinned by multi-arch manifest digest (Phase 8, P8-D5 A; Dependabot opens PRs for updates).

FROM mcr.microsoft.com/dotnet/sdk:10.0@sha256:fe0fd4e93bd2fbb47c883b7a6369e5c8d3db8bf01cdc5981f6ee35fbdf3ff4fb AS build
WORKDIR /src

# Restore as its own layer, so it is cached until a project or package file changes.
# Only the Api project is restored: the .slnx also lists the test projects, which .dockerignore excludes.
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/MortgageApplications.Core/MortgageApplications.Core.csproj src/MortgageApplications.Core/packages.lock.json src/MortgageApplications.Core/
COPY src/MortgageApplications.Infrastructure/MortgageApplications.Infrastructure.csproj src/MortgageApplications.Infrastructure/packages.lock.json src/MortgageApplications.Infrastructure/
COPY src/MortgageApplications.Api/MortgageApplications.Api.csproj src/MortgageApplications.Api/packages.lock.json src/MortgageApplications.Api/
RUN dotnet restore src/MortgageApplications.Api/MortgageApplications.Api.csproj --locked-mode

# .editorconfig is copied after the restore, so a style-rule change keeps the restore cache; publish still applies
# the same analyzer settings as scripts/verify.sh.
COPY .editorconfig ./
COPY src/ src/
RUN dotnet publish src/MortgageApplications.Api/MortgageApplications.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0@sha256:bfd912c83295ce66d4b706ebb8541b9f14259b0e76fbb88b2751f58ce2399d2e AS runtime
WORKDIR /app

# The SQLite file lives in /app/data (a named volume in compose); the non-root app user must own it.
RUN mkdir -p /app/data && chown "$APP_UID" /app/data

COPY --from=build /app/publish .

# No secrets or connection strings here: configuration comes from environment variables at run time.
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "MortgageApplications.Api.dll"]
