# syntax=docker/dockerfile:1

FROM node:22-alpine AS web
WORKDIR /src/app/client
RUN corepack enable
COPY app/client/package.json app/client/pnpm-lock.yaml app/client/pnpm-workspace.yaml ./
RUN pnpm install --frozen-lockfile
COPY app/client/ ./
RUN pnpm ng build --configuration production

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src/app/api
COPY app/api/global.json app/api/Directory.Build.props app/api/Directory.Packages.props ./
COPY app/api/src/ ./src/
RUN dotnet restore src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.csproj --locked-mode
RUN dotnet publish src/Bootstrap/PersonalFinance.Api/PersonalFinance.Api.csproj -c Release --no-restore -o /out
RUN mkdir -p /data

FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled-extra
WORKDIR /app
COPY --from=api /out ./
COPY --from=web /src/app/client/dist/client/browser ./wwwroot
COPY --from=api --chown=1654:1654 /data /data
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__PersonalFinanceDb="Data Source=/data/personalfinance.db" \
    Database__MigrateOnStartup=true \
    Logging__LogLevel__Microsoft.EntityFrameworkCore=Warning
LABEL org.opencontainers.image.source="https://github.com/InakiPoch/personal-finance-app"
EXPOSE 8080
EXPOSE 8443
ENTRYPOINT ["dotnet", "PersonalFinance.Api.dll"]
