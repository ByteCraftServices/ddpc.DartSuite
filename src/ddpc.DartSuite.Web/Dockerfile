# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN find . -maxdepth 4 -name "ddpc.DartSuite.Web.csproj" -print

# WICHTIG: publish erzeugt /app/out
RUN dotnet publish "./ddpc.DartSuite.Web/ddpc.DartSuite.Web.csproj" -c Release -o /app/out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "ddpc.DartSuite.Web.dll"]
