FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

ARG PROJECT_PATH
ARG APP_DLL

COPY backend/ ./backend/
RUN dotnet restore "$PROJECT_PATH"
RUN dotnet publish "$PROJECT_PATH" --no-restore --configuration Release --output /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=build /app/publish .
COPY backend/Services/ApiGateway/ocelot.Docker.json ./ocelot.Docker.json

ARG APP_DLL
ENV APP_DLL=${APP_DLL}
ENTRYPOINT ["sh", "-c", "dotnet \"$APP_DLL.dll\""]
