# syntax=docker/dockerfile:1

# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .

# Publish the web project.
# Workaround: If your csproj uses absolute ProjectReferences like /ddpc.DartSuite.Application/...
# we create root-level symlinks to wherever those projects exist inside /src.
RUN set -eux; \
    WEB_PROJ="$(find . -maxdepth 6 -name 'ddpc.DartSuite.Web.csproj' -print -quit)"; \
    echo "Web project: ${WEB_PROJ}"; \
    \
    for name in ddpc.DartSuite.Application ddpc.DartSuite.Infrastructure ddpc.DartSuite.Domain; do \
      p="$(find . -maxdepth 6 -name "${name}.csproj" -print -quit || true)"; \
      if [ -n "${p}" ]; then \
        dir="/src/$(dirname "${p#./}")"; \
        ln -s "${dir}" "/${name}" || true; \
        echo "Linked /${name} -> ${dir}"; \
      else \
        echo "WARNING: ${name}.csproj not found in build context"; \
      fi; \
    done; \
    \
    dotnet publish "${WEB_PROJ}" -c Release -o /app/out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app/out/ ./
ENV ASPNETCORE_URLS=http://0.0.0.0:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ddpc.DartSuite.Web.dll"]
