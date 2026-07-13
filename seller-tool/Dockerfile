# Build context is seller-tool/ (the whole tool solution). Publishes the API, which pulls in the
# Data + Dating projects via project references.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
RUN apt-get update && apt-get install -y --no-install-recommends libgssapi-krb5-2 && rm -rf /var/lib/apt/lists/*
USER $APP_UID
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet restore "Api/EdenRelics.SellerTool.Api.csproj"
RUN dotnet publish "Api/EdenRelics.SellerTool.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "EdenRelics.SellerTool.Api.dll"]
