# Build stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/ddpc.DartSuite.Api/ddpc.DartSuite.Api.csproj", "src/ddpc.DartSuite.Api/"]
RUN dotnet restore "src/ddpc.DartSuite.Api/ddpc.DartSuite.Api.csproj"
COPY . .
WORKDIR "/src/src/ddpc.DartSuite.Api"
RUN dotnet build "ddpc.DartSuite.Api.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "ddpc.DartSuite.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "ddpc.DartSuite.Api.dll"]
