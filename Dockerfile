FROM node:22-bookworm-slim AS web
WORKDIR /web
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci --no-audit --no-fund
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api
WORKDIR /src
COPY backend/src/ClerkAssistant.Api/ClerkAssistant.Api.csproj src/ClerkAssistant.Api/
RUN dotnet restore src/ClerkAssistant.Api/ClerkAssistant.Api.csproj
COPY backend/src/ src/
RUN dotnet publish src/ClerkAssistant.Api/ClerkAssistant.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=api /app ./
COPY --from=web /web/dist ./wwwroot
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_gcServer=0 \
    DOTNET_GCHeapHardLimitPercent=0x46
USER app
EXPOSE 8080
ENTRYPOINT ["dotnet", "ClerkAssistant.Api.dll"]
