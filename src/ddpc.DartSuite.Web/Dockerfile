# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

# Find the project file inside the build context and publish it.
# This avoids leading-slash paths (e.g. "/something.csproj") which MSBuild can treat as a switch.
RUN set -eux; \
    PROJ="$(find . -maxdepth 6 -name 'ddpc.DartSuite.Web.csproj' -print -quit)"; \
    echo "Using project: ${PROJ}"; \
    dotnet publish "${PROJ}" -c Release -o /app/out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out/ ./

# Bind to all interfaces on port 8080 (Fly expects your app to listen on the internal_port).
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "ddpc.DartSuite.Web.dll"]
